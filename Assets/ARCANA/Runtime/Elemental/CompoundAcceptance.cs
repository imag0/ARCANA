using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Arcana.Elemental
{
    public sealed partial class ElementalAcceptance
    {
        static List<Vector2> Loop(Vector2 center,float radius,int n=64)
        {var p=new List<Vector2>();for(int i=0;i<=n;i++){float a=i*Mathf.PI*2/n;p.Add(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius);}return p;}
        Construction CompoundGraph(float spin=1,Vector3? axis=null)
        {
            var c=new Construction{origin=new Vector3(-24,0,-18),target=new Vector3(-24,5,-18),height=5,depth=0,scale=8,vertical=true,speed=3};
            var stone=c.Add(Rune.Stone,new Vector2(-.65f,.45f));stone.sourceId="bulk-stone";stone.forceN=250000;
            var gather=c.Add(Rune.Gather,new Vector2(-.43f,.45f));gather.width=960;
            var form=c.Add(Rune.Form,new Vector2(-.2f,.45f));form.sketch=Loop(Vector2.zero,.38f);form.fillMode=1;
            var turn=c.Add(Rune.Spin,new Vector2(.05f,.45f));turn.spin=spin;turn.axis=axis??Vector3.forward;
            c.Connect(stone.id,gather.id);c.Connect(gather.id,form.id);c.Connect(form.id,turn.id);
            var water=c.Add(Rune.Water,new Vector2(-.65f,-.2f));water.sourceId="bulk-water";water.forceN=1000000;
            var mass=c.Add(Rune.Gather,new Vector2(-.43f,-.2f));mass.width=9500;
            var volume=c.Add(Rune.Form,new Vector2(-.2f,-.2f));volume.sketch=Loop(Vector2.zero,.15f);volume.fillMode=3;
            c.Connect(water.id,mass.id);c.Connect(mass.id,volume.id);
            var lightning=c.Add(Rune.Lightning,new Vector2(-.2f,-.45f));lightning.powerW=100000;
            var inner=c.Add(Rune.Combine,new Vector2(.05f,-.2f));c.Connect(volume.id,inner.id);c.Connect(lightning.id,inner.id);
            var merge=c.Add(Rune.Combine,new Vector2(.3f,.1f));c.Connect(turn.id,merge.id);c.Connect(inner.id,merge.id);
            var assemble=c.Add(Rune.Assemble,new Vector2(.47f,.1f));var launch=c.Add(Rune.Launch,new Vector2(.65f,.1f));launch.width=60;
            var hold=c.Add(Rune.Hold,new Vector2(.65f,-.3f));hold.seconds=120;c.Connect(merge.id,assemble.id);c.Connect(assemble.id,launch.id);c.Connect(launch.id,hold.id);
            c.seal=Loop(Vector2.zero,.94f);return c;
        }
        IEnumerator Compound()
        {
            world.ResetArea("all");world.input.SetPaused(false);motor.enabled=false;Place(new Vector3(-24,1,-33),new Vector3(-24,8,-20));
            notebook.Show(true);yield return Click("COMPOUND STARTER");
            Check("Editable starter uses the general graph and nested validation",notebook.draft.innerSeals.Count==2&&world.Validate(notebook.draft,out _,out _,out var starterReason),detail:notebook.LastInterpretation);
            var graph=CompoundGraph();notebook.draft=graph;
            // Close a child contour through the production input path; it must not cast.
            graph.seal=Loop(Vector2.zero,.94f).Take(59).ToList();double before=world.ledger.remaining;
            yield return Click("NEW INNER SEAL");yield return Stroke(Loop(new Vector2(-.3f,-.3f),.45f));
            Check("Inner seal closes without activation or charge",graph.innerSeals.Count==1&&graph.innerSeals[0].closed&&!graph.committed&&world.ledger.remaining==before,detail:notebook.LastInterpretation);
            yield return Click("RESET PAGE VIEW");var center=ScreenPoint(Vector2.zero);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=center,scroll=new Vector2(0,120)});yield return null;yield return null;
            Check("Notebook wheel zoom changes viewport",graph.canvasZoom>1,graph.canvasZoom,1);
            var anchor=new Vector2(.08f,.05f);Vector2 screen=ScreenPoint(anchor);var canvas=notebook.board;
            Check("Zoom inverse preserves page coordinates",Vector2.Distance(canvas.ViewToPage(canvas.PageToView(anchor)),anchor)<1e-5);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=center,buttons=4});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=center+new Vector2(50,30),buttons=4});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=center+new Vector2(50,30)});yield return null;yield return null;
            Check("Middle mouse pans without drawing",graph.canvasPan.magnitude>.01f,graph.canvasPan.magnitude,.01);
            yield return Capture("compound-notebook-zoom");yield return Click("RESET PAGE VIEW");
            // The child region deliberately crosses the parent? No: its furthest point is < .88.
            graph.seal=Loop(Vector2.zero,.94f);graph.innerSeals[0].closed=false;
            Check("Open child blocks parent with no debit",!world.CloseSeal(graph)&&world.ledger.remaining==before,detail:world.Message);
            graph.innerSeals[0].closed=true;graph.seal=Loop(Vector2.zero,.94f).Take(59).ToList();yield return Click("5 OUTER SEAL");yield return Stroke(Loop(Vector2.zero,.94f).Skip(58).ToList());
            Check("General compound graph activates through outer mouse closure",world.IsControlling(graph.id),detail:world.Message);
            if(!world.IsControlling(graph.id))throw new InvalidOperationException(world.Message);
            var cast=world.casts.Find(c=>c.id==graph.id);
            yield return new WaitForSeconds(1);
            Check("Launch waits while matter is still assembling",cast.launches.Count==1&&!cast.launches[0].launched);
            var aim=(new Vector3(.25f,.5f,1)).normalized;Aim(motor.view.transform.position+aim*30);
            float deadline=Time.time+36;while(!cast.launches[0].ready&&Time.time<deadline)yield return new WaitForFixedUpdate();
            yield return LaunchClick(cast);
            System.IO.File.WriteAllText(System.IO.Path.Combine(output,"compound-state.json"),JsonUtility.ToJson(world.Capture(graph),true));
            Check("Assembly completes from actual packet positions",cast.launches[0].launched,cast.elapsed,40,cast.status);
            if(!cast.launches[0].launched)yield break;
            Check("Launch captures look at assembly completion",Vector3.Dot(cast.launches[0].direction,aim)>.999,Vector3.Dot(cast.launches[0].direction,aim),.999);
            var water=world.bodies.Where(b=>b.owner==cast.id&&b.kind==MatterKind.Water).ToArray();
            Check("Revolved volume has finite physical water mass",water.Length>12&&Math.Abs(water.Sum(b=>b.sheetSize.x*b.sheetSize.y*b.sheetSize.z*1000)-water.Sum(b=>b.kg))<.1,water.Sum(b=>b.kg));
            Check("Combined lightning energy is carried in water",water.Sum(b=>b.electricalJ)>1000,water.Sum(b=>b.electricalJ),1000);
            var initial=world.bodies.Where(b=>b.owner==cast.id).ToDictionary(b=>b.id,b=>b.Body.position);yield return new WaitForSeconds(2);
            var state=cast.launches[0];double minTravel=world.bodies.Where(b=>initial.ContainsKey(b.id)).Min(b=>Vector3.Dot(b.Body.position-initial[b.id],aim));
            Check("Whole compound including stone ring travels",minTravel>2,minTravel,2);
            double error=world.guided.Where(g=>g.castId==cast.id).Max(g=>{var c=cast.channels[g.channel];var target=c.anchor+Quaternion.AngleAxis(state.elapsed*c.spin*Mathf.Rad2Deg,c.axis)*(g.heldPoint-c.anchor)+state.offset+state.direction*state.distance;return Vector3.Distance(world.bodies.Find(b=>b.id==g.bodyId).Body.position,target);});
            Check("Flying compound retains independent forms",error<.3,error,.3);
            Place(state.center+state.direction*state.distance-aim*9,state.center+state.direction*state.distance);yield return Capture("compound-flight");
            var focus=state.center+state.direction*state.distance;Place(focus-aim*8+Vector3.right*2,focus);yield return Capture("compound-closeup");
            string saved=JsonUtility.ToJson(world.Capture(graph));double stored=water.Sum(b=>b.electricalJ);world.RestoreWorld(saved);cast=world.casts.Find(c=>c.id==graph.id);
            Check("Flight direction and conductive energy survive restore",Vector3.Dot(cast.launches[0].direction,aim)>.999&&Math.Abs(world.bodies.Where(b=>b.owner==cast.id&&b.kind==MatterKind.Water).Sum(b=>b.electricalJ)-stored)<.1);
            world.input.SetPaused(false);var charged=world.bodies.First(b=>b.owner==cast.id&&b.electricalJ>0);var receiver=world.posts.First(p=>p.receiver);double old=receiver.receivedJ,energy=charged.electricalJ;
            world.Cancel();charged.Body.position=receiver.transform.position+Vector3.up*3;charged.Body.linearVelocity=Vector3.down;Physics.SyncTransforms();yield return new WaitForSeconds(2);
            Check("Physical conductive impact debits stored energy",charged.electricalJ==0&&Math.Abs(receiver.receivedJ-old-energy*.9)<.1,receiver.receivedJ-old,energy*.9);
            world.ResetArea("all");var wind=Graph(Rune.Air,new Vector3(-24,0,-18),new Vector3(-24,3,-18),Rune.Form);wind.height=3;wind.scale=4;wind.nodes[1].fillMode=1;wind.nodes[1].sketch=Loop(Vector2.zero,.45f);var spin=wind.Add(Rune.Spin,new Vector2(.5f,.3f));wind.Connect(wind.nodes[1].id,spin.id);spin.spin=2;
            world.CloseSeal(wind);Place(new Vector3(-29,1,-24),wind.target);yield return new WaitForSeconds(1);
            var air=FindObjectsByType<ParticleSystem>().FirstOrDefault(p=>p.name=="Flow-aligned air filaments");Check("Air renderer shows live field tracers",air!=null&&air.particleCount>0,air?.particleCount??0);yield return Capture("compound-air-streamlines");
            int systems=FindObjectsByType<ParticleSystem>().Length;var rock=Take("bulk-stone",20,wind.target);for(int i=0;i<10;i++)world.Contact(rock,wind.target,3);yield return new WaitForSeconds(.1f);
            Check("Ten impact events reuse particle systems",FindObjectsByType<ParticleSystem>().Length==systems,systems);yield return Capture("compound-earth-dust");world.Cancel();
            world.ResetArea("all");var legacy=world.Capture(graph);legacy.version=1;legacy.sources.RemoveAll(s=>world.Source(s.id)?.introducedVersion==2);world.RestoreWorld(JsonUtility.ToJson(legacy));
            Check("Older saves migrate newly added finite reservoirs",world.Source("bulk-water").Available==20000&&world.Source("bulk-stone").Available==40000);world.input.SetPaused(false);
            Check("Compound conservation ledger closes",Math.Abs(world.ledger.Residual)<.01,world.ledger.Residual,.01);
        }
    }
}
