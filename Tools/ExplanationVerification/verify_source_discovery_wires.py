"""Real loopback protocol fixtures for C# native Web/UDP/MQTT clients. Never supplies the live HUD."""
import base64
import hashlib
import http.server
import json
from pathlib import Path
import socket
import socketserver
import struct
import subprocess
import threading
import time
from urllib.parse import parse_qs,urlparse

ROOT=Path(__file__).resolve().parents[2]
KEY='FAA.SourceDiscovery.WireFixture'
P='sim/flightmodel/position/'
CORE={P+'latitude':33.,P+'longitude':-83.,P+'elevation':1200.,P+'theta':2.,P+'phi':3.,P+'psi':90.,P+'indicated_airspeed':100.,P+'groundspeed':45.,P+'vh_ind':1.,'sim/version/xplane_internal_version':120400.}
CLOCK='sim/time/total_running_time_sec'
VALUES={**CORE,CLOCK:100.}
commands=[]
stop=threading.Event()

def evaluate(code):
    p=subprocess.run(['unity','command','eval',code,'--format','json'],cwd=ROOT,text=True,capture_output=True,timeout=20)
    e=json.loads(p.stdout)
    if not e.get('success') or not e['data']['result'].get('success'):raise RuntimeError(e)
    return e['data']['result']['result']

def mqtt_packet(kind,payload):
    n=len(payload);length=bytearray()
    while True:
        b=n%128;n//=128;length.append(b|(128 if n else 0))
        if not n:break
    return bytes([kind])+bytes(length)+payload

def read_exact(s,n):
    data=bytearray()
    while len(data)<n:
        p=s.recv(n-len(data))
        if not p:raise EOFError
        data.extend(p)
    return bytes(data)

class MqttHandler(socketserver.BaseRequestHandler):
    def handle(self):
        self.request.settimeout(.03);filters=set();sent=0;started=time.monotonic();next_send=0
        try:
            while not stop.is_set():
                try:
                    first=self.request.recv(1)
                    if not first:break
                    n=0;mult=1
                    while True:
                        b=read_exact(self.request,1)[0];n+=(b&127)*mult;mult*=128
                        if not b&128:break
                    data=read_exact(self.request,n);kind=first[0]>>4;commands.append(('mqtt',kind))
                    if kind==1:self.request.sendall(b'\x20\x02\x00\x00')
                    elif kind==8:
                        index=2;added=[]
                        while index<len(data):
                            size=struct.unpack('!H',data[index:index+2])[0];index+=2;topic=data[index:index+size].decode();index+=size+1;filters.add(topic);added.append(topic)
                        self.request.sendall(mqtt_packet(0x90,data[:2]+bytes(len(added))))
                        # Deliberately plausible RETAINED telemetry must never qualify as live.
                        raw=json.dumps({'raw':VALUES}).encode();topic=b'unrelated/retained';self.request.sendall(mqtt_packet(0x31,struct.pack('!H',len(topic))+topic+raw))
                    elif kind==10:
                        index=2
                        while index<len(data):
                            size=struct.unpack('!H',data[index:index+2])[0];index+=2;filters.discard(data[index:index+size].decode());index+=size
                        self.request.sendall(mqtt_packet(0xB0,data[:2]))
                    elif kind==12:self.request.sendall(b'\xd0\x00')
                    elif kind==14:break
                    else:raise AssertionError('Unexpected MQTT write/control: '+str(kind))
                except socket.timeout:pass
                now=time.monotonic()
                if filters and now>=next_send:
                    next_send=now+.1;sent+=1
                    raw={**VALUES,CLOCK:100+sent*.1}
                    topic=b'lab-rig/aircraft-A/flight-snapshot'
                    payload=json.dumps({'raw':raw},separators=(',',':')).encode()
                    self.request.sendall(mqtt_packet(0x30,struct.pack('!H',len(topic))+topic+payload))
        except (OSError,EOFError):pass

class Server(socketserver.ThreadingTCPServer):
    allow_reuse_address=True
    daemon_threads=True

class WebHandler(http.server.BaseHTTPRequestHandler):
    protocol_version='HTTP/1.1'
    def log_message(self,*args):pass
    def do_GET(self):
        url=urlparse(self.path)
        if url.path=='/api/v1/datarefs':
            name=parse_qs(url.query).get('filter[name]',[''])[0]
            commands.append(('http','GET'))
            if name not in VALUES:
                self.send_response(404);self.send_header('Content-Length','0');self.end_headers();return
            index=list(VALUES).index(name)+1
            raw=json.dumps({'data':[{'id':index,'name':name,'value_type':'float'}]}).encode()
            self.send_response(200);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(raw)));self.end_headers();self.wfile.write(raw);return
        if url.path!='/api/v1' or self.headers.get('Upgrade','').lower()!='websocket':
            self.send_response(404);self.send_header('Content-Length','0');self.end_headers();return
        key=self.headers['Sec-WebSocket-Key'];accept=base64.b64encode(hashlib.sha1((key+'258EAFA5-E914-47DA-95CA-C5AB0DC85B11').encode()).digest()).decode()
        self.send_response(101);self.send_header('Upgrade','websocket');self.send_header('Connection','Upgrade');self.send_header('Sec-WebSocket-Accept',accept);self.end_headers()
        try:
            self.connection.settimeout(3)
            h=read_exact(self.connection,2);size=h[1]&127
            if size==126:size=struct.unpack('!H',read_exact(self.connection,2))[0]
            mask=read_exact(self.connection,4) if h[1]&128 else bytes(4)
            data=read_exact(self.connection,size);message=json.loads(bytes(b^mask[i%4] for i,b in enumerate(data)))
            commands.append(('ws',message['type']));assert message['type']=='dataref_subscribe_values'
            mapping={str(i+1):v for i,v in enumerate(VALUES.values())};clock_id=str(list(VALUES).index(CLOCK)+1)
            def send(value):
                b=json.dumps(value,separators=(',',':')).encode();prefix=bytes([0x81,len(b)]) if len(b)<126 else b'\x81\x7e'+struct.pack('!H',len(b));self.connection.sendall(prefix+b)
            send({'type':'result','req_id':1,'success':True})
            send({'type':'dataref_update_values','data':mapping})
            i=0
            while not stop.wait(.1):
                i+=1;send({'type':'dataref_update_values','data':{clock_id:100+i*.1}})
        except (OSError,EOFError):pass
        self.close_connection=True

class UdpFixture:
    def __init__(self):
        self.socket=socket.socket(socket.AF_INET,socket.SOCK_DGRAM);self.socket.bind(('127.0.0.1',0));self.port=self.socket.getsockname()[1];self.socket.settimeout(.02)
        self.clients={};self.unsubscribed=0;self.lock=threading.Lock()
        threading.Thread(target=self.loop,daemon=True).start()
    def loop(self):
        next_send=0;sequence=0
        while not stop.is_set():
            try:
                data,address=self.socket.recvfrom(65535)
                assert len(data)==413 and data[:5]==b'RREF\0'
                hz,index=struct.unpack('<ii',data[5:13]);path=data[13:].split(b'\0',1)[0].decode()
                commands.append(('udp','RREF'))
                with self.lock:
                    fields=self.clients.setdefault(address,{})
                    if hz==0:fields.pop(index,None);self.unsubscribed+=1
                    elif path in VALUES:fields[index]=path
            except socket.timeout:pass
            except OSError:break
            now=time.monotonic()
            if now>=next_send:
                next_send=now+.1;sequence+=1
                with self.lock:
                    for address,fields in list(self.clients.items()):
                        if fields:
                            data=b'RREF\0'+b''.join(struct.pack('<if',index,100+sequence*.1 if path==CLOCK else VALUES[path]) for index,path in fields.items())
                            self.socket.sendto(data,address)

def main():
    mqtt=Server(('127.0.0.1',0),MqttHandler);web=http.server.ThreadingHTTPServer(('127.0.0.1',0),WebHandler);udp=UdpFixture();started=False
    for server in [mqtt,web]:threading.Thread(target=server.serve_forever,daemon=True).start()
    try:
        code='if(AppDomain.CurrentDomain.GetData("'+KEY+'")!=null)throw new Exception("Existing wire test");var cancel=new System.Threading.CancellationTokenSource();var rows=new System.Collections.Concurrent.ConcurrentDictionary<string,FAA.XPlaneIntegration.Runtime.XPlaneDiscoveredFrame>();var errors=new System.Collections.Concurrent.ConcurrentQueue<string>();Action<FAA.XPlaneIntegration.Runtime.XPlaneDiscoveredFrame> publish=f=>rows[f.Id]=f;Action<string> log=s=>errors.Enqueue(s);'
        code+=f'var a=System.Threading.Tasks.Task.Run(()=>FAA.XPlaneIntegration.Runtime.XPlaneDiscoveryUdp.RunRref(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,{udp.port}),publish,log,cancel.Token));'
        code+=f'var b=System.Threading.Tasks.Task.Run(()=>FAA.XPlaneIntegration.Runtime.XPlaneDiscoveryWeb.Run("http://127.0.0.1:{web.server_port}",publish,log,cancel.Token));'
        code+=f'var c=System.Threading.Tasks.Task.Run(()=>FAA.XPlaneIntegration.Runtime.XPlaneDiscoveryMqtt.Run(new FAA.XPlaneIntegration.Runtime.XPlaneMqttDiscoveryConfig{{host="127.0.0.1",port={mqtt.server_address[1]}}},publish,log,cancel.Token));'
        code+='AppDomain.CurrentDomain.SetData("'+KEY+'",new object[]{cancel,rows,errors,new[]{a,b,c}});return "Started isolated read-only wire fixtures";'
        print(evaluate(code));started=True
        deadline=time.monotonic()+25;rows=[]
        while time.monotonic()<deadline:
            state=evaluate('var ctx=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");var rows=(System.Collections.Concurrent.ConcurrentDictionary<string,FAA.XPlaneIntegration.Runtime.XPlaneDiscoveredFrame>)ctx[1];return new{rows=rows.Values.Select(f=>new{f.Id,f.Transport,f.Topic,fields=f.Values.Count,latitude=f.Values["'+P+'latitude"]}).ToArray(),errors=((System.Collections.Concurrent.ConcurrentQueue<string>)ctx[2]).ToArray()};')
            rows=state['rows']
            if len(rows)>=3:break
            time.sleep(.3)
        assert len(rows)==3,state
        assert {r['Transport'] for r in rows}=={'UDP RREF','Native Web API','MQTT'},rows
        assert all(r['latitude']==33 for r in rows)
        assert next(r for r in rows if r['Transport']=='MQTT')['Topic']=='lab-rig/aircraft-A/flight-snapshot'
        # Let the bounded discovery window expire and verify wildcard unsubscribe on the wire.
        deadline=time.monotonic()+10
        while ('mqtt',10) not in commands and time.monotonic()<deadline:time.sleep(.2)
        assert ('mqtt',10) in commands,'MQTT wildcard was not narrowed after discovery'
        evaluate('var ctx=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");((System.Threading.CancellationTokenSource)ctx[0]).Cancel();return "Cancellation requested";')
        time.sleep(.5)
        completion=evaluate('var ctx=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");return ((System.Threading.Tasks.Task[])ctx[3]).Select(t=>new{t.IsCompleted,t.IsFaulted}).ToArray();')
        assert all(t['IsCompleted'] and not t['IsFaulted'] for t in completion),completion
        assert udp.unsubscribed>0,'RREF subscriptions were not removed'
        assert all(kind in {'RREF','GET','dataref_subscribe_values',1,8,10,12,14} for _,kind in commands),commands
        report={'passed':True,'fixture_only':True,'live_hud_modified':False,'rows':rows,'workers_completed':completion,'udp_unsubscribed':udp.unsubscribed,'operations':sorted({str(x) for x in commands})}
        path=ROOT/'artifacts/vsi-reference-readme/source-wire-tests.json';path.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
    finally:
        if started:
            evaluate('var c=(object[])AppDomain.CurrentDomain.GetData("'+KEY+'");((System.Threading.CancellationTokenSource)c[0]).Cancel();AppDomain.CurrentDomain.SetData("'+KEY+'",null);return "Wire fixtures released";')
        stop.set();mqtt.shutdown();web.shutdown();mqtt.server_close();web.server_close();udp.socket.close()

if __name__=='__main__':main()
