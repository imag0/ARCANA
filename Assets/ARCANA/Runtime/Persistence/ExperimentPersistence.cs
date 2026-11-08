using System;
using System.IO;
using UnityEngine;
using Arcana.Mechanics;

namespace Arcana
{
    public sealed class ExperimentPersistence : MonoBehaviour
    {
        public ExperimentState experiment;public ProbeInstrument probe;
        [NonSerialized] public string pathOverride;
        public string SavePath=>string.IsNullOrEmpty(pathOverride)?Path.Combine(Application.persistentDataPath,"experiment-v1.json"):pathOverride;
        public Snapshot Capture()=>new Snapshot{geometry=experiment.Geometry.Copy(),ledger=experiment.Ledger.Copy(),probePosition=probe.Position,probeAxis=probe.Axis,probeLength=probe.length,probeRadius=probe.radius,preload=experiment.preload};
        public string Serialize(){var s=Capture();s.Validate();return JsonUtility.ToJson(s,true);}
        public void Restore(string json)
        {
            if(string.IsNullOrWhiteSpace(json)||json.Length>2000000||!json.TrimStart().StartsWith("{"))throw new ArgumentException("Invalid experiment JSON");
            foreach(var key in new[]{"version","geometry","ledger","probePosition","probeAxis","probeLength","probeRadius","preload"})if(!json.Contains("\""+key+"\""))throw new ArgumentException("Missing snapshot field: "+key);
            var s=JsonUtility.FromJson<Snapshot>(json);if(s==null)throw new ArgumentException("Empty snapshot");s.Validate();
            // All validation happens before committing any live state.
            experiment.LoadSnapshot(s);probe.Restore(s.probePosition,s.probeAxis,s.probeLength,s.probeRadius);
        }
        public bool Save()
        {
            try{Directory.CreateDirectory(Path.GetDirectoryName(SavePath));string tmp=SavePath+".tmp";File.WriteAllText(tmp,Serialize());if(File.Exists(SavePath))File.Replace(tmp,SavePath,SavePath+".bak");else File.Move(tmp,SavePath);experiment.Message("Experiment saved: "+SavePath);return true;}catch(Exception e){experiment.Message("Save failed: "+e.Message);return false;}
        }
        public bool Load(){try{Restore(File.ReadAllText(SavePath));return true;}catch(Exception e){experiment.Message("Load rejected; current experiment preserved: "+e.Message);return false;}}
    }
}
