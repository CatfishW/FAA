using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;

namespace FAA.XPlaneIntegration.Runtime
{
    public static class XPlaneDiscoveryMqtt
    {
        public static async Task Run(XPlaneMqttDiscoveryConfig config,Action<XPlaneDiscoveredFrame> publish,Action<string> diagnostic,CancellationToken token)
        {
            string endpoint=config.host+":"+config.port;
            while(!token.IsCancellationRequested)
            {
                try
                {
                    using(var client=new MqttFactory().CreateMqttClient())
                    {
                        var matcher=new XPlaneMqttTopicMatcher(endpoint);var sync=new object();
                        var options=new MqttClientOptionsBuilder().WithTcpServer(config.host,config.port).WithClientId("faa-discovery-"+Guid.NewGuid().ToString("N")).WithCleanSession().WithKeepAlivePeriod(TimeSpan.FromSeconds(10)).WithCommunicationTimeout(TimeSpan.FromSeconds(3));
                        if(config.tls)options.WithTls();
                        string username=string.IsNullOrEmpty(config.usernameEnvironment)?null:Environment.GetEnvironmentVariable(config.usernameEnvironment);
                        string password=string.IsNullOrEmpty(config.passwordEnvironment)?null:Environment.GetEnvironmentVariable(config.passwordEnvironment);
                        if(!string.IsNullOrEmpty(config.usernameEnvironment)&&username==null||!string.IsNullOrEmpty(config.passwordEnvironment)&&password==null)
                            throw new InvalidOperationException("Configured credential environment variable is missing.");
                        if(username!=null)options.WithCredentials(username,password);
                        client.UseApplicationMessageReceivedHandler(message=>
                        {
                            if(token.IsCancellationRequested)return;
                            XPlaneDiscoveredFrame frame;
                            lock(sync)frame=matcher.Accept(message.ApplicationMessage.Topic,message.ApplicationMessage.Payload,message.ApplicationMessage.Retain,XPlaneDiscoveryData.Now);
                            if(frame!=null)publish(frame);
                        });
                        using(var connection=CancellationTokenSource.CreateLinkedTokenSource(token))
                        {connection.CancelAfter(TimeSpan.FromSeconds(4));await client.ConnectAsync(options.Build(),connection.Token);}
                        await client.SubscribeAsync(new TopicFilterBuilder().WithTopic(config.topicFilter).WithAtMostOnceQoS().Build());
                        diagnostic("Observing MQTT topic schemas at "+endpoint+" (retained messages ignored)");
                        double broadSince=XPlaneDiscoveryData.Now,lastScan=broadSince;bool broad=true;string[] subscribed=Array.Empty<string>();
                        try
                        {
                            while(!token.IsCancellationRequested&&client.IsConnected)
                            {
                                await Task.Delay(250,token);double now=XPlaneDiscoveryData.Now;bool overload;string[] matches;
                                lock(sync){overload=matcher.Overloaded;matches=matcher.Filters;}
                                if(overload)throw new InvalidOperationException("MQTT observation budget exceeded.");
                                // Once semantic candidates are known, stop wildcard observation and retain exact relevant filters.
                                if(broad&&now-broadSince>8&&!matches.Contains(config.topicFilter))
                                {
                                    foreach(string filter in matches.Except(subscribed))await client.SubscribeAsync(new TopicFilterBuilder().WithTopic(filter).WithAtMostOnceQoS().Build());
                                    subscribed=matches;await client.UnsubscribeAsync(config.topicFilter);broad=false;
                                }
                                if(!broad&&now-lastScan>45)
                                {await client.SubscribeAsync(new TopicFilterBuilder().WithTopic(config.topicFilter).WithAtMostOnceQoS().Build());broad=true;broadSince=lastScan=now;}
                            }
                        }
                        finally
                        {
                            try{if(client.IsConnected)await client.DisconnectAsync();}catch(Exception){ }
                        }
                    }
                }
                catch(Exception e) when(!(e is OutOfMemoryException))
                {if(token.IsCancellationRequested)break;diagnostic("MQTT unavailable/auth required or discovery limited at "+endpoint+"; credentials and payloads are not logged.");}
                try{await Task.Delay(15000,token);}catch(OperationCanceledException){break;}
            }
        }
    }
}
