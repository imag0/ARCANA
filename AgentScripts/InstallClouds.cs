using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public static class InstallClouds
{
    public static string Run()
    {
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/VolumetricClouds/VolumetricClouds.mat");
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/ARCANA/Generated/ElementalVolume.asset");
        if(renderer==null||material==null||profile==null)throw new System.Exception("Cloud renderer, material or playground profile missing");
        var feature=renderer.rendererFeatures.Find(f=>f is VolumetricCloudsURP) as VolumetricCloudsURP;
        if(feature==null){feature=ScriptableObject.CreateInstance<VolumetricCloudsURP>();feature.name="Volumetric Clouds URP";AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);}
        feature.CloudsMaterial=material;feature.SetActive(true);feature.Create();EditorUtility.SetDirty(feature);EditorUtility.SetDirty(renderer);
        if(!profile.TryGet<VolumetricClouds>(out var clouds)){clouds=profile.Add<VolumetricClouds>(true);AssetDatabase.AddObjectToAsset(clouds,profile);}
        clouds.state.overrideState=true;clouds.state.value=true;EditorUtility.SetDirty(clouds);EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();return "Cloud feature and playground volume enabled";
    }
}
