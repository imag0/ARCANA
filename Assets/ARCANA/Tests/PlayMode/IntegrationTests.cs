using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Arcana.Core;
using Arcana.Mechanics;

namespace Arcana.Tests
{
    public sealed class IntegrationTests
    {
        FirstPersonMotor motor;LabInput input;ExperimentState experiment;DrawingTool tool;ProbeInstrument probe;ExperimentPersistence persistence;
        Keyboard keyboard;Mouse mouse;
        [UnitySetUp] public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("ArcanaLab");yield return null;
            motor=UnityEngine.Object.FindAnyObjectByType<FirstPersonMotor>();input=UnityEngine.Object.FindAnyObjectByType<LabInput>();experiment=UnityEngine.Object.FindAnyObjectByType<ExperimentState>();tool=UnityEngine.Object.FindAnyObjectByType<DrawingTool>();probe=UnityEngine.Object.FindAnyObjectByType<ProbeInstrument>();persistence=UnityEngine.Object.FindAnyObjectByType<ExperimentPersistence>();
            input.SetPaused(true);motor.enabled=false;motor.ResetPosition();Physics.SyncTransforms();for(int i=0;i<25;i++)motor.Tick(Vector2.zero,false,false,false,.02f);
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();
        }
        [UnityTearDown] public IEnumerator TearDown(){if(input!=null)input.SetPaused(true);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);yield return null;}
        [UnityTest] public IEnumerator GroundedAndDiagonalSpeed()
        {Assert.That(motor.Grounded,Is.True);var start=motor.transform.position;for(int i=0;i<25;i++)motor.Tick(Vector2.up,false,false,false,.02f);float straight=Vector3.Distance(start,motor.transform.position);motor.ResetPosition();Physics.SyncTransforms();for(int i=0;i<25;i++)motor.Tick(Vector2.zero,false,false,false,.02f);start=motor.transform.position;for(int i=0;i<25;i++)motor.Tick(Vector2.one,false,false,false,.02f);float diagonal=Vector3.Distance(start,motor.transform.position);Assert.That(straight,Is.EqualTo(2).Within(.05));Assert.That(diagonal,Is.EqualTo(straight).Within(.05));yield return null;}
        [UnityTest] public IEnumerator ActualKeyboardMovesAndPauseExcludesInput()
        {input.SetPaused(false);motor.enabled=true;var start=motor.transform.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSeconds(.25f);Assert.That(motor.transform.position.z-start.z,Is.GreaterThan(.2f));input.SetPaused(true);start=motor.transform.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSeconds(.1f);Assert.That(Vector3.Distance(start,motor.transform.position),Is.LessThan(.01));Assert.That(input.actions.FindActionMap("Gameplay").enabled,Is.False);Assert.That(input.actions.FindActionMap("UI").enabled,Is.True);}
        [UnityTest] public IEnumerator JumpIsGroundedGatedAndGravityReturns()
        {float ground=motor.transform.position.y;motor.Tick(Vector2.zero,false,false,true,.02f);float first=motor.VerticalSpeed;for(int i=0;i<6;i++)motor.Tick(Vector2.zero,false,false,true,.02f);Assert.That(motor.VerticalSpeed,Is.LessThan(first));Assert.That(motor.transform.position.y,Is.GreaterThan(ground+.3f));for(int i=0;i<70;i++)motor.Tick(Vector2.zero,false,false,false,.02f);Assert.That(motor.Grounded,Is.True);Assert.That(motor.transform.position.y,Is.EqualTo(ground).Within(.04));yield return null;}
        [UnityTest] public IEnumerator CrouchPreservesFeetAndCeilingBlocksStand()
        {float feet=motor.transform.position.y;motor.Tick(Vector2.zero,false,true,false,.02f);Assert.That(motor.Crouched,Is.True);Assert.That(motor.transform.position.y,Is.EqualTo(feet).Within(.03));var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.transform.position=motor.transform.position+Vector3.up*1.45f;ceiling.transform.localScale=new Vector3(2,.15f,2);Physics.SyncTransforms();Assert.That(motor.CanStand(),Is.False);motor.Tick(Vector2.zero,false,false,false,.02f);Assert.That(motor.Crouched,Is.True);UnityEngine.Object.Destroy(ceiling);yield return null;Physics.SyncTransforms();motor.Tick(Vector2.zero,false,false,false,.02f);Assert.That(motor.Crouched,Is.False);}
        [UnityTest] public IEnumerator WallCollisionAndFallReset()
        {for(int i=0;i<150;i++)motor.Tick(Vector2.up,true,false,false,.02f);Assert.That(motor.transform.position.z,Is.LessThan(3));var cc=motor.GetComponent<CharacterController>();cc.enabled=false;motor.transform.position=new Vector3(0,-12,0);cc.enabled=true;motor.Tick(Vector2.zero,false,false,false,.02f);Assert.That(Vector3.Distance(motor.transform.position,motor.spawn),Is.LessThan(.01));yield return null;}
        [UnityTest] public IEnumerator StepsAreTraversable()
        {var cc=motor.GetComponent<CharacterController>();cc.enabled=false;motor.transform.position=new Vector3(-6.5f,.04f,-5.2f);cc.enabled=true;Physics.SyncTransforms();for(int i=0;i<40;i++)motor.Tick(Vector2.up,false,false,false,.02f);Assert.That(motor.transform.position.y,Is.GreaterThan(.65f));yield return null;}
        [UnityTest] public IEnumerator MouseLookDeltaHasNoFrameRateMultiplier()
        {motor.sensitivity=.12f;motor.invertY=false;motor.ResetPosition();motor.Look(new Vector2(100,30));float yaw=motor.transform.eulerAngles.y,pitch=motor.Pitch;motor.ResetPosition();for(int i=0;i<10;i++)motor.Look(new Vector2(10,3));Assert.That(Mathf.DeltaAngle(yaw,motor.transform.eulerAngles.y),Is.Zero.Within(.001));Assert.That(motor.Pitch,Is.EqualTo(pitch).Within(.001));motor.Look(new Vector2(0,100000));Assert.That(motor.Pitch,Is.EqualTo(-85));Assert.That(motor.view.transform.eulerAngles.z,Is.Zero.Within(.001));yield return null;}
        [UnityTest] public IEnumerator PauseFocusAndCursorTransitions()
        {input.SetPaused(false);Assert.That(input.Paused,Is.False);Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.Locked));input.SendMessage("OnApplicationFocus",false);Assert.That(input.Paused,Is.True);Assert.That(input.Read2("Move"),Is.EqualTo(Vector2.zero));Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));input.SendMessage("OnApplicationFocus",true);Assert.That(input.Paused,Is.True);input.SetPaused(false);Assert.That(input.Paused,Is.False);yield return null;}
        [UnityTest] public IEnumerator CameraRaycastFreehandAndMissBreaks()
        {
            var cc=motor.GetComponent<CharacterController>();cc.enabled=false;motor.transform.position=new Vector3(0,.16f,1.7f);cc.enabled=true;motor.cameraPivot.localPosition=Vector3.up*1.64f;motor.transform.rotation=Quaternion.identity;motor.cameraPivot.localRotation=Quaternion.identity;Physics.SyncTransforms();input.SetPaused(false);
            Assert.That(tool.Contact(out var hit,out var surface),Is.True);Assert.That(surface.stableId,Is.EqualTo("paper"));
            InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});yield return null;
            motor.Look(new Vector2(20,0));yield return null;motor.Look(new Vector2(20,0));yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
            Assert.That(experiment.Geometry.strokes.Count,Is.GreaterThanOrEqualTo(1));Assert.That(experiment.Ledger.stored,Is.Zero.Within(1e-15));Assert.That(tool.Preview,Is.Null);
            tool.Sample(surface,new D3(-.5,0,0));tool.Sample(surface,new D3(-.4,0,0));tool.Sample(surface,new D3(.6,0,0));tool.Sample(surface,new D3(.7,0,0));tool.Finish();Assert.That(experiment.Geometry.strokes.Count,Is.GreaterThanOrEqualTo(3));
        }
        [UnityTest] public IEnumerator ClosedLoopPreparesOnceProbeAndSaveRestore()
        {
            experiment.Demo(false);probe.Place(experiment.Surface("paper"),default);yield return WaitForReading();
            Assert.That(experiment.Geometry.supports.Count,Is.EqualTo(2));Assert.That(experiment.Ledger.stored,Is.GreaterThan(0));Assert.That(probe.Reading.available,Is.True);Assert.That(Math.Abs(probe.Reading.shift),Is.GreaterThan(1e-8));
            double reserve=experiment.Ledger.reserve,signal=probe.Reading.shift;string saved=persistence.Serialize();experiment.Prepare(experiment.Geometry.strokes[0].id);Assert.That(experiment.Ledger.reserve,Is.EqualTo(reserve));
            experiment.AdjustPreload(.005);yield return WaitForReading();persistence.Restore(saved);yield return WaitForReading();Assert.That(experiment.Ledger.reserve,Is.EqualTo(reserve));Assert.That(probe.Reading.shift,Is.EqualTo(signal).Within(1e-12));Assert.That(experiment.Ledger.Residual,Is.Zero.Within(1e-12));
        }
        [UnityTest] public IEnumerator WSceneMatchesIndependentBenchmark()
        {tool.Interact(LabStation.Kind.WBenchmark);yield return WaitForReading();Assert.That(experiment.Mechanics.maxStrain,Is.EqualTo(.02).Within(1e-12));Assert.That(probe.Reading.shift,Is.EqualTo(-1.401262439e-6).Within(1e-9));Assert.That(probe.LastMilliseconds,Is.LessThan(3000));}
        [UnityTest] public IEnumerator CutUndoRedoDoNotDuplicateEnergy()
        {experiment.Demo(false);double initialReserve=experiment.Ledger.reserve,energy=experiment.Ledger.stored;var stroke=experiment.Geometry.strokes[0];Assert.That(experiment.Cut(stroke.id,0,new D3(0,-.3,0)),Is.True);Assert.That(experiment.Ledger.reserve,Is.LessThanOrEqualTo(initialReserve));Assert.That(experiment.Ledger.stored,Is.LessThan(energy));Assert.That(experiment.Ledger.stored,Is.GreaterThan(0));experiment.Undo();Assert.That(experiment.Ledger.reserve,Is.LessThan(initialReserve));experiment.Redo();Assert.That(experiment.Ledger.Residual,Is.Zero.Within(1e-12));yield return null;}
        [UnityTest] public IEnumerator JoinCompatibleEndpointsClosesActualGraph()
        {var s=experiment.Surface("paper").NewStroke(true,.02);s.points.AddRange(new[]{new D3(-.3,-.3,0),new D3(.3,-.3,0),new D3(.3,.3,0),new D3(-.3,.3,0),new D3(-.3,-.21,0)});experiment.AddStroke(s);Assert.That(experiment.Mechanics.graph.components[0].Closed,Is.False);Assert.That(experiment.Join(s.id),Is.True);Assert.That(experiment.Mechanics.graph.components[0].Closed,Is.True);yield return null;}
        [UnityTest] public IEnumerator CorruptLoadPreservesLiveExperiment()
        {experiment.Demo(true);string before=persistence.Serialize();Assert.Throws<ArgumentException>(()=>persistence.Restore("not json"));Assert.Throws<ArgumentException>(()=>persistence.Restore("{}"));Assert.That(persistence.Serialize(),Is.EqualTo(before));var s=persistence.Capture();s.ledger.reserve+=10;Assert.Throws<ArgumentException>(()=>persistence.Restore(JsonUtility.ToJson(s)));Assert.That(persistence.Serialize(),Is.EqualTo(before));yield return null;}
        [UnityTest] public IEnumerator RestoredProbeRemainsMovableAndRotatable()
        {tool.Interact(LabStation.Kind.WBenchmark);yield return WaitForReading();string saved=persistence.Serialize();var before=probe.Position;double signal=probe.Reading.shift;persistence.Restore(saved);probe.ChangeDistance(.3f);yield return WaitForReading();Assert.That(D3.Distance(before,probe.Position),Is.EqualTo(.3).Within(1e-5));Assert.That(Math.Abs(probe.Reading.shift),Is.LessThan(Math.Abs(signal)));persistence.Restore(saved);probe.Rotate(90);yield return WaitForReading();Assert.That(Math.Abs(D3.Dot(probe.Axis,new D3(1,0,0))),Is.LessThan(1e-5));}
        [UnityTest] public IEnumerator ReleasedLoopAndRepositionedSupports()
        {experiment.Demo(false);var s=experiment.Geometry.strokes[0];experiment.RepositionSupport("paper",new D3(.42,-.3,0));Assert.That(experiment.Mechanics.valid,Is.True);Assert.That(experiment.Ledger.Residual,Is.Zero.Within(1e-12));experiment.ReleaseSupports(s.id);Assert.That(experiment.Mechanics.graph.components[0].Closed,Is.True);Assert.That(experiment.Geometry.supports.Count,Is.Zero);Assert.That(experiment.Ledger.stored,Is.Zero.Within(1e-15));Assert.That(experiment.Ledger.released,Is.GreaterThan(0));yield return null;}
        [UnityTest] public IEnumerator ProbeMovementPublishesOnlyCurrentRevision()
        {experiment.Demo(true);for(int i=0;i<6;i++){probe.Place(experiment.Surface("stone"),new D3(i*.01,0,0));experiment.AdjustPreload(.001);yield return null;}yield return WaitForReading();Assert.That(probe.PublishedRevision,Is.EqualTo(experiment.Revision));Assert.That(probe.Pending,Is.False);}
        IEnumerator WaitForReading(){float until=Time.realtimeSinceStartup+10;while(probe.Pending&&Time.realtimeSinceStartup<until)yield return null;Assert.That(probe.Pending,Is.False,"Field computation did not publish in 10 seconds");Assert.That(probe.Reading,Is.Not.Null);}
    }
}
