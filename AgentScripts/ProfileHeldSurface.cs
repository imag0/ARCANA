using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Diagnostics;
using UnityEngine;
using Arcana.Elemental;
public static class ProfileHeldSurface
{
    public static async Task<string> Run(string output="held-surface-optimized.json")
    {
        var w=UnityEngine.Object.FindAnyObjectByType<ElementalWorld>();w.ResetArea("all");w.input.SendMessage("OnApplicationFocus",true);w.input.SetPaused(false);
        var c=JsonUtility.FromJson<Construction>(Resources.Load<TextAsset>("CompoundStudy").text);c.seal.Add(c.seal[0]);c.spellSize=1;
        // Keep the compound suspended in open space for a repeatable renderer sample.
        foreach(var n in c.nodes.Where(n=>n.rune==Rune.Launch))n.rune=Rune.Assemble;
        UnityEngine.Object.FindAnyObjectByType<GlyphNotebook>().draft=c;
        if(!w.CloseSeal(c))throw new Exception(w.Message);await Task.Delay(14000);w.input.SetPaused(true);
        var surface=UnityEngine.Object.FindAnyObjectByType<FluidSurface>();var type=surface.GetType();var update=type.GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);var next=type.GetField("nextUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        double[] Sample(){var result=new double[12];for(int i=0;i<result.Length;i++){next?.SetValue(surface,-1f);var watch=Stopwatch.StartNew();update.Invoke(surface,null);result[i]=watch.Elapsed.TotalMilliseconds;}return result;}
        if(w.guided.Count<80||surface.rebuildCount==0)throw new Exception("Profile did not assemble enough physical cells");var evidence=new{bodies=w.bodies.Count,guides=w.guided.Count,elapsed=w.casts[0].elapsed,surface.rebuildCount,surface.cachedFrames,surface.lastBuildMs};var held=Sample();w.Cancel();var released=Sample();var json=Newtonsoft.Json.JsonConvert.SerializeObject(new{evidence,heldMs=held,releasedMs=released,heldMedian=held.OrderBy(v=>v).ElementAt(6),releasedMedian=released.OrderBy(v=>v).ElementAt(6)},Newtonsoft.Json.Formatting.Indented);
        System.IO.Directory.CreateDirectory("../../reports");System.IO.File.WriteAllText("../../reports/"+output,json);return json;
    }
}
