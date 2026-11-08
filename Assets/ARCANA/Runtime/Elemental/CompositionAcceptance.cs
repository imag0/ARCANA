using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Arcana.Elemental
{
    public sealed partial class ElementalAcceptance
    {
        IEnumerator EnterNumber(string label,string value)
        {
            var field=FindObjectsByType<TMP_InputField>().First(f=>f.name==label+" input");
            var r=(RectTransform)field.transform;var point=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftCtrl,Key.A));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            foreach(char ch in value){InputSystem.QueueTextEvent(keyboard,ch);yield return null;}
            yield return null;
        }
        IEnumerator Composition()
        {
            world.ResetArea("all");world.input.SetPaused(false);motor.enabled=false;
            Place(new Vector3(-22,.2f,-18),new Vector3(-19,1.3f,-12));
            // No example factory: the graph, contour and parameters are independent data.
            var wall=Graph(Rune.Stone,new Vector3(-19,0,-12),new Vector3(-19,1.1f,-12),Rune.Gather);wall.name="Acceptance arbitrary stone contour";wall.height=1.05f;wall.scale=2;wall.vertical=true;
            wall.nodes[0].sourceId="courtyard-stone";wall.nodes[0].forceN=12000;wall.nodes[1].width=230;
            var shape=wall.Add(Rune.Form,new Vector2(.2f,-.2f));shape.fillMode=2;var hold=wall.Add(Rune.Hold,new Vector2(.6f,-.2f));hold.seconds=90;wall.Connect(wall.nodes[1].id,shape.id);wall.Connect(shape.id,hold.id);
            notebook.draft=wall;notebook.selected=shape;notebook.Show(true);yield return Click("3 FORM SHAPE");
            var polygon=new[]{new Vector2(-.5f,-.5f),new Vector2(.5f,-.5f),new Vector2(.5f,.5f),new Vector2(.15f,.5f),new Vector2(.15f,.25f),new Vector2(-.5f,.25f),new Vector2(-.5f,-.5f)};
            yield return Stroke(DrawnGeometry.Resample(polygon,.04f));Check("Independent FORM receives hand-drawn nonrectangular contour",shape.sketch.Count>20&&wall.form.Count==0,shape.sketch.Count);
            yield return Click("SETTINGS / HELP");yield return EnterNumber("Sketch rotation ° x,y,z","0, 13, 0");yield return Tap(Key.Escape);yield return Click("APPLY SETTINGS");
            wall=notebook.draft;shape=wall.nodes.Find(n=>n.rune==Rune.Form);Check("Numeric settings accept arbitrary typed orientation",Mathf.Abs(shape.geometryEuler.y-13)<.01&&world.input.Paused&&notebook.Open,shape.geometryEuler.y,13,notebook.LastInterpretation);
            yield return Capture("composition-settings");yield return Click("SETTINGS / HELP");yield return Click("SEAL GUIDE");yield return CloseGap();
            Check("Custom stone FORM commits through normal seal input",world.IsControlling(wall.id),detail:world.Message);
            if(!world.IsControlling(wall.id))throw new InvalidOperationException(world.Message);
            yield return new WaitForSeconds(18);
            var pieces=world.bodies.Where(b=>b.owner==wall.id).ToArray();var guides=world.guided.Where(g=>g.castId==wall.id).ToArray();double error=guides.Length>0?guides.Max(g=>Vector3.Distance(world.bodies.Find(b=>b.id==g.bodyId).Body.position,g.heldPoint)):999;
            System.IO.File.WriteAllText(System.IO.Path.Combine(output,"wall-state.json"),JsonUtility.ToJson(world.Capture(wall),true));
            Check("Drawn stone wall forms with bounded forces",pieces.Length>=20&&error<.22,error,.22);
            Check("Filled stone volume equals withdrawn mass",pieces.Length>0&&Math.Abs(pieces.Sum(b=>b.sheetSize.x*b.sheetSize.y*b.sheetSize.z*2400)-230)<.02,pieces.Sum(b=>b.kg),230);
            Check("Stone shape has real collidable faces",pieces.All(b=>b.GetComponent<BoxCollider>()?.enabled==true)&&Physics.Raycast(new Vector3(-19,1,-16),Vector3.forward,out var hit,8)&&hit.collider.GetComponent<MatterBody>()!=null);
            Aim(new Vector3(-19,1.1f,-12));yield return Capture("composition-stone-wall");
            world.Cancel(wall.id);yield return new WaitForSeconds(3);
            double releasedError=guides.Max(g=>Vector3.Distance(world.bodies.Find(b=>b.id==g.bodyId).Body.position,g.heldPoint));Check("Released supported wall stays assembled",releasedError<.3,releasedError,.3);
            Check("Released wall retains physical stone geometry",pieces.All(b=>!b.Controlled&&b.GetComponent<BoxCollider>().enabled&&b.sheetSize.sqrMagnitude>0));
            var pieceIds=new HashSet<string>(pieces.Select(p=>p.id));string json=JsonUtility.ToJson(world.Capture(wall));var restored=world.RestoreWorld(json);Check("Stone and branch geometry survive world restore",world.bodies.Count(b=>pieceIds.Contains(b.id)&&b.GetComponent<BoxCollider>()?.enabled==true)==pieceIds.Count&&restored.nodes.Find(n=>n.rune==Rune.Form).sketch.Count==shape.sketch.Count);world.input.SetPaused(false);
            world.ResetArea("all");
            var vortex=Graph(Rune.Air,new Vector3(-20,0,-12),new Vector3(-20,7,-12),Rune.Form);vortex.name="Acceptance tall drawn thermal flow";vortex.vertical=true;vortex.height=7;vortex.scale=12;vortex.speed=4;
            var form=vortex.nodes[1];form.fillMode=1;form.sketch=new List<Vector2>{new Vector2(0,-.5f),new Vector2(0,.5f)};
            var spread=vortex.Add(Rune.Spread,new Vector2(.35f,.4f));spread.width=5;var spin=vortex.Add(Rune.Spin,new Vector2(.6f,.4f));spin.spin=2;spin.axis=Vector3.up;vortex.Connect(form.id,spread.id);vortex.Connect(spread.id,spin.id);
            var fire=vortex.Add(Rune.Fire,new Vector2(-.5f,-.3f));fire.powerW=1000000;fire.seconds=60;var fireSpread=vortex.Add(Rune.Spread,new Vector2(-.1f,-.3f));fireSpread.width=5;vortex.Connect(fire.id,fireSpread.id);
            var combine=vortex.Add(Rune.Combine,new Vector2(.35f,-.1f));var sustain=vortex.Add(Rune.Hold,new Vector2(.6f,-.3f));sustain.seconds=60;vortex.Connect(spin.id,combine.id);vortex.Connect(fireSpread.id,combine.id);vortex.Connect(combine.id,sustain.id);
            Check("Large fire and air composition validates without recipe",world.CloseSeal(vortex),detail:world.Message);
            Place(new Vector3(-30,.2f,-26),new Vector3(-20,6,-12));yield return new WaitForSeconds(5);
            var hot=world.Capture(vortex).motes.Where(m=>m.hot&&m.heat>0).ToArray();var center=vortex.target;double turn=hot.Length==0?0:hot.Average(m=>Vector3.Dot(Vector3.Cross(Vector3.up,m.p-center),m.v));double span=hot.Length==0?0:hot.Max(m=>m.p.y)-hot.Min(m=>m.p.y);
            Check("Tall drawn fire volume follows actual gas state",hot.Length>100&&span>10,span,10);Check("Hot gas acquires angular momentum from air",turn>1,turn,1);
            var field=world.WindAt(center+Vector3.right*2);Check("Wide vortex reaches beyond original narrow radius",field.magnitude>2,field.magnitude,2);
            yield return Capture("composition-large-fire-vortex");
            var air=world.casts.Find(c=>c.id==vortex.id).channels.First(c=>c.element==Rune.Air);air.spin=-air.spin;var reversed=world.WindAt(center+Vector3.right*2);Check("Reversing spin reverses the physical field",field.z*reversed.z<0,reversed.z);
            world.Cancel();
            // Independent 3D paths, different plane rotations, seven elements in one graph.
            var all=new Construction{origin=new Vector3(-20,0,-12),height=4,scale=3,vertical=true};int count=0;
            foreach(Rune rune in new[]{Rune.Fire,Rune.Water,Rune.Air,Rune.Stone,Rune.Frost,Rune.Lightning,Rune.Light})
            {
                var e=all.Add(rune,new Vector2(-.7f,.7f-count*.2f));e.sourceId=rune==Rune.Stone?"courtyard-stone":"courtyard-water";
                var route=all.Add(Rune.Direct,new Vector2(.1f,.7f-count*.2f));route.sketch=new List<Vector2>{new Vector2(-.7f,-.2f),new Vector2(-.1f,.5f),new Vector2(.6f,-.3f)};route.depthProfile=new List<Vector2>{new Vector2(-1,-.2f),new Vector2(0,.6f),new Vector2(1,.1f)};route.geometryEuler=new Vector3(count*11,count*17,count*23);route.geometryOffset=new Vector3(0,count*.4f,0);all.Connect(e.id,route.id);count++;
            }
            Check("All seven elements accept separate nonplanar paths",world.Validate(all,out _,out var channels,out var why)&&channels.Count==7,channels.Count,7,why);
            Check("Branch geometry remains independent",channels.Select(c=>c.path[c.path.Count/2]).Distinct().Count()==7);
            world.ResetArea("all");
            var spatial=Graph(Rune.Water,new Vector3(-27,0,-8),new Vector3(-24,3,-6),Rune.Gather);spatial.nodes[0].sourceId="courtyard-water";spatial.nodes[1].width=4;spatial.scale=2;spatial.speed=1.5f;spatial.vertical=true;
            var spatialRoute=spatial.Add(Rune.Direct,new Vector2(.3f,-.2f));spatial.Connect(spatial.nodes[1].id,spatialRoute.id);spatialRoute.sketch=new List<Vector2>{new Vector2(-.7f,0),new Vector2(-.2f,.5f),new Vector2(.3f,.3f),new Vector2(.7f,-.2f)};spatialRoute.depthProfile=new List<Vector2>{new Vector2(-1,0),new Vector2(0,.35f),new Vector2(1,-.2f)};spatialRoute.profileDepth=1;spatialRoute.geometryEuler=new Vector3(15,27,19);
            var end=spatial.Add(Rune.End,new Vector2(.6f,-.2f));spatial.Connect(spatialRoute.id,end.id);Check("Nonplanar water route activates",world.CloseSeal(spatial),detail:world.Message);
            double routeError=0;int observations=0;long startStep=world.simulationSteps;
            while(world.simulationSteps-startStep<900){yield return new WaitForFixedUpdate();foreach(var guide in world.guided.Where(g=>g.castId==spatial.id)){var cast=world.casts.Find(c=>c.id==spatial.id);var target=DrawnGeometry.At(cast.channels[guide.channel].path,guide.distance,out _);routeError=Math.Max(routeError,Vector3.Distance(world.bodies.Find(b=>b.id==guide.bodyId).Body.position,target));observations++;}}
            Check("Physical water follows rotated depth-profile route",observations>100&&routeError<=.1241,routeError,.1241);
            world.Cancel();
            Check("Composition conserves material and charge",Math.Abs(world.MaterialTotals()["courtyard-stone"]-250)<1e-4&&Math.Abs(world.ledger.Residual)<.01,world.ledger.Residual,.01);
        }
    }
}
