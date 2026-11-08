using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Arcana.Core;

namespace Arcana
{
    // Runs only with an explicit -arcanaAcceptance output-directory launch argument.
    // Uses the real player, real CharacterController, input actions, camera raycasts and solver.
    // Captures are framebuffer screenshots. The user-facing game never auto-plays.
    public sealed class PlayerAcceptance : MonoBehaviour
    {
        string output;LabInput input;FirstPersonMotor motor;ExperimentState experiment;DrawingTool tool;ProbeInstrument probe;ExperimentPersistence persistence;
        Keyboard keyboard;Mouse mouse;readonly Receipt receipt=new Receipt();readonly List<float> frameTimes=new List<float>();
        [Serializable] public sealed class Receipt
        {
            public string editor,graphics,device,status;public int width,height,strokes,segments,frames;public double passive,activated,restored,wBenchmark,reserve,stored,released,ledgerResidual,averageFps,p95FrameMs,recomputeMs;public List<string> checks=new List<string>();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-arcanaAcceptance");if(index<0)index=Array.IndexOf(args,"-arcanaAnnexAcceptance");if(index<0||index==args.Length-1)return;
            var go=new GameObject("Explicit player acceptance run");go.AddComponent<PlayerAcceptance>().output=Path.GetFullPath(args[index+1]);
        }
        void Start(){Directory.CreateDirectory(output);StartCoroutine(Guard(Run()));}
        IEnumerator Guard(IEnumerator run)
        {
            while(true)
            {
                object current;
                try{if(!run.MoveNext())break;current=run.Current;}
                catch(Exception e){receipt.status="FAILED: "+e;WriteReceipt();Debug.LogError(receipt.status);Application.Quit(1);yield break;}
                yield return current;
            }
        }
        void Check(bool condition,string label){if(!condition)throw new InvalidOperationException(label);receipt.checks.Add(label);Debug.Log("ARCANA_ACCEPTANCE "+label);}
        void WriteReceipt(){File.WriteAllText(Path.Combine(output,"player-acceptance.json"),JsonUtility.ToJson(receipt,true));}
        IEnumerator WaitMeasurement()
        {float end=Time.realtimeSinceStartup+15;while(probe.Pending&&Time.realtimeSinceStartup<end)yield return null;}
        IEnumerator Capture(string name){yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;yield return null;}
        IEnumerator Tap(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
        void AimAt(Vector3 target)
        {
            Vector3 d=(target-motor.view.transform.position).normalized;float yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;float pitch=-Mathf.Asin(d.y)*Mathf.Rad2Deg;
            motor.transform.rotation=Quaternion.Euler(0,yaw,0);motor.cameraPivot.localRotation=Quaternion.Euler(pitch,0,0);
        }
        IEnumerator Draw(List<D3> points)
        {
            var surface=experiment.Surface("paper");AimAt(surface.World(points[0]));yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return null;
            foreach(var point in points){AimAt(surface.World(point));yield return null;}
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;yield return null;
        }
        static List<D3> SamplePath(params D3[] corners)
        {
            var points=new List<D3>();points.Add(corners[0]);for(int i=1;i<corners.Length;i++){int steps=Math.Max(1,(int)Math.Ceiling(D3.Distance(corners[i-1],corners[i])/.06));for(int j=1;j<=steps;j++)points.Add(D3.Lerp(corners[i-1],corners[i],j/(double)steps));}return points;
        }
        IEnumerator Run()
        {
            bool annex=Environment.GetCommandLineArgs().Contains("-arcanaAnnexAcceptance");if(!annex&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="ArcanaLab")yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("ArcanaLab");
            yield return new WaitForSecondsRealtime(1);if(annex)FindAnyObjectByType<Arcana.Elemental.GlyphNotebook>().SetAnnex(true);
            input=FindAnyObjectByType<LabInput>();motor=FindAnyObjectByType<FirstPersonMotor>();experiment=FindAnyObjectByType<ExperimentState>();tool=FindAnyObjectByType<DrawingTool>();probe=FindAnyObjectByType<ProbeInstrument>();persistence=FindAnyObjectByType<ExperimentPersistence>();
            persistence.pathOverride=Path.Combine(output,"acceptance-experiment.json");
            receipt.editor=Application.unityVersion;receipt.graphics=SystemInfo.graphicsDeviceType.ToString();receipt.device=SystemInfo.graphicsDeviceName;receipt.width=Screen.width;receipt.height=Screen.height;
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();
            input.SetPaused(false);var start=motor.transform.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSeconds(.4f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Check(motor.transform.position.z>start.z+.5f,"Actual W input moves the player");
            input.SetPaused(true);start=motor.transform.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSecondsRealtime(.15f);Check(Vector3.Distance(start,motor.transform.position)<.01f,"Pause excludes gameplay input");InputSystem.QueueStateEvent(keyboard,new KeyboardState());input.SetPaused(false);
            // Fixed viewpoint for repeatable camera-raycast sampling, after independently testing input motion.
            motor.enabled=false;var cc=motor.GetComponent<CharacterController>();cc.enabled=false;motor.transform.position=new Vector3(0,.16f,1.6f)+(annex?FindAnyObjectByType<Arcana.Elemental.ElementalWorld>().annexRoot.position:Vector3.zero);cc.enabled=true;motor.cameraPivot.localPosition=Vector3.up*1.64f;Physics.SyncTransforms();
            yield return Draw(SamplePath(new D3(-.4,-.28,0),new D3(.4,-.28,0)));
            probe.Place(experiment.Surface("paper"),default);yield return WaitMeasurement();
            Check(!probe.Pending&&probe.Reading.available&&Math.Abs(probe.Reading.shift)<1e-12,"Camera raycast passive drawing reads zero");receipt.passive=probe.Reading.shift;AimAt(experiment.Surface("paper").World(default));yield return Capture("01-passive-drawing");
            // Separate final stroke closes actual connectivity; no symbol recognition or demo fixture shortcut.
            yield return Draw(SamplePath(new D3(.4,-.28,0),new D3(.4,.28,0),new D3(-.4,.28,0),new D3(-.4,-.28,0)));
            yield return WaitMeasurement();Check(experiment.Geometry.supports.Count==2&&experiment.Ledger.stored>0,"Separate closing stroke installs two supports and debits work");
            Check(!probe.Pending&&probe.Reading.available&&Math.Abs(probe.Reading.shift)>1e-8,"Activated freehand polygon produces calculated nonzero signal");receipt.activated=probe.Reading.shift;AimAt(experiment.Surface("paper").World(default));yield return Capture("02-activated-loop");
            Check(persistence.Save(),"Snapshot written to disk");string saved=persistence.Serialize();double savedReserve=experiment.Ledger.reserve;double original=probe.Reading.shift;
            experiment.AdjustPreload(experiment.preload);yield return WaitMeasurement();Check(Math.Abs(probe.Reading.shift/original-4)<.003,"Double preload gives fourfold weak response");
            experiment.AdjustPreload(-2*experiment.preload);yield return WaitMeasurement();Check(Math.Abs(probe.Reading.shift/original-4)<.003,"Strain sign reversal preserves signal");
            persistence.Restore(saved);probe.ChangeDistance(.3f);yield return WaitMeasurement();Check(Math.Abs(probe.Reading.shift)<Math.Abs(original),"Moving probe farther reduces response");
            persistence.Restore(saved);yield return WaitMeasurement();probe.Rotate(90);yield return WaitMeasurement();Check(Math.Abs(probe.Reading.shift-original)>1e-9,"Probe orientation changes its reading");
            persistence.Restore(saved);var first=experiment.Geometry.strokes[0];Check(experiment.Cut(first.id,0,new D3(0,-.28,0)),"Mechanically significant cut committed");yield return WaitMeasurement();Check(experiment.Ledger.stored>0&&experiment.Ledger.released>0,"Cut retains supported strain and records unrecovered release");
            Check(persistence.Load(),"Snapshot reloaded from disk");yield return WaitMeasurement();receipt.restored=probe.Reading.shift;Check(Math.Abs(receipt.restored-original)<1e-12&&Math.Abs(experiment.Ledger.reserve-savedReserve)<1e-12,"Geometry, supports, ledger and reading restored exactly");AimAt(experiment.Surface("paper").World(default));yield return Capture("03-restored-experiment");
            for(int i=0;i<180;i++){yield return null;frameTimes.Add(Time.unscaledDeltaTime);}
            receipt.frames=frameTimes.Count;receipt.averageFps=1/frameTimes.Average();var sorted=frameTimes.OrderBy(v=>v).ToArray();receipt.p95FrameMs=sorted[(int)(sorted.Length*.95)]*1000;receipt.recomputeMs=probe.LastMilliseconds;receipt.strokes=experiment.Geometry.strokes.Count;receipt.segments=experiment.Mechanics.graph.edges.Count;receipt.reserve=experiment.Ledger.reserve;receipt.stored=experiment.Ledger.stored;receipt.released=experiment.Ledger.released;receipt.ledgerResidual=experiment.Ledger.Residual;
            tool.Interact(LabStation.Kind.WBenchmark);yield return WaitMeasurement();receipt.wBenchmark=probe.Reading.shift;Check(Math.Abs(receipt.wBenchmark+1.401262439e-6)<1e-9,"Windows player W benchmark within 1e-9");
            persistence.Restore(saved);yield return WaitMeasurement();var hud=FindAnyObjectByType<LabHud>();
            yield return Tap(Key.Tab);Check(hud.diagnosticsPanel.activeSelf,"Tab opens actual diagnostics panel");
            float energyDeadline=Time.realtimeSinceStartup+15;while((probe.FieldEnergy==null||probe.EnergyPending)&&Time.realtimeSinceStartup<energyDeadline)yield return null;
            yield return Capture("04-diagnostics");yield return Tap(Key.Tab);
            yield return Tap(Key.F1);Check(hud.controlsPanel.activeSelf&&input.Paused,"F1 opens controls and pauses gameplay");yield return Capture("05-controls");
            yield return Tap(Key.Escape);Check(!input.Paused&&!hud.controlsPanel.activeSelf,"Escape resumes and closes help overlay");input.SetPaused(true);yield return Capture("06-pause-menu");
            Check(hud.sensitivity.fillRect.rect.width<=((RectTransform)hud.sensitivity.transform).rect.width+1&&hud.fov.fillRect.rect.width<=((RectTransform)hud.fov.transform).rect.width+1&&!hud.controls.isTextOverflowing&&!hud.diagnostics.isTextOverflowing,"Menu sliders and text fit their panels");
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);motor.enabled=true;receipt.status="PASSED";WriteReceipt();Debug.Log("ARCANA_ACCEPTANCE_PASSED "+Path.Combine(output,"player-acceptance.json"));
            Application.Quit(0);
        }
    }
}

