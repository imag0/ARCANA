using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using Arcana.Elemental;
public static class FinalizePlayground
{
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Edit mode required");
        Arcana.Editor.PlaygroundBootstrap.Ensure();var world=Object.FindAnyObjectByType<ElementalWorld>();world.ledger=new ChargeLedger();
        var source=Object.FindObjectsByType<MaterialSource>().First(s=>s.stableId=="courtyard-water");source.outlet.localPosition=new Vector3(0,2.7f,0);
        if(source.transform.Find("Drawing outlet riser")==null){var pipe=GameObject.CreatePrimitive(PrimitiveType.Cylinder);pipe.name="Drawing outlet riser";pipe.transform.SetParent(source.transform,false);pipe.transform.localPosition=new Vector3(0,1.3f,0);pipe.transform.localScale=new Vector3(.12f,1.3f,.12f);pipe.GetComponent<Renderer>().sharedMaterial=world.bodyMaterial;}
        EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);return "Saved playground ledger and raised finite outlet";
    }
}
