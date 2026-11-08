using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class AuditPresentation
{
    [Serializable] public sealed class Result
    {
        public int renderers, textElements, panelImages, particleSystems;
        public string status;
        public List<string> issues = new List<string>();
    }
    public static string Run()
    {
        var result = new Result();
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            result.renderers++;
            foreach (var material in renderer.sharedMaterials)
                if (material == null || material.name == "Default-Material" || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                    result.issues.Add("Missing/default/error material: " + renderer.name);
        }
        foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            result.textElements++;
            if (text.font == null || (text.color == Color.white && Math.Abs(text.fontSize - 36) < .01))
                result.issues.Add("Missing or unstyled font: " + text.name);
        }
        foreach (var panel in UnityEngine.Object.FindObjectsByType<Image>(FindObjectsInactive.Include))
        {
            result.panelImages++;
            if (Gray(panel.color)) result.issues.Add("Untinted grayscale UI: " + panel.name);
        }
        foreach (var particles in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include))
        {
            result.particleSystems++;
            if (Gray(particles.main.startColor.color)) result.issues.Add("Untinted grayscale particles: " + particles.name);
        }
        result.status = result.issues.Count == 0 ? "PASSED" : "FAILED";
        string json = JsonUtility.ToJson(result, true);
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../../reports/presentation-audit.json")), json);
        return json;
    }
    static bool Gray(Color c) => Math.Abs(c.r-c.g)<.025 && Math.Abs(c.g-c.b)<.025;
}
