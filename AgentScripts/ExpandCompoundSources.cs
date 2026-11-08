using System.Linq;
using Arcana.Elemental;
using TMPro;
using UnityEngine;
using UnityEditor.SceneManagement;
public static class ExpandCompoundSources
{
    public static string Run()
    {
        Arcana.Editor.PlaygroundBootstrap.Ensure();
        foreach(var item in new[]{("bulk-water","BULK WATER / 20,000 kg","courtyard-water",20000.0,new Vector3(-36,.1f,-24)),("bulk-stone","BULK STONE / 40,000 kg","courtyard-stone",40000.0,new Vector3(-36,.1f,-13))})
        {
            if(Object.FindObjectsByType<MaterialSource>().Any(s=>s.stableId==item.Item1))continue;
            var original=Object.FindObjectsByType<MaterialSource>().First(s=>s.stableId==item.Item3);
            var copy=Object.Instantiate(original.gameObject,original.transform.parent);copy.name=item.Item2;copy.transform.position=item.Item5;copy.transform.localScale=Vector3.one*2;
            var source=copy.GetComponent<MaterialSource>();source.stableId=item.Item1;source.label=item.Item2;source.initialKg=item.Item4;source.introducedVersion=2;source.area="courtyard";source.lots.Clear();
            var label=new GameObject("Finite bulk source label").AddComponent<TextMeshPro>();label.font=Object.FindAnyObjectByType<TextMeshPro>().font;label.text=item.Item2+"\nChoose source on the element glyph";label.fontSize=1.5f;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(10,3);label.transform.SetParent(copy.transform,false);label.transform.localPosition=new Vector3(0,2.2f,0);label.transform.localScale=Vector3.one*.5f;
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();return "Added two finite bulk sources";
    }
}
