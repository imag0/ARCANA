using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Arcana.Elemental;
public static class InspectFrontAssembly
{
    public static async Task<string> Run()
    {
        var w=UnityEngine.Object.FindAnyObjectByType<ElementalWorld>();w.ResetArea("all");w.input.SendMessage("OnApplicationFocus",true);w.input.SetPaused(false);w.motor.enabled=false;
        var cc=w.motor.GetComponent<CharacterController>();cc.enabled=false;w.motor.transform.position=new Vector3(-24,1,-33);cc.enabled=true;w.motor.view.transform.rotation=Quaternion.LookRotation(new Vector3(-24,8,-20)-w.motor.view.transform.position);
        var a=new GameObject("Inspection fixture").AddComponent<ElementalAcceptance>();var c=(Construction)typeof(ElementalAcceptance).GetMethod("CompoundGraph",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(a,new object[]{1f,null});c.nodes.First(n=>n.rune==Rune.Launch).width=.5f;
        if(!w.CloseSeal(c))throw new Exception(w.Message);await Task.Delay(16000);w.input.SetPaused(true);
        var cast=w.casts.First();var state=cast.launches.First();
        var data=new{cast.elapsed,cast.active,cast.status,state,channels=cast.channels.Select(ch=>new{ch.element,ch.amount,ch.emitted,ch.frontDepth,ch.frontDown,ch.anchor}),packets=w.guided.Select(g=>{var b=w.bodies.Find(v=>v.id==g.bodyId);var ch=cast.channels[g.channel];var target=g.heldPoint+state.offset;return new{ch.element,g.formStage,g.heldPoint,b.Body.position,b.Body.linearVelocity,error=Vector3.Distance(b.Body.position,target),g.entry};}).OrderByDescending(p=>p.error).Take(15)};
        var json=Newtonsoft.Json.JsonConvert.SerializeObject(data,new Newtonsoft.Json.JsonSerializerSettings{ReferenceLoopHandling=Newtonsoft.Json.ReferenceLoopHandling.Ignore});System.IO.File.WriteAllText("../../reports/front-assembly-debug.json",json);return json;
    }
}
