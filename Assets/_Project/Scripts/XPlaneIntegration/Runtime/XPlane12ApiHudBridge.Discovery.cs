using System;
using UnityEngine;

namespace FAA.XPlaneIntegration.Runtime
{
    public partial class XPlane12ApiHudBridge
    {
        [Header("Automatic source selection")]
        [SerializeField] private bool autoDiscoverSources=true;
        private bool discoveryDriven;
        private string discoveryIdentity="";
        private double discoveryLastReceived=double.NegativeInfinity;
        public bool IsDiscoveryDriven=>discoveryDriven;
        public XPlaneSourceDiscovery SourceDiscovery{get;private set;}

        private void StartConfiguredSource()
        {
            if(!autoDiscoverSources){StartBridge();return;}
            SourceDiscovery=GetComponent<XPlaneSourceDiscovery>()??gameObject.AddComponent<XPlaneSourceDiscovery>();
            SourceDiscovery.Initialize(this);
        }
        public void SetDiscoveryWaiting(string identity)
        {
            if(discoveryDriven&&discoveryIdentity==identity)return;
            StopBridge();ClearDiscoveryState();discoveryDriven=true;discoveryIdentity=identity;
            LastSender=identity;LastError="Waiting for validated source data";LastPacketAgeSeconds=float.PositiveInfinity;
        }
        public void ApplyDiscoveredFrame(XPlaneDiscoveredFrame frame)
        {
            if(frame==null||!XPlaneDiscoveryData.CoreValid(frame.Values)||Time.realtimeSinceStartup<0)return;
            double age=XPlaneDiscoveryData.Now-frame.Received;if(age<0||age>.9)return;
            if(!discoveryDriven||discoveryIdentity!=frame.Id)SetDiscoveryWaiting(frame.Id);
            discoveryLastReceived=frame.Received;
            ApplySnapshotEnvelope(XPlaneDiscoveryData.Envelope(frame,age));
        }
        public void StartExistingFallback(string endpoint)
        {
            if(!XPlaneTerrainConnection.TryNormalize(endpoint,out var normalized))return;
            StopBridge();ClearDiscoveryState();discoveryDriven=false;discoveryIdentity="";
            baseUrl=normalized;transportMode=TransportMode.HttpApi;StartBridge();
        }
        private void UpdateDiscoveredHealth()
        {
            if(!discoveryDriven)return;
            LastPacketAgeSeconds=(float)Math.Max(0,XPlaneDiscoveryData.Now-discoveryLastReceived);
            if(LastPacketAgeSeconds>1){IsFeedHealthy=false;LastError="Selected source is stale; waiting for fresh data or fallback.";}
        }
        private void ClearDiscoveryState()
        {
            _snapshot.Aircraft.Clear();_snapshot.Systems.Clear();_snapshot.Weather.Clear();_snapshot.Traffic.Clear();
            discoveryLastReceived=double.NegativeInfinity;_hasSimWeatherReference=false;
            _hasWeatherRadarPowerState=false;_isWeatherRadarPowered=false;_weatherRadarMode=-1;
            _lastDownloadedWeatherTextureRealtime=_lastStreamWeatherTextureRealtime=-1;
            if(weatherImageTarget!=null)weatherImageTarget.texture=null;
            if(trafficImageTarget!=null)trafficImageTarget.texture=null;
            DestroyTexture(ref _latestWeatherTexture);DestroyTexture(ref _latestTrafficTexture);DestroyTexture(ref _streamWeatherTexture);
        }
    }
}
