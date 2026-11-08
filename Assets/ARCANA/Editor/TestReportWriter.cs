using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Arcana.Editor
{
    [InitializeOnLoad]
    public sealed class TestReportWriter : ICallbacks
    {
        static TestRunnerApi api;
        static TestReportWriter(){EditorApplication.delayCall+=()=>{api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new TestReportWriter());};}
        public static void SetMode(string mode){EditorPrefs.SetString("ARCANA.TestMode",mode);}
        public void RunStarted(ITestAdaptor testsToRun){}
        public void RunFinished(ITestResultAdaptor result)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../reports"));Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,EditorPrefs.GetString("ARCANA.TestMode","UnityTests")+".xml");TestRunnerApi.SaveResultToFile(result,path);Debug.Log($"ARCANA_TEST_REPORT {path}: passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}");
        }
        public void TestStarted(ITestAdaptor test){}
        public void TestFinished(ITestResultAdaptor result){}
    }
}
