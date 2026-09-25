using System;
using System.Collections.Generic;
using System.Linq;

namespace FAA.XPlaneIntegration.Runtime
{
    /// <summary>Per-source warmup and sticky selection; never combines values from different candidates.</summary>
    public sealed class XPlaneSourceSelector
    {
        private sealed class Candidate
        {
            public XPlaneDiscoveredFrame Frame;
            public double First,Last;
            public int Samples;
            public string Signature;
        }
        private readonly Dictionary<string,Candidate> candidates=new(StringComparer.Ordinal);
        public string SelectedId{get;private set;}="";
        private double selectedAt;
        public int Count=>candidates.Count;
        public void Reset(){candidates.Clear();SelectedId="";selectedAt=0;}
        public bool Observe(XPlaneDiscoveredFrame frame)
        {
            if(frame==null||string.IsNullOrEmpty(frame.Id)||frame.Id.Length>512||!XPlaneDiscoveryData.Finite(frame.Received)||!XPlaneDiscoveryData.CoreValid(frame.Values))return false;
            if(!candidates.TryGetValue(frame.Id,out var candidate))
            {
                if(candidates.Count>=32)return false;
                candidate=new Candidate{First=frame.Received};candidates.Add(frame.Id,candidate);
            }
            if(frame.Received<=candidate.Last||frame.Signature!=null&&frame.Signature==candidate.Signature)return false;
            if(frame.Received-candidate.Last>1.25){candidate.First=frame.Received;candidate.Samples=0;}
            candidate.Frame=frame;candidate.Last=frame.Received;candidate.Signature=frame.Signature;candidate.Samples++;return true;
        }
        private static bool Ready(Candidate c,double now)=>c.Samples>=3&&c.Last-c.First>=.4&&now>=c.Last&&now-c.Last<=.9;
        public XPlaneDiscoveredFrame Select(double now,string preferred="")
        {
            foreach(string key in candidates.Where(p=>now-p.Value.Last>60&&p.Key!=SelectedId).Select(p=>p.Key).ToArray())candidates.Remove(key);
            Candidate best=null;
            if(!string.IsNullOrEmpty(preferred))
            {if(candidates.TryGetValue(preferred,out var chosen)&&Ready(chosen,now))best=chosen;}
            else
            {
                best=candidates.Values.Where(c=>Ready(c,now)).OrderByDescending(c=>c.Frame.Priority).ThenBy(c=>c.Frame.Id,StringComparer.Ordinal).FirstOrDefault();
                if(candidates.TryGetValue(SelectedId,out var existing)&&Ready(existing,now)&&
                    (best==null||best.Frame.Priority<=existing.Frame.Priority||now-selectedAt<8))best=existing;
            }
            if(best==null)return null;
            if(SelectedId!=best.Frame.Id){SelectedId=best.Frame.Id;selectedAt=now;}
            return best.Frame;
        }
        public string[] Describe(double now)=>candidates.Values.OrderByDescending(c=>c.Frame.Priority).Take(8)
            .Select(c=>c.Frame.Label+" | "+(Ready(c,now)?"VALIDATED":"warming/stale")+" | "+Math.Max(0,now-c.Last).ToString("F1")+" s").ToArray();
        public string[] ReadyIds(double now)=>candidates.Values.Where(c=>Ready(c,now)).OrderByDescending(c=>c.Frame.Priority).Select(c=>c.Frame.Id).ToArray();
    }
}
