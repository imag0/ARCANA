using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Arcana.Elemental;
public static class PolishPlayground
{
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit mode required");
        Arcana.Editor.PlaygroundBootstrap.Ensure();var world=UnityEngine.Object.FindAnyObjectByType<ElementalWorld>();
        const string folder="Assets/ARCANA/Materials";Directory.CreateDirectory(folder);
        string texturePath=folder+"/ElementalMote.png";var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
        for(int y=0;y<64;y++)for(int x=0;x<64;x++){float r=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;texture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),1.5f)));}
        texture.Apply();File.WriteAllBytes(texturePath,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(texturePath);
        var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
        string materialPath=folder+"/ElementalMote.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(material,materialPath);}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",10);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;world.effectMaterial=material;EditorUtility.SetDirty(material);
        EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);AssetDatabase.SaveAssets();
        var svg=new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1200' height='1080' viewBox='0 0 1200 1080'><rect width='1200' height='1080' fill='#10242b'/><g fill='#f0eadc' font-family='sans-serif'><text x='48' y='64' font-size='38'>ARCANA · GLYPH NOTEBOOK</text><text x='48' y='102' font-size='19'>Choose an element → connect instructions → draw geometry → close the outer seal</text>");
        var runes=Enum.GetValues(typeof(Rune)).Cast<Rune>().Where(r=>r!=Rune.Unknown).ToArray();
        for(int i=0;i<runes.Length;i++){var rune=runes[i];var def=GlyphAlphabet.Get(rune);int x=48+i%3*390,y=150+i/3*145;svg.Append($"<rect x='{x}' y='{y}' width='365' height='126' rx='12' fill='#1d353b'/><text x='{x+105}' y='{y+48}' font-size='23'>{rune.ToString().ToUpperInvariant()}</text>");foreach(var stroke in def.strokes){string pts=string.Join(" ",stroke.Select(p=>(x+50+p.x*40).ToString("F2",CultureInfo.InvariantCulture)+","+(y+64-p.y*40).ToString("F2",CultureInfo.InvariantCulture)));svg.Append($"<polyline points='{pts}' fill='none' stroke='#60d4bd' stroke-width='3' stroke-linecap='round' stroke-linejoin='round'/>");}svg.Append($"<path d='M{x+50} {y+12} v8' stroke='#d8af6a' stroke-width='3'/>");}
        svg.Append("<text x='48' y='1050' font-size='18'>G / F1 notebook · T target · E use · F carry board · X release · F5/F9 save/load</text></g></svg>");File.WriteAllText(Path.GetFullPath("../../GLYPH_ALPHABET.svg"),svg.ToString());
        return "Saved state-derived soft particles and exported actual glyph templates";
    }
}
