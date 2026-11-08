using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Arcana;
using Arcana.Elemental;
public static class FinishPlaygroundLook
{
    static Texture2D Pattern(string name,bool paving)
    {
        string path="Assets/ARCANA/Materials/"+name+".png";var t=new Texture2D(128,128,TextureFormat.RGBA32,true);var random=new System.Random(882);
        for(int y=0;y<128;y++)for(int x=0;x<128;x++){float shade=.94f+(float)random.NextDouble()*.10f;if(paving&&(x%32==0||y%32==0))shade=.78f;if(!paving)shade*=.92f+Mathf.PerlinNoise(x*.09f,y*.09f)*.14f;t.SetPixel(x,y,new Color(shade,shade,shade,1));}
        t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Bilinear;importer.mipmapEnabled=true;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit mode required");Arcana.Editor.PlaygroundBootstrap.Ensure();var world=UnityEngine.Object.FindAnyObjectByType<ElementalWorld>();var root=GameObject.Find("Elemental playground / 120 metres").transform;
        var stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/ARCANA/Generated/Elemental Limestone.mat");var earth=AssetDatabase.LoadAssetAtPath<Material>("Assets/ARCANA/Generated/Elemental Earth.mat");var brass=AssetDatabase.LoadAssetAtPath<Material>("Assets/ARCANA/Generated/Elemental Brass.mat");var timber=AssetDatabase.LoadAssetAtPath<Material>("Assets/ARCANA/Generated/Elemental Timber.mat");
        stone.SetTexture("_BaseMap",Pattern("Limestone paving",true));earth.SetTexture("_BaseMap",Pattern("Garden grain",false));EditorUtility.SetDirty(stone);EditorUtility.SetDirty(earth);
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())if(renderer.sharedMaterial==stone||renderer.sharedMaterial==earth){var axes=new[]{Mathf.Abs(renderer.transform.lossyScale.x),Mathf.Abs(renderer.transform.lossyScale.y),Mathf.Abs(renderer.transform.lossyScale.z)}.OrderByDescending(x=>x).ToArray();var material=new Material(renderer.sharedMaterial);material.name=renderer.name+" / world scale";material.mainTextureScale=new Vector2(Mathf.Max(.5f,axes[0]/4),Mathf.Max(.5f,axes[1]/4));string folder="Assets/ARCANA/Generated/Surface Instances";Directory.CreateDirectory(folder);string path=folder+"/"+renderer.gameObject.GetEntityId().GetHashCode()+".mat";AssetDatabase.CreateAsset(material,path);renderer.sharedMaterial=material;}
        void Detail(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.layer=2;}
        foreach(var fixture in root.GetComponentsInChildren<PlaygroundFixture>().Where(f=>f.portable))if(fixture.transform.Find("Brass board frame top")==null)
        {Detail("Brass board frame top",fixture.transform,new Vector3(0,.49f,0),new Vector3(1.04f,.025f,1.15f),brass);Detail("Brass board frame bottom",fixture.transform,new Vector3(0,-.49f,0),new Vector3(1.04f,.025f,1.15f),brass);foreach(float x in new[]{-.49f,.49f})Detail("Brass board frame side",fixture.transform,new Vector3(x,0,0),new Vector3(.025f,1.04f,1.15f),brass);}
        foreach(var pillar in root.Cast<Transform>().Where(t=>t.name=="Garden pillar").ToArray())if(pillar.Find("Pillar cap")==null){Detail("Pillar cap",pillar,new Vector3(0,.49f,0),new Vector3(1.25f,.04f,1.25f),brass);Detail("Pillar base",pillar,new Vector3(0,-.47f,0),new Vector3(1.3f,.08f,1.3f),stone);}
        if(root.Find("Courtyard perimeter inlay")==null)
        {var group=new GameObject("Courtyard perimeter inlay").transform;group.SetParent(root,false);foreach(float x in new[]{-30.7f,-9.3f})Detail("Brass paving edge",group,new Vector3(x,.056f,-13),new Vector3(.06f,.008f,17.4f),brass);foreach(float z in new[]{-21.7f,-4.3f})Detail("Brass paving edge",group,new Vector3(-20,.056f,z),new Vector3(21.4f,.008f,.06f),brass);}
        string profilePath="Assets/ARCANA/Generated/ElementalVolume.asset";var volume=UnityEngine.Object.FindAnyObjectByType<Volume>();if(AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath)==null)AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(volume.sharedProfile),profilePath);volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);var profile=volume.sharedProfile;
        if(profile.TryGet<Bloom>(out var bloom)){bloom.intensity.Override(.28f);bloom.threshold.Override(1.1f);EditorUtility.SetDirty(bloom);}if(profile.TryGet<ColorAdjustments>(out var color)){color.saturation.Override(8);color.contrast.Override(7);EditorUtility.SetDirty(color);}if(profile.TryGet<Vignette>(out var vignette)){vignette.intensity.Override(.16f);EditorUtility.SetDirty(vignette);}EditorUtility.SetDirty(profile);
        if(world.effectMaterial!=null){world.effectMaterial.SetColor("_BaseColor",new Color(2,2,2,1));EditorUtility.SetDirty(world.effectMaterial);}
        world.motor.view.GetUniversalAdditionalCameraData().renderPostProcessing=true;world.motor.view.allowHDR=true;
        EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);AssetDatabase.SaveAssets();return "Saved material grain, architectural trim, framed boards and dedicated elemental color volume";
    }
}
