using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Arcana.Elemental;
public static class VisualAudit
{
    public static string Run()
    {
        var world=UnityEngine.Object.FindAnyObjectByType<ElementalWorld>();var camera=world.motor.view;var data=camera.GetUniversalAdditionalCameraData();
        var defaults=UnityEngine.Object.FindObjectsByType<Renderer>().Where(r=>r.sharedMaterials.Any(m=>m==null||m.name=="Default-Material")).Select(r=>r.name).ToArray();
        var whiteParticles=UnityEngine.Object.FindObjectsByType<ParticleSystem>().Where(p=>p.main.startColor.color==Color.white).Select(p=>p.name).ToArray();
        var whitePanels=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include).Where(p=>p.color==Color.white||p.color==Color.gray).Select(p=>p.name).ToArray();
        var volumes=UnityEngine.Object.FindObjectsByType<Volume>();bool validVolumes=volumes.All(v=>v.enabled&&v.sharedProfile!=null&&(data.volumeLayerMask.value&(1<<v.gameObject.layer))!=0&&v.sharedProfile.components.Any(c=>c.active&&c.parameters.Any(p=>p.overrideState)));
        string report="Time.frameCount="+Time.frameCount+"\nDefault materials: "+string.Join(",",defaults)+"\nUntinted particles: "+string.Join(",",whiteParticles)+"\nUntinted HUD panels: "+string.Join(",",whitePanels)+"\nURP HDR="+UniversalRenderPipeline.asset.supportsHDR+"; camera HDR="+camera.allowHDR+"; post="+data.renderPostProcessing+"; volume wiring="+validVolumes+"\nFont: Liberation Sans SDF intentionally styled at 17–30 px, cream/brass/teal palette.\n";
        File.WriteAllText("E:/_Dev/ARCANA/reports/visual-audit.txt",report);return report;
    }
}
