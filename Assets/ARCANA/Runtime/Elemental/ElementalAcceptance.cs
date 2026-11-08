using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Arcana.Elemental
{
    // Explicit opt-in graphics-enabled player exercise. Synthetic Input System
    // events traverse the same EventSystem, canvas handlers and seal path as a mouse.
    public sealed partial class ElementalAcceptance : MonoBehaviour
    {
        public string output;ElementalWorld world;GlyphNotebook notebook;FirstPersonMotor motor;Keyboard keyboard;Mouse mouse;
        [Serializable] public sealed class CheckResult{public string name;public bool passed;public double value,limit;public string detail;}
        [Serializable] public sealed class Receipt{public string status,engine,gpu,cpu,input="Automated Input System keyboard/mouse events; not a physical human playtest";public int width,height;public List<CheckResult> checks=new List<CheckResult>();public List<float> frameMs=new List<float>();public double averageFps,p95Ms,maxSimulationMs;}
        Receipt receipt=new Receipt();
        readonly Dictionary<string,double[]> routeMetrics=new Dictionary<string,double[]>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Launch()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-elementalAcceptance");if(i>=0&&i<args.Length-1)new GameObject("Explicit elemental acceptance").AddComponent<ElementalAcceptance>().output=Path.GetFullPath(args[i+1]);}
        void Start(){if(string.IsNullOrEmpty(output))return;Directory.CreateDirectory(output);StartCoroutine(Guard(Run()));}
        IEnumerator Guard(IEnumerator root)
        {
            var stack=new Stack<IEnumerator>();stack.Push(root);
            while(stack.Count>0)
            {
                object value=null;bool next=false;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}
                catch(Exception e){receipt.status="ERROR: "+e;Write();Debug.LogError(receipt.status);Finish(1);yield break;}
                if(!next){stack.Pop();continue;}if(value is IEnumerator nested)stack.Push(nested);else yield return value;
            }
            receipt.status=receipt.checks.All(c=>c.passed)?"PASSED":"FAILED";Write();Finish(receipt.status=="PASSED"?0:1);
        }
        void Finish(int code){world.input.SetPaused(true);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(mouse!=null)InputSystem.RemoveDevice(mouse);if(!Application.isEditor)Application.Quit(code);}
        void Write(){File.WriteAllText(Path.Combine(output,"elemental-acceptance.json"),JsonUtility.ToJson(receipt,true));}
        void Check(string name,bool passed,double value=0,double limit=0,string detail=""){receipt.checks.Add(new CheckResult{name=name,passed=passed,value=value,limit=limit,detail=detail});Debug.Log("ELEMENTAL_CHECK "+name+": "+passed+" "+value);Write();}
        IEnumerator Tap(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
        IEnumerator Click(string name)
        {
            var button=FindObjectsByType<Button>().FirstOrDefault(b=>b.name==name&&b.gameObject.activeInHierarchy);if(button==null)throw new InvalidOperationException("Missing visible UI button: "+name);
            var r=(RectTransform)button.transform;var position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return null;yield return null;InputSystem.QueueStateEvent(mouse,new MouseState{position=position,buttons=1});yield return null;yield return null;InputSystem.QueueStateEvent(mouse,new MouseState{position=position});yield return null;yield return null;
        }
        Vector2 ScreenPoint(Vector2 page)=>notebook.board.PageToScreen(page);
        IEnumerator Stroke(IReadOnlyList<Vector2> points)
        {
            var first=ScreenPoint(points[0]);InputSystem.QueueStateEvent(mouse,new MouseState{position=first});yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=first,buttons=1});yield return null;yield return null;
            for(int i=1;i<points.Count;i++){InputSystem.QueueStateEvent(mouse,new MouseState{position=ScreenPoint(points[i]),buttons=1});yield return null;}
            InputSystem.QueueStateEvent(mouse,new MouseState{position=ScreenPoint(points[points.Count-1])});yield return null;yield return null;
        }
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;yield return null;}
        void Place(Vector3 point,Vector3 aim)
        {var cc=motor.GetComponent<CharacterController>();cc.enabled=false;motor.transform.position=point;cc.enabled=true;Aim(aim);Physics.SyncTransforms();}
        void Aim(Vector3 point){var d=(point-motor.view.transform.position).normalized;motor.transform.rotation=Quaternion.Euler(0,Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,0);motor.cameraPivot.localRotation=Quaternion.Euler(-Mathf.Asin(d.y)*Mathf.Rad2Deg,0,0);}
        IEnumerator CloseGap()
        {
            yield return Click("5 OUTER SEAL");var points=new List<Vector2>{notebook.draft.seal.Last()};for(int i=1;i<=14;i++){float a=(338+42*i/14f)*Mathf.Deg2Rad;points.Add(new Vector2(Mathf.Cos(a)*.93f,Mathf.Sin(a)*.88f));}yield return Stroke(points);
        }
        IEnumerator OpenExample(string name)
        {if(!notebook.Open)yield return Tap(Key.G);yield return Click(name);}
        IEnumerator MeasureRoute(string name,int samples,float bend)
        {
            world.ResetArea("all");Place(new Vector3(-20,.2f,-14),new Vector3(-27,2,-8));motor.enabled=false;
            yield return OpenExample("WATER ROUTE");yield return Click("4 DIRECT PATH");
            var points=new List<Vector2>();for(int i=0;i<=samples;i++){float t=i/(float)samples;points.Add(new Vector2(-.7f+1.4f*t,-.15f+bend*Mathf.Sin(t*Mathf.PI)));}yield return Stroke(points);
            Check(name+" real canvas accepts arbitrary path",notebook.draft.trajectory.Count>12,notebook.draft.trajectory.Count);
            string id=notebook.draft.id;double before=world.Source("courtyard-water").Available;yield return CloseGap();Check(name+" outer gap commits via mouse",notebook.draft.committed&&world.IsControlling(id));
            if(!world.IsControlling(id))throw new InvalidOperationException("Cannot measure dormant route: "+world.Message);
            Aim(new Vector3(-23,2,-8));double max=0,sum=0;int count=0;string firstBody=null;double firstRelease=0;var trace=new List<string>{"step,body,x,y,z,desired_x,desired_y,desired_z,error"};long start=world.simulationSteps;
            while(world.simulationSteps-start<650)
            {
                yield return new WaitForFixedUpdate();foreach(var g in world.guided.Where(g=>g.castId==id))
                {var b=world.bodies.Find(b=>b.id==g.bodyId);if(firstBody==null)firstBody=b.id;var c=world.casts.Find(c=>c.id==id).channels[g.channel];var target=DrawnGeometry.At(c.path,g.distance,out _);double error=Vector3.Distance(b.Body.position,target);max=Math.Max(max,error);sum+=error;count++;if(world.simulationSteps%10==0)trace.Add($"{world.simulationSteps},{b.id},{b.Body.position.x:R},{b.Body.position.y:R},{b.Body.position.z:R},{target.x:R},{target.y:R},{target.z:R},{error:R}");}
                if(firstBody!=null&&firstRelease==0&&!world.guided.Any(g=>g.bodyId==firstBody))firstRelease=(world.simulationSteps-start)*ElementalWorld.PhysicsStep;
                if(world.simulationSteps-start==200||world.simulationSteps-start==400)yield return Capture(name+"-"+(world.simulationSteps-start));
            }
            File.WriteAllLines(Path.Combine(output,name+"-motion.csv"),trace);double transported=before-world.Source("courtyard-water").Available;double tolerance=.1241;
            Check(name+" feasible trajectory maximum error",count>0&&max<=tolerance,max,tolerance,"Average "+sum/Math.Max(1,count)+" m; 0.5 kg water packet diameter 0.1241 m");Check(name+" finite transport",Math.Abs(transported-8)<=.08,transported,8);
            var totals=world.MaterialTotals();Check(name+" complete material balance",Math.Abs(totals["courtyard-water"]-250)<=2.5,totals["courtyard-water"],250);
            routeMetrics[name]=new[]{transported,(double)world.casts.Find(c=>c.id==id).channels.First(c=>c.direct).length,firstRelease};
            world.Cancel(id);yield return Capture(name+"-release");
        }
        MatterBody Take(string source,double kg,Vector3 position){var lots=world.Source(source).Withdraw(kg);if(lots.Count!=1)throw new InvalidOperationException("Fixture material withdrawal failed");return world.Spawn(lots[0],position);}
        Construction Graph(Rune element,Vector3 origin,Vector3 target,Rune operation=Rune.Hold)
        {
            var c=new Construction{origin=origin,target=target,forward=Vector3.forward,scale=8,height=2,speed=4};var e=c.Add(element,new Vector2(-.5f,.2f));e.intensity=3;e.duration=3;var op=c.Add(operation,new Vector2(.3f,.2f));op.duration=3;c.Connect(e.id,op.id);for(int i=0;i<=96;i++){float a=i*Mathf.PI/48;c.seal.Add(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.95f);}return c;
        }
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);world=FindAnyObjectByType<ElementalWorld>();notebook=FindAnyObjectByType<GlyphNotebook>();motor=world.motor;world.saveOverride=Path.Combine(output,"acceptance-world.json");
            receipt.engine=Application.unityVersion;receipt.gpu=SystemInfo.graphicsDeviceName;receipt.cpu=SystemInfo.processorType;receipt.width=Screen.width;receipt.height=Screen.height;
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keyboard.MakeCurrent();mouse.MakeCurrent();Application.runInBackground=true;
            if(Environment.GetCommandLineArgs().Contains("-launchOnly")){yield return LaunchRevision();yield break;}
            if(Environment.GetCommandLineArgs().Contains("-compoundOnly")){yield return Compound();yield break;}
            if(Environment.GetCommandLineArgs().Contains("-compositionOnly")){yield return Composition();yield break;}
            if(Environment.GetCommandLineArgs().Contains("-bridgeOnly")){yield return Bridge();yield break;}
            if(Environment.GetCommandLineArgs().Contains("-boardsOnly")){yield return BoardsAndPatterns();yield break;}
            if(Environment.GetCommandLineArgs().Contains("-interactionsOnly")){yield return SecondaryInteractions();yield break;}
            yield return Capture("00-normal-start-menu");
            float ambientGain=ElementalFeedback.MusicGain,sfxGain=ElementalFeedback.SfxGain;world.feedback.SetMusicGain(0);world.feedback.SetSfxGain(0);
            Check("Audio gain controls mute ambient and event voices",world.feedback.GetComponentsInChildren<AudioSource>().Where(s=>s.name=="Courtyard air"||s.name.StartsWith("Elemental feedback")||s.name=="Notebook interface sound").All(s=>s.volume==0));
            world.feedback.SetMusicGain(ambientGain);world.feedback.SetSfxGain(sfxGain);
            Check("Pause menu exposes both audio sliders",FindObjectsByType<Slider>().Length==2);
            yield return Tap(Key.Escape);Check("Escape resumes normal menu",!world.input.Paused);
            var start=motor.transform.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSeconds(.35f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;Check("W input moves first-person controller",Vector3.Distance(start,motor.transform.position)>.5);
            yield return Tap(Key.G);Check("G opens actual notebook and pauses",notebook.Open&&world.input.Paused);yield return Capture("01-alphabet-and-guide");
            yield return Click("FREEHAND MODE");var definition=GlyphAlphabet.Get(Rune.Water);foreach(var stroke in definition.strokes)yield return Stroke(stroke.Select(p=>new Vector2(-.5f,.2f)+p*.06f).ToList());yield return Tap(Key.Enter);
            Check("Freehand water recognized through pointer input",notebook.selected!=null&&notebook.selected.rune==Rune.Water,notebook.selected?.confidence??0,.58);Check("Internal glyph loop does not cast or debit",world.casts.Count==0&&world.ledger.overhead==0);yield return Capture("02-freehand-recognition");
            yield return MeasureRoute("03-curve-A",45,.28f);yield return MeasureRoute("04-curve-B",45,-.28f);yield return MeasureRoute("05-curve-A-dense",90,.28f);
            var baseline=routeMetrics["03-curve-A"];var dense=routeMetrics["05-curve-A-dense"];double densityError=baseline.Select((v,i)=>Math.Abs(v-dense[i])/Math.Max(.001,Math.Abs(v))).Max();Check("Sampling density: mass, geometry and arrival within 2%",densityError<=.02&&baseline[2]>0,densityError,.02);
            Application.targetFrameRate=30;yield return MeasureRoute("05b-curve-A-30fps",45,.28f);Application.targetFrameRate=120;var low=routeMetrics["05b-curve-A-30fps"];double rateError=baseline.Select((v,i)=>Math.Abs(v-low[i])/Math.Max(.001,Math.Abs(v))).Max();Check("30 versus 120 FPS fixed-physics observables within 2%",rateError<=.02,rateError,.02);
            world.ResetArea("all");world.input.SetPaused(false);motor.enabled=false;var stone=Take("courtyard-stone",2,new Vector3(0,12,-20));stone.Body.linearDamping=0;stone.Body.linearVelocity=new Vector3(2,3,0);var p0=stone.Body.position;var v0=stone.Body.linearVelocity;long s0=world.simulationSteps;while(world.simulationSteps-s0<50)yield return new WaitForFixedUpdate();float elapsed=(world.simulationSteps-s0)*ElementalWorld.PhysicsStep;double ballistic=Vector3.Distance(stone.Body.position,p0+v0*elapsed+Physics.gravity*(.5f*elapsed*elapsed));Check("Released stone gravity trajectory",ballistic<=.02*2,ballistic,.04);
            var wallStone=Take("courtyard-stone",3,new Vector3(0,1.5f,2));wallStone.Body.linearVelocity=Vector3.forward*12;yield return new WaitForSeconds(.6f);Check("High-speed stone collides with lane wall",wallStone.Body.position.z<4.9,wallStone.Body.position.z,4.9);Place(new Vector3(-4,1,0),new Vector3(0,1.5f,5));yield return Capture("06-collision");
            // Active world snapshot is a replacement transaction, including original hinges.
            world.SaveWorld(notebook.draft);var totalsBefore=world.MaterialTotals();string saved=File.ReadAllText(world.SavePath);world.RestoreWorld(saved);Check("Snapshot preserves original wind rotor hinge",world.bodies.Find(b=>b.id=="Wind rotor").GetComponent<HingeJoint>()!=null);Check("Snapshot preserves total resources",totalsBefore.All(t=>Math.Abs(world.MaterialTotals()[t.Key]-t.Value)<1e-5));world.input.SetPaused(false);
            // Real air force against the built hinge, using the same route as visible motes.
            world.ResetArea("all");var wind=Graph(Rune.Air,new Vector3(-4,0,-12),new Vector3(-4,2.2f,-12),Rune.Direct);wind.trajectory=new List<Vector2>{new Vector2(-.5f,0),new Vector2(.5f,0)};wind.origin=new Vector3(-4,0,-12);wind.height=2.2f;wind.depth=0;wind.speed=10;world.CloseSeal(wind);yield return new WaitForSeconds(2);var rotor=world.bodies.Find(b=>b.id=="Wind rotor");Check("Air drives physical hinge mechanism",rotor.Body.angularVelocity.magnitude>.1,rotor.Body.angularVelocity.magnitude,.1);Place(new Vector3(-7,1,-17),rotor.Body.position);yield return Capture("07-air-rotor");
            world.ResetArea("all");var hot=Take("thermal-water",.5,new Vector3(15,2,-20));var fire=Graph(Rune.Fire,new Vector3(15,0,-22),hot.Body.position);world.CloseSeal(fire);hot.Body.constraints=RigidbodyConstraints.FreezePosition;yield return new WaitForSeconds(8);Check("Fire supplies latent heat before steam",hot.phase==MatterPhase.Steam,hot.enthalpy/hot.kg,2680000);hot.Body.constraints=RigidbodyConstraints.None;world.Cancel();Check("Steam remains material after release",hot.phase==MatterPhase.Steam&&hot.kg==.5);Place(new Vector3(12,1,-24),hot.Body.position);yield return Capture("08-steam");
            yield return Operators();yield return SecondaryInteractions();yield return Combustion();yield return Bridge();yield return ElectricAndLight();yield return BoardsAndPatterns();yield return Composition();yield return Compound();yield return LaunchRevision();yield return Performance();
            Check("Charge ledger closes",Math.Abs(world.ledger.Residual)<.01,world.ledger.Residual,.01);motor.enabled=true;world.input.SetPaused(true);yield return Capture("99-finished");
        }
        IEnumerator BoardsAndPatterns()
        {
            world.ResetArea("all");world.input.SetPaused(false);var board=FindObjectsByType<PlaygroundFixture>().First(b=>b.portable);var body=board.GetComponent<MatterBody>();Place(board.transform.position-Vector3.forward*3-Vector3.up,board.transform.position);motor.enabled=false;
            yield return Tap(Key.F);yield return new WaitForSeconds(.4f);Check("F picks up a physical portable board",PlaygroundFixture.Carried==board&&!body.Body.isKinematic);
            world.SaveWorld(notebook.draft);var saved=File.ReadAllText(world.SavePath);world.RestoreWorld(saved);world.input.SetPaused(false);Check("Save/load restores carried board and collider",PlaygroundFixture.Carried==board&&body.GetComponent<Collider>().enabled);
            yield return Tap(Key.F);Check("F releases board into ordinary physics",PlaygroundFixture.Carried==null&&!body.Body.isKinematic);
            world.ResetArea("all");Place(board.transform.position-Vector3.forward*3-Vector3.up,board.transform.position);yield return Tap(Key.E);Check("E opens notebook on board frame",notebook.Open&&notebook.draft.drawingSurfaceId==body.id);notebook.Show(false);
            var pattern=Graph(Rune.Light,body.Body.position,body.Body.position+Vector3.back*10,Rune.Form);pattern.form=new List<Vector2>{new Vector2(-.3f,-.2f),new Vector2(.3f,-.2f),new Vector2(.3f,.2f),new Vector2(-.3f,.2f),new Vector2(-.3f,-.2f)};pattern.vertical=true;pattern.scale=3;pattern.height=0;pattern.forward=Vector3.back;pattern.drawingSurfaceId=body.id;int events=world.feedback.PlayedEvents;Check("Board light pattern passes production validation",world.CloseSeal(pattern),detail:world.Message);yield return new WaitForSeconds(.2f);
            var beams=FindObjectsByType<LineRenderer>().Where(l=>l.name.StartsWith("Calculated route "+pattern.id+"light-")).ToArray();Check("Light FORM uses drawn emission geometry",beams.Length==8&&beams.Select(l=>l.GetPosition(0)).Distinct().Count()>=7,beams.Length,8);
            Check("Seal produces spatial audio event",world.feedback.PlayedEvents>events&&world.feedback.GetComponentsInChildren<AudioSource>().Any(s=>s.name.StartsWith("Elemental feedback")&&s.isPlaying&&s.spatialBlend==1));Check("Committed glyph ink attaches to drawing board",board.GetComponentsInChildren<LineRenderer>().Length>0);
            Place(new Vector3(-25,.4f,-25),board.transform.position);yield return Capture("16-portable-board-ink");world.Cancel();
        }
        IEnumerator SecondaryInteractions()
        {
            world.ResetArea("all");world.input.SetPaused(false);motor.enabled=false;
            var soil=Take("soil",1,new Vector3(-10,1,-23));var water=Take("courtyard-water",.5,soil.Body.position+Vector3.up*(soil.radius+.065f));soil.Body.constraints=water.Body.constraints=RigidbodyConstraints.FreezePosition;yield return new WaitForSeconds(.3f);
            Check("Water wets actual loose-soil contact",soil.wetness>0);Check("Wetting retains soil and water provenance",Math.Abs(world.MaterialTotals()["soil"]-150)<.01&&Math.Abs(world.MaterialTotals()["courtyard-water"]-250)<.01);
            soil.Body.constraints=water.Body.constraints=RigidbodyConstraints.None;var wind=Graph(Rune.Air,new Vector3(-10,0,-23),new Vector3(-10,1,-23),Rune.Direct);wind.vertical=false;wind.height=1;wind.scale=4;wind.speed=10;wind.depth=0;wind.trajectory=new List<Vector2>{new Vector2(-.5f,0),new Vector2(.5f,0)};world.CloseSeal(wind);yield return new WaitForSeconds(.5f);Check("Air transports water and wet sediment through force",water.Body.linearVelocity.x>.1&&soil.Body.linearVelocity.x>.05,water.Body.linearVelocity.x,.1);
            world.ResetArea("all");var rock=Take("courtyard-stone",2,new Vector3(-10,2,-23));rock.Body.constraints=RigidbodyConstraints.FreezePosition;var fire=Graph(Rune.Fire,new Vector3(-10,0,-23),rock.Body.position);world.CloseSeal(fire);yield return new WaitForSeconds(1);double heated=rock.enthalpy;Check("Fire heats existing stone without transmutation",heated>10000&&rock.kind==MatterKind.Stone,heated,10000);
            var frost=Graph(Rune.Frost,new Vector3(-10,0,-23),rock.Body.position);world.CloseSeal(frost);yield return new WaitForSeconds(.5f);Check("Frost and Fire combine through net heat transfer",rock.enthalpy<heated&&rock.kind==MatterKind.Stone,rock.enthalpy,heated);world.Cancel();rock.Body.constraints=RigidbodyConstraints.None;
            Place(new Vector3(-14,.3f,-27),rock.Body.position);yield return Capture("17-stone-heat-and-sediment");
        }
        Construction MaterialGraph(Rune element,string source,float amount,Vector3 target,int intensity=2)
        {
            var c=Graph(element,world.Source(source).Position,target,Rune.Gather);c.nodes[0].sourceId=source;c.nodes[0].intensity=intensity;c.nodes[1].width=amount;var hold=c.Add(Rune.Hold,new Vector2(.5f,-.3f));hold.duration=3;c.Connect(c.nodes[1].id,hold.id);return c;
        }
        IEnumerator Operators()
        {
            world.ResetArea("all");world.input.SetPaused(false);var c=MaterialGraph(Rune.Water,"courtyard-water",8,new Vector3(-24,4,-8));var hold=c.nodes.Last();c.nodes[1].position=new Vector2(-.2f,.2f);c.nodes.Remove(hold);c.edges.RemoveAll(e=>e.to==hold.id);var direct=c.Add(Rune.Direct,new Vector2(.1f,.2f));var split=c.Add(Rune.Split,new Vector2(.35f,.2f));var left=c.Add(Rune.End,new Vector2(.65f,.4f));var right=c.Add(Rune.End,new Vector2(.65f,-.3f));c.Connect(c.nodes[1].id,direct.id);c.Connect(direct.id,split.id);c.Connect(split.id,left.id);c.Connect(split.id,right.id);
            var collector=world.Source("collection");var delta=collector.Position-world.Source("courtyard-water").Position;var horizontal=Vector3.ProjectOnPlane(delta,Vector3.up);c.forward=Vector3.Cross(horizontal.normalized,Vector3.up);c.vertical=true;c.scale=40;c.speed=8;c.trajectory=new List<Vector2>{Vector2.zero,new Vector2(horizontal.magnitude/40,delta.y/40)};
            Check("Explicit split seal accepted",world.CloseSeal(c));yield return new WaitForSeconds(14);
            double emitted=world.casts.Last().channels.Sum(ch=>ch.emitted);Check("Split divides gathered quantity without duplication",Math.Abs(emitted-8)<.08,emitted,8);
            double charge=world.ledger.remaining;Check("Repeat seal cannot debit twice",!world.CloseSeal(c)&&world.ledger.remaining==charge);world.Cancel();
            // Both branches physically traverse the courtyard, release over the basin,
            // fall into its trigger, stop against its floor, and transfer into its ledger.
            Check("Collected split material available for reuse",Math.Abs(collector.Available-8)<.08,collector.Available,8);var reuse=MaterialGraph(Rune.Water,"collection",4,collector.Position+Vector3.up*2);world.CloseSeal(reuse);yield return new WaitForSeconds(4.1f);Check("Reuse retains all source provenance",Math.Abs(world.MaterialTotals()["courtyard-water"]-250)<.01,world.MaterialTotals()["courtyard-water"],250);
            world.ledger.Pay(world.ledger.remaining*ChargeLedger.JoulesPerCharge,"overhead");yield return new WaitForFixedUpdate();Check("Charge depletion releases control without deleting mass",!world.casts.Any(x=>x.active)&&Math.Abs(world.MaterialTotals()["courtyard-water"]-250)<.01);
            world.ResetArea("all");var heavy=MaterialGraph(Rune.Stone,"courtyard-stone",100,new Vector3(-10,8,-8),1);world.CloseSeal(heavy);yield return new WaitForSeconds(5);var bodies=world.bodies.Where(b=>b.owner==heavy.id).ToArray();Check("Insufficient total force cannot lift a heavy construction",bodies.Length>0&&bodies.Average(b=>b.Body.position.y)<3,bodies.Length>0?bodies.Average(b=>b.Body.position.y):0,3);world.Cancel();
            foreach(var option in new[]{new Vector3(0,1,0),new Vector3(0,-1,0),new Vector3(1,0,0)})
            {
                world.ResetArea("all");var orbit=GlyphExamples.Create("Stone orbit",new Vector3(-25,0,22),Vector3.forward,world.sources);orbit.origin=new Vector3(-25,0,22);orbit.target=orbit.origin+Vector3.up*3;orbit.height=3;orbit.vertical=option.x!=0;orbit.forward=option.x!=0?Vector3.right:Vector3.forward;orbit.nodes.First(n=>n.rune==Rune.Spin).axis=option;orbit.seal=Graph(Rune.Air,Vector3.zero,Vector3.zero).seal;world.CloseSeal(orbit);yield return new WaitForSeconds(7);
                var moving=world.bodies.Where(b=>b.owner==orbit.id).ToArray();double momentum=moving.Sum(b=>Vector3.Dot(Vector3.Cross(b.Body.position-orbit.target,b.Body.linearVelocity),option)*b.kg);Check("Spin follows signed axis "+option,momentum>2,momentum,2);var velocities=moving.ToDictionary(b=>b.id,b=>b.Body.linearVelocity);world.Cancel(orbit.id);Check("Spin release preserves tangent velocity "+option,moving.All(b=>Vector3.Distance(b.Body.linearVelocity,velocities[b.id])<1e-6));Place(new Vector3(-25,1,17),orbit.target);yield return Capture("spin-"+option.x+"-"+option.y);yield return new WaitForSeconds(.4f);
            }
        }
        IEnumerator Combustion()
        {
            world.ResetArea("all");world.input.SetPaused(false);var fuel=world.bodies.Find(b=>b.id=="Fuel log 0");var fire=Graph(Rune.Fire,new Vector3(17,0,-20),fuel.Body.position);world.CloseSeal(fire);yield return new WaitForSeconds(3);Check("Fuel ignites through heat input",fuel.burning&&fuel.fuelKg<1.4,fuel.fuelKg,1.4);world.Cancel();yield return new WaitForSeconds(.3f);Check("Fuel can continue after spell release",fuel.burning);var water=Take("thermal-water",8,fuel.Body.position+Vector3.up*.65f);yield return new WaitForSeconds(1);Check("Water contact cools and extinguishes fuel",!fuel.burning,fuel.Temperature,160);
            var vortex=GlyphExamples.Create("Flame vortex",new Vector3(18,0,-24),Vector3.forward,world.sources);vortex.target=new Vector3(18,3,-24);vortex.seal=Graph(Rune.Air,Vector3.zero,Vector3.zero).seal;world.CloseSeal(vortex);yield return new WaitForSeconds(1);var gas=world.Capture(notebook.draft).motes.Where(m=>m.hot&&m.heat>0).ToArray();double angular=gas.Length>0?gas.Average(m=>Vector3.Cross(m.p-vortex.target,m.v).y):0;Check("Air vortex transports authoritative hot gas",gas.Length>0&&angular>.005,angular,.005);Place(new Vector3(15,1,-29),vortex.target);yield return Capture("flame-vortex");world.Cancel();
        }
        IEnumerator Bridge()
        {
            world.ResetArea("all");Place(new Vector3(24,.2f,4),new Vector3(30,0,10));motor.enabled=false;yield return OpenExample("ICE BRIDGE");yield return CloseGap();Check("Editable bridge seal accepted",notebook.draft.committed);
            Place(new Vector3(25,1.3f,5),new Vector3(30,.2f,10));yield return new WaitForSeconds(44);var ice=world.bodies.Where(b=>b.kind==MatterKind.Water&&b.phase==MatterPhase.Ice).ToArray();int required=world.casts.Last().channels.First(c=>c.element==Rune.Water).cells.Count;Check("Every finite water cell becomes collidable sheet ice",ice.Length==required&&required>0,ice.Length,required);File.WriteAllText(Path.Combine(output,"bridge-world.json"),JsonUtility.ToJson(world.Capture(notebook.draft),true));yield return Capture("09-ice-bridge");world.Cancel();yield return new WaitForSeconds(.5f);
            Place(new Vector3(26,.3f,10),new Vector3(34,.3f,10));motor.enabled=true;motor.transform.rotation=Quaternion.Euler(0,90,0);InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSeconds(2.1f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Check("First-person walks released ice bridge",motor.transform.position.x>33&&motor.transform.position.y>-.2,motor.transform.position.x,33,"Final position "+motor.transform.position);motor.enabled=false;yield return Capture("10-bridge-crossing");
            // Localized physical thaw breaks existing bonds, not deletion on cancellation.
            foreach(var b in ice.Where(b=>Mathf.Abs(b.Body.position.x-30)<1))b.AddHeat(b.kg*500000);yield return new WaitForSeconds(1);Check("Thawed bridge cells lose bonds and fall",ice.Any(b=>b.phase==MatterPhase.Liquid&&!b.bridge));yield return Capture("11-thaw");
        }
        IEnumerator ElectricAndLight()
        {
            world.ResetArea("all");world.input.SetPaused(false);var c=Graph(Rune.Lightning,new Vector3(30,0,-35),new Vector3(40,1,-35));world.CloseSeal(c);yield return new WaitForSeconds(.2f);var receiver=world.posts.Find(p=>p.receiver);double blocked=receiver.receivedJ;Check("Insulated barrier blocks direct lightning",blocked==0);
            // Finite water detour physically goes around the insulating slab.
            foreach(var p in new[]{new Vector3(34,1,-36.2f),new Vector3(35.5f,1,-36.2f),new Vector3(37,1,-36.2f)}){var b=Take("conductive-water",1,p);b.Body.constraints=RigidbodyConstraints.FreezePosition;}
            yield return new WaitForSeconds(.5f);Check("Water detour changes route and powers receiver",receiver.receivedJ>blocked&&world.LastDischargeRoute.Count>=5,receiver.receivedJ,0,world.LastDischargeDiagnostics);Place(new Vector3(33,1,-40),new Vector3(36,1,-35));yield return Capture("12-conductive-detour");world.Cancel();
            Place(new Vector3(0,.2f,36),new Vector3(0,2,46));var light=Graph(Rune.Light,motor.transform.position,new Vector3(0,2,46));world.CloseSeal(light);yield return new WaitForSeconds(.2f);var beam=FindObjectsByType<LineRenderer>().FirstOrDefault(l=>l.name=="Calculated route "+light.id+"light");Check("Light route stops at real occluder",beam!=null&&beam.positionCount>1&&beam.GetPosition(beam.positionCount-1).z<41);yield return Capture("13-occluded-light");
            var occluder=world.bodies.Find(b=>b.id=="Light occluder");occluder.Body.position=new Vector3(5,1.5f,40);Physics.SyncTransforms();yield return new WaitForSeconds(.2f);Check("Moving blocker changes illuminated surface",beam!=null&&beam.GetPosition(beam.positionCount-1).z>45);yield return Capture("14-light-surface");world.Cancel();
            var rayStart=light.origin+Vector3.up*1.7f;var direction=(light.target-rayStart).normalized;var droplet=Take("optics-water",1,rayStart+direction*6+Vector3.right*.03f);droplet.Body.constraints=RigidbodyConstraints.FreezePosition;light.id=Guid.NewGuid().ToString("N");light.committed=false;world.CloseSeal(light);yield return new WaitForSeconds(.2f);var refracted=FindObjectsByType<LineRenderer>().FirstOrDefault(l=>l.name=="Calculated route "+light.id+"light");Check("Water changes the calculated optical route",refracted!=null&&refracted.positionCount>=3&&Math.Abs(refracted.GetPosition(refracted.positionCount-1).x)>.1);yield return Capture("14b-water-refraction");world.Cancel();
        }
        IEnumerator Performance()
        {
            world.ResetArea("all");world.input.SetPaused(false);Place(new Vector3(-20,1,-15),new Vector3(-24,2,-8));motor.enabled=false;
            var a=Graph(Rune.Air,new Vector3(-24,0,-8),new Vector3(-24,2,-8),Rune.Spin);a.nodes[1].spin=1;world.CloseSeal(a);
            var f=Graph(Rune.Fire,new Vector3(-24,0,-8),new Vector3(-24,2,-8));world.CloseSeal(f);
            var w=GlyphExamples.Create("Water route",new Vector3(-24,0,-8),Vector3.forward,world.sources);w.seal=Graph(Rune.Water,Vector3.zero,Vector3.zero).seal;w.nodes.Last().rune=Rune.Hold;w.nodes.Last().duration=3;world.CloseSeal(w);
            yield return new WaitForSeconds(3);Check("Three simultaneous constructions active",world.casts.Count(c=>c.active)>=3,world.casts.Count(c=>c.active),3);
            for(int i=0;i<600;i++){yield return null;receipt.frameMs.Add(Time.unscaledDeltaTime*1000);receipt.maxSimulationMs=Math.Max(receipt.maxSimulationMs,world.lastStepMs);}
            receipt.averageFps=1000/receipt.frameMs.Average();var sorted=receipt.frameMs.OrderBy(x=>x).ToArray();receipt.p95Ms=sorted[(int)(sorted.Length*.95)];Check("1080p representative average 60 FPS",Screen.width==1920&&Screen.height==1080&&receipt.averageFps>=60,receipt.averageFps,60);yield return Capture("15-three-constructions");world.Cancel();
        }
    }
}
