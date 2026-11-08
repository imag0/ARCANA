using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace Arcana.Elemental
{
    public sealed partial class ElementalAcceptance
    {
        IEnumerator LaunchClick(CastState cast)
        {
            Check("Compound waits for deliberate launch click",cast.launches[0].ready&&!cast.launches[0].launched);
            float spin=cast.channels.First(c=>c.spin!=0).spin;yield return Tap(UnityEngine.InputSystem.Key.RightBracket);Check("Live angular speed increases from keyboard",Mathf.Abs(cast.channels.First(c=>c.spin!=0).spin-spin*1.25f)<.001f);yield return Tap(UnityEngine.InputSystem.Key.LeftBracket);
            yield return new WaitForSeconds(.4f);Check("Ready compound does not auto-launch",!cast.launches[0].launched);
            var armedAim=motor.view.transform.forward;Aim(motor.view.transform.position+armedAim*30+Vector3.right*2);yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();Check("Ready stays armed while aiming",cast.launches[0].ready&&!cast.launches[0].launched);Aim(motor.view.transform.position+armedAim*30);yield return new WaitForFixedUpdate();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=new Vector2(Screen.width/2,Screen.height/2),buttons=1});yield return null;yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=new Vector2(Screen.width/2,Screen.height/2)});yield return null;yield return null;
            Check("Left mouse launches ready compound",cast.launches[0].launched);
        }
        IEnumerator LaunchRevision()
        {
            world.ResetArea("all");world.input.SetPaused(false);motor.enabled=false;
            Place(new Vector3(-24,1,-33),new Vector3(-24,8,-20));notebook.Show(true);yield return Click("COMPOUND STARTER");
            notebook.selected=notebook.draft.nodes.First(n=>n.rune==Rune.Spin);yield return Click("SETTINGS / HELP");yield return EnterNumber("Spin rad/s","4");yield return EnterNumber("Launch speed m/s","18");yield return Tap(UnityEngine.InputSystem.Key.Escape);yield return Click("APPLY SETTINGS");
            var settings=world.Evaluate(notebook.draft,GlyphPlan.Compile(notebook.draft));Check("Typed spin and launch speeds reach simulation",settings.Any(c=>Mathf.Abs(c.spin-4)<.001f)&&settings.All(c=>Mathf.Abs(c.speed-18)<.001f));yield return Capture("manual-speed-settings");yield return Click("SETTINGS / HELP");
            float original=notebook.draft.spellSize;yield return Click("SMALLER");Check("Smaller button reduces the whole spell",notebook.draft.spellSize<original,notebook.draft.spellSize,original);
            var small=world.Evaluate(notebook.draft,GlyphPlan.Compile(notebook.draft));double smallMass=small.Where(c=>c.element==Rune.Water||c.element==Rune.Stone).Sum(c=>c.amount);float smallCell=small.First(c=>c.volume&&c.element==Rune.Water).cellSize.x;
            yield return Click("BIGGER");var large=world.Evaluate(notebook.draft,GlyphPlan.Compile(notebook.draft));double largeMass=large.Where(c=>c.element==Rune.Water||c.element==Rune.Stone).Sum(c=>c.amount);
            Check("Size scales geometry and finite material together",Math.Abs(largeMass/smallMass-Math.Pow(1.25,3))<.001&&Mathf.Abs(large.First(c=>c.volume&&c.element==Rune.Water).cellSize.x/smallCell-1.25f)<.001,largeMass/smallMass,1.953125);
            var draft=notebook.draft;draft.seal.Add(draft.seal[0]);notebook.board.SetVerticesDirty();var water=draft.nodes.First(n=>n.rune==Rune.Water);string source=water.sourceId;water.sourceId="";double charge=world.ledger.remaining;
            yield return Click("CHECK / CAST SEAL");Check("Closed invalid seal explains missing source in game",!draft.committed&&notebook.SealBlocked&&notebook.SealStatus.Contains("source")&&world.ledger.remaining==charge,detail:notebook.SealStatus);yield return Capture("launch-blocked-reason");
            water.sourceId=source;yield return Click("CHECK / CAST SEAL");Check("Corrected closed seal can retry without redrawing",draft.committed&&world.IsControlling(draft.id),detail:world.Message);world.Cancel();world.ResetArea("all");
            // The same normal mouse path must accept the closing stroke backwards.
            notebook.Show(true);yield return Click("COMPOUND STARTER");draft=notebook.draft;var gap=new System.Collections.Generic.List<Vector2>{draft.seal.Last()};for(int i=1;i<=14;i++){float a=(338+42*i/14f)*Mathf.Deg2Rad;gap.Add(new Vector2(Mathf.Cos(a)*.93f,Mathf.Sin(a)*.88f));}gap.Reverse();yield return Click("5 OUTER SEAL");yield return Stroke(gap);
            Check("Reverse-direction closure casts through mouse input",draft.committed,detail:notebook.LastInterpretation);if(!draft.committed)throw new InvalidOperationException(notebook.LastInterpretation);
            var cast=world.casts.Find(c=>c.id==draft.id);yield return new WaitForSeconds(1);
            var trapped=world.bodies.First(b=>b.owner==cast.id&&b.kind==MatterKind.Stone);var cage=new GameObject("Acceptance closed obstacle cage");cage.transform.position=trapped.Body.position;
            foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back}){var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(cage.transform,false);wall.transform.localPosition=normal;wall.transform.localScale=new Vector3(normal.x!=0?.2f:2.2f,normal.y!=0?.2f:2.2f,normal.z!=0?.2f:2.2f);wall.GetComponent<Renderer>().enabled=false;}Physics.SyncTransforms();
            var aim=new Vector3(.2f,.45f,1).normalized;Place(motor.transform.position+Vector3.right*2,motor.view.transform.position+Vector3.right*2+aim*30);
            float deadline=Time.time+35;while((cast.launches.Count==0||!cast.launches[0].ready)&&Time.time<deadline)yield return new WaitForFixedUpdate();
            if(!cast.launches[0].ready)System.IO.File.WriteAllText(System.IO.Path.Combine(output,"assembly-timeout.json"),JsonUtility.ToJson(world.Capture(draft),true));
            var trappedGuide=world.guided.Find(g=>g.bodyId==trapped.id);Check("A stone escapes a closed physical cage and settles",trapped.GetComponent<AssemblyRouting>()?.Phasing==true&&Vector3.Distance(trapped.Body.position,trappedGuide.heldPoint+cast.launches[0].offset)<.18f,detail:cast.status);cage.SetActive(false);Destroy(cage);yield return null;
            yield return LaunchClick(cast);var state=cast.launches[0];Check("Moved player assembles a launch in front",state.launched&&Vector3.Dot(state.center-motor.view.transform.position,aim)>1&&Vector3.Dot(state.center-motor.view.transform.position,aim)<5&&Vector3.Cross(state.center-motor.view.transform.position,aim).magnitude<.2f,Vector3.Distance(state.center,motor.view.transform.position),5,cast.status);
            Check("Flight uses current view after player moves",state.launched&&Vector3.Dot(state.direction,aim)>.999,Vector3.Dot(state.direction,aim),.999);
            if(!state.launched)throw new InvalidOperationException(cast.status);yield return Capture("launch-player-origin");
            var ids=world.guided.Where(g=>g.castId==cast.id).Select(g=>g.bodyId).ToHashSet();double mass=world.bodies.Where(b=>ids.Contains(b.id)).Sum(b=>b.kg);int impacts=world.impactEvents;
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Acceptance impact wall";obstacle.transform.SetPositionAndRotation(state.center+aim*8,Quaternion.LookRotation(aim));obstacle.transform.localScale=new Vector3(12,12,.4f);obstacle.GetComponent<Renderer>().sharedMaterial=world.bodyMaterial;Physics.SyncTransforms();
            deadline=Time.time+8;while(!state.impacted&&Time.time<deadline)yield return new WaitForFixedUpdate();yield return null;
            Check("Launched compound breaks on a real obstacle",state.impacted&&world.impactEvents==impacts+1,world.impactEvents-impacts,1);
            Check("Impact releases every compound branch once",!cast.active&&!world.guided.Any(g=>g.castId==cast.id)&&world.bodies.Where(b=>ids.Contains(b.id)).All(b=>!b.Controlled&&b.sheetSize==Vector3.zero));
            Check("Impact preserves physical material and charge accounting",Math.Abs(world.bodies.Where(b=>ids.Contains(b.id)).Sum(b=>b.kg)-mass)<.01&&Math.Abs(world.ledger.Residual)<.01,world.ledger.Residual,.01);
            Check("Impact produces visible particles",FindObjectsByType<ParticleSystem>().Sum(p=>p.particleCount)>40,FindObjectsByType<ParticleSystem>().Sum(p=>p.particleCount),40);yield return Capture("launch-impact-breakup");yield return new WaitForSeconds(.4f);yield return Capture("launch-impact-dispersed");Destroy(obstacle);yield return null;
            world.ResetArea("all");Place(new Vector3(-24,1,-33),new Vector3(-24,8,-20));var held=CompoundGraph();held.nodes.First(n=>n.rune==Rune.Launch).width=.5f;notebook.draft=held;
            Check("Full-size held compound validates",world.CloseSeal(held),detail:world.Message);var heldCast=world.casts.Find(c=>c.id==held.id);if(heldCast==null)throw new InvalidOperationException(world.Message);
            // Missing pre-revision JSON fields deserialize to these zero values.
            var old=JsonUtility.FromJson<PlaygroundSnapshot>(JsonUtility.ToJson(world.Capture(held)));
            foreach(var ch in old.casts.SelectMany(c=>c.channels)){ch.assemblyCenter=Vector3.zero;ch.frontDepth=ch.frontDown=ch.formationRadius=0;}
            foreach(var launch in old.casts.SelectMany(c=>c.launches)){launch.center=launch.referenceCenter=launch.offset=Vector3.zero;}
            world.RestoreWorld(JsonUtility.ToJson(old));heldCast=world.casts.Find(c=>c.id==held.id);world.input.SetPaused(false);
            Check("Older pending compounds restore a valid launch frame",heldCast.channels.All(ch=>ch.frontDepth>0&&ch.formationRadius>0));
            deadline=Time.time+35;while((heldCast.launches.Count==0||!heldCast.launches[0].ready)&&Time.time<deadline)yield return new WaitForFixedUpdate();yield return LaunchClick(heldCast);yield return new WaitForSeconds(3);
            var surface=world.GetComponent<FluidSurface>();long builds=surface.rebuildCount;var frames=new System.Collections.Generic.List<double>();var costs=new System.Collections.Generic.List<double>();float until=Time.realtimeSinceStartup+4;
            while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime*1000);costs.Add(surface.lastUpdateMs);}
            costs.Sort();frames.Sort();double p95=frames[(int)(frames.Count*.95)],renderP95=costs[(int)(costs.Count*.95)];
            Check("Assembled water reuses its surface",heldCast.active&&heldCast.launches[0].launched&&surface.rebuildCount-builds<=2,surface.rebuildCount-builds,2,heldCast.status);
            Check("Cached assembled surface stays under 3 ms p95",renderP95<3,renderP95,3);
            Check("Full assembled compound maintains 60 FPS average",frames.Count>60&&frames.Average()<1000.0/60,1000/frames.Average(),60,"p95 frame "+p95.ToString("F2")+" ms; another user player may be open");
            System.IO.File.WriteAllText(System.IO.Path.Combine(output,"held-performance.json"),JsonUtility.ToJson(new HeldPerformance{frames=frames.ToArray(),rendererMs=costs.ToArray(),rebuilds=surface.rebuildCount-builds},true));yield return Capture("launch-held-optimized");
            var releasedWater=world.bodies.Where(b=>b.owner==held.id&&b.kind==MatterKind.Water).ToArray();world.Cancel();
            Check("Manual release restores physical fluid droplets",releasedWater.Length>0&&releasedWater.All(b=>!b.Controlled&&b.GetComponent<SphereCollider>().enabled&&!b.GetComponent<BoxCollider>().enabled));
        }
        [Serializable] sealed class HeldPerformance {public double[] frames,rendererMs;public long rebuilds;}
    }
}
