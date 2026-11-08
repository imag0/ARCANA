using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine.InputSystem;
public static class RepairPresentation
{
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode first");
        EditorSceneManager.OpenScene(Arcana.Editor.LabBootstrap.ScenePath);
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/ARCANA/Generated/Stroke unlit.mat");material.shader=Shader.Find("ARCANA/Stroke");material.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(material);
        foreach(var text in Object.FindObjectsByType<TextMeshPro>())
        {
            if(text.text.Contains("rho=50")){text.fontSize=1.4f;var local=text.transform.localPosition;local.y=1.06f;text.transform.localPosition=local;}
            if(text.text.Contains("MECHANICAL FIELD LABORATORY")||text.text=="ARCANA"){text.text="ARCANA";text.fontSize=2.2f;text.transform.position=new Vector3(0,3.8f,4.62f);}
            if(text.text.StartsWith("[E]"))text.fontSize=.85f;
        }
        var hud=Object.FindAnyObjectByType<Arcana.LabHud>();hud.instrument.fontSize=21;
        foreach(var slider in new[]{hud.sensitivity,hud.fov})
        {
            slider.fillRect.sizeDelta=new Vector2(0,-4);slider.fillRect.anchoredPosition=Vector2.zero;slider.handleRect.sizeDelta=new Vector2(18,8);
            var label=(RectTransform)slider.transform.parent.Find(slider.name.Replace(" slider",""));label.anchoredPosition=new Vector2(0,((RectTransform)slider.transform).anchoredPosition.y+30);
        }
        foreach(var panel in new[]{hud.pausePanel,hud.controlsPanel})panel.GetComponent<UnityEngine.UI.Image>().color=Arcana.Palette.Ink;
        hud.controls.enableAutoSizing=true;hud.controls.fontSizeMin=14;hud.controls.fontSizeMax=19;
        hud.diagnostics.enableAutoSizing=true;hud.diagnostics.fontSizeMin=14;hud.diagnostics.fontSizeMax=18;
        var input=Object.FindAnyObjectByType<Arcana.LabInput>();var map=input.actions.FindActionMap("Gameplay");if(map.FindAction("ReleaseSupports")==null)map.AddAction("ReleaseSupports",UnityEngine.InputSystem.InputActionType.Button,"<Keyboard>/u");System.IO.File.WriteAllText(AssetDatabase.GetAssetPath(input.actions),input.actions.ToJson());AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(input.actions));
        var p=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/ARCANA/Generated/WorkshopVolume.asset");
        Get<UnityEngine.Rendering.Universal.DepthOfField>(p).mode.Override(UnityEngine.Rendering.Universal.DepthOfFieldMode.Off);
        Get<UnityEngine.Rendering.Universal.MotionBlur>(p).intensity.Override(0);
        Get<UnityEngine.Rendering.Universal.ChromaticAberration>(p).intensity.Override(0);
        Get<UnityEngine.Rendering.Universal.LensDistortion>(p).intensity.Override(0);
        Get<UnityEngine.Rendering.Universal.FilmGrain>(p).intensity.Override(0);
        Get<UnityEngine.Rendering.Universal.PaniniProjection>(p).distance.Override(0);
        EditorUtility.SetDirty(p);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();return "Stroke vertex colours and lab labels repaired";
    }
    static T Get<T>(UnityEngine.Rendering.VolumeProfile p) where T:UnityEngine.Rendering.VolumeComponent
    {if(p.TryGet<T>(out var component))return component;component=p.Add<T>(true);AssetDatabase.AddObjectToAsset(component,p);return component;}
}

