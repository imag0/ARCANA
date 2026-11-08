using System.IO;
using UnityEngine;
public static class PreserveElemental
{
    public static string Run()
    {
        var notebook=Object.FindAnyObjectByType<Arcana.Elemental.GlyphNotebook>();
        if(notebook==null)return "No live elemental session";
        string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../logs/elemental-before-repair.json"));
        File.WriteAllText(path,JsonUtility.ToJson(notebook.world.Capture(notebook.draft),true));
        return path;
    }
}
