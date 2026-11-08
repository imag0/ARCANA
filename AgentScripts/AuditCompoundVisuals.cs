using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Arcana.Elemental;
public static class AuditCompoundVisuals
{
    public static async Task<string> Run()
    {
        var world=UnityEngine.Object.FindAnyObjectByType<ElementalWorld>();var notebook=UnityEngine.Object.FindAnyObjectByType<GlyphNotebook>();world.ResetArea("all");world.input.SetPaused(false);
        var c=JsonUtility.FromJson<Construction>(Resources.Load<TextAsset>("CompoundStudy").text);c.id=Guid.NewGuid().ToString("N");c.seal.Add(c.seal[0]);notebook.draft=c;
        var motor=world.motor;motor.enabled=false;var cc=motor.GetComponent<CharacterController>();cc.enabled=false;motor.transform.position=new Vector3(-24,.2f,-30);cc.enabled=true;motor.transform.rotation=Quaternion.identity;motor.cameraPivot.localRotation=Quaternion.Euler(-12,0,0);
        if(!world.CloseSeal(c))throw new Exception(world.Message);
        int initial=Time.frameCount;await Task.Delay(12000);
        var defaults=UnityEngine.Object.FindObjectsByType<Renderer>().Where(r=>r.sharedMaterials.Any(m=>m!=null&&m.name=="Default-Material")).Select(r=>r.name).ToArray();
        var systems=UnityEngine.Object.FindObjectsByType<ParticleSystem>();var white=systems.Where(p=>p.main.startColor.color==Color.white).Select(p=>p.name).ToArray();
        var whitePanels=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Image>().Where(p=>p.color==Color.white&&p.sprite==null).Select(p=>p.name).ToArray();
        var result=new {initialFrame=initial,currentFrame=Time.frameCount,step=world.simulationSteps,defaultMaterials=defaults,whiteParticleDefaults=white,whitePanels,particleSystems=systems.Length,liquidSurfaces=UnityEngine.Object.FindObjectsByType<MeshRenderer>().Count(r=>r.name=="Reconstructed liquid volume"&&r.enabled),launched=world.casts.Find(v=>v.id==c.id).launches.FirstOrDefault()?.launched};
        var json=Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented);Directory.CreateDirectory("../../captures/elemental/compound-editor");File.WriteAllText("../../captures/elemental/compound-editor/visual-audit.json",json);return json;
    }
}
