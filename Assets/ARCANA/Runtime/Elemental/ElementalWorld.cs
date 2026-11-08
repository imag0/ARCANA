using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arcana.Elemental
{
    [Serializable] public sealed class ChargeLedger
    {
        public const double JoulesPerCharge=10000;
        public double initial=100000,external,remaining=100000,drive,heat,cooling,overhead,loss,discharge,light;
        public double[] Accounts()=>new[]{initial,external,remaining,drive,heat,cooling,overhead,loss,discharge,light};
        public void RestoreExact(long[] bits)
        {
            if(bits==null)return;if(bits.Length!=10)throw new ArgumentException("Invalid exact charge ledger");var values=bits.Select(BitConverter.Int64BitsToDouble).ToArray();var decimalValues=Accounts();
            for(int i=0;i<values.Length;i++)if(double.IsNaN(values[i])||double.IsInfinity(values[i])||double.IsNaN(decimalValues[i])||double.IsInfinity(decimalValues[i])||Math.Abs(values[i]-decimalValues[i])>8e-15*Math.Max(1,Math.Abs(values[i])))throw new ArgumentException("Charge representations disagree");
            initial=values[0];external=values[1];remaining=values[2];drive=values[3];heat=values[4];cooling=values[5];overhead=values[6];loss=values[7];discharge=values[8];light=values[9];
        }
        public double Residual=>(initial+external-remaining)*JoulesPerCharge-drive-heat-cooling-overhead-loss-discharge-light;
        public double Pay(double joules,string category)
        {
            double actual=Math.Min(Math.Max(0,joules),remaining*JoulesPerCharge);remaining-=actual/JoulesPerCharge;
            switch(category){case "drive":drive+=actual;break;case "heat":heat+=actual;break;case "cooling":cooling+=actual;break;case "discharge":discharge+=actual;break;case "light":light+=actual;break;case "loss":loss+=actual;break;default:overhead+=actual;break;}return actual;
        }
    }
    [Serializable] public sealed class Channel
    {
        public Rune element;public string sourceId,targetId;public float fraction=1,amount,emitted,rate=2,width=.3f,speed=3,force=250,power=50000,duration=12,spin;
        public bool form,direct,hold,spread,tracking,attached,end,bodySpin,worldPath,explicitPower,assemble,volume,ended;public int formNode;public int combine=-1,launchGroup=-1,assemblyGroup=-1;public Vector3 assemblyCenter;public float formationRadius,frontDepth,frontDown;public float launchRange=40,requiredMass;public Vector3 axis=Vector3.up;
        public List<Vector3> path=new List<Vector3>(),shape=new List<Vector3>();public Vector3 anchor;public float length;
        public List<Vector3> cells=new List<Vector3>();public Vector3 cellSize;public Quaternion cellRotation=Quaternion.identity;
        public Channel Copy()=>JsonUtility.FromJson<Channel>(JsonUtility.ToJson(this));
    }
    [Serializable] public sealed class CastState
    {
        public string id;public Construction drawing;public List<Channel> channels=new List<Channel>();public List<LaunchState> launches=new List<LaunchState>();public float elapsed,emitClock;public bool active=true;public string status="Active";
    }
    [Serializable] public sealed class GuidedState
    {
        public string bodyId,castId;public int channel,formStage;public float distance,age;public Vector3 offset,heldPoint,entry;
    }
    public sealed partial class ElementalWorld : MonoBehaviour
    {
        public LabInput input;public FirstPersonMotor motor;public Material bodyMaterial,trailMaterial,effectMaterial;public Transform annexRoot;
        public List<MaterialSource> sources=new List<MaterialSource>();public List<ConductivePost> posts=new List<ConductivePost>();
        public ChargeLedger ledger=new ChargeLedger();public List<CastState> casts=new List<CastState>();public List<GuidedState> guided=new List<GuidedState>();
        public readonly List<MatterBody> bodies=new List<MatterBody>();public const int BodyLimit=192,CastLimit=8;public const float PhysicsStep=1f/100;
        public string Message{get;private set;}="G opens your glyph notebook. Choose elements, connect operators, draw a route, close the seal.";
        public double ambientHeatJ,fuelReleasedJ;public long simulationSteps;public double lastStepMs;public event Action Changed;
        public ElementalFeedback feedback;
        Queue<MatterBody> pool=new Queue<MatterBody>();Dictionary<string,BodyState> fixtureStates=new Dictionary<string,BodyState>();
        readonly Dictionary<string,MatterBody> fixtures=new Dictionary<string,MatterBody>();
        readonly Dictionary<string,string> fixtureAreas=new Dictionary<string,string>();
        public void Say(string message){Message=message;Changed?.Invoke();}
        void Awake()
        {
            Time.fixedDeltaTime=PhysicsStep;Physics.defaultSolverIterations=10;Physics.defaultSolverVelocityIterations=4;
            sources=FindObjectsByType<MaterialSource>().ToList();posts=FindObjectsByType<ConductivePost>().ToList();
            foreach(var body in FindObjectsByType<MatterBody>()){body.world=this;if(string.IsNullOrEmpty(body.id))body.id=Guid.NewGuid().ToString("N");body.Refresh();bodies.Add(body);fixtureStates[body.id]=body.Capture();fixtures[body.id]=body;fixtureAreas[body.id]=sources.OrderBy(s=>Vector3.Distance(s.Position,body.transform.position)).FirstOrDefault()?.area;}
        }
        void Start(){gameObject.AddComponent<FluidSurface>().world=this;feedback=gameObject.AddComponent<ElementalFeedback>();feedback.world=this;gameObject.AddComponent<WorldGlyphInk>().world=this;input.PauseChanged+=Paused;Paused(input.Paused);}
        void Paused(bool paused){Time.timeScale=paused?0:1;AudioListener.pause=paused;}
        void OnDestroy(){if(input!=null)input.PauseChanged-=Paused;Time.timeScale=1;AudioListener.pause=false;}
        public void RegisterFixture(MatterBody body,string area){if(fixtures.ContainsKey(body.id))return;body.world=this;body.Refresh();bodies.Add(body);fixtures[body.id]=body;fixtureStates[body.id]=body.Capture();fixtureAreas[body.id]=area;}
        public MaterialSource Source(string id)=>sources.Find(s=>s.stableId==id);
        public bool IsControlling(string id)=>casts.Any(c=>c.id==id&&c.active);
        public List<Channel> Evaluate(Construction drawing,GlyphPlan plan)
        {
            var output=new Dictionary<int,List<Channel>>();
            foreach(var node in plan.order)
            {
                List<Channel> channels;
                if(GlyphPlan.Element(node.rune))channels=new List<Channel>{new Channel{element=node.rune,sourceId=node.sourceId,targetId=node.targetId,rate=2*node.intensity,force=node.intensity==1?250:node.intensity==2?2500:10000,power=node.intensity==1?50000:node.intensity==2?150000:500000,duration=node.duration==1?4:node.duration==2?12:45,speed=drawing.speed,anchor=drawing.target}};
                else
                {
                    channels=new List<Channel>();foreach(var edge in drawing.edges.Where(e=>e.to==node.id))
                    {
                        int count=drawing.edges.Count(e=>e.from==edge.from);bool divided=drawing.nodes.Find(n=>n.id==edge.from).rune==Rune.Split;
                        foreach(var source in output[edge.from]){var clone=source.Copy();if(divided){clone.fraction/=count;clone.amount/=count;clone.rate/=count;clone.power/=count;clone.force/=count;}channels.Add(clone);}
                    }
                    foreach(var channel in channels)switch(node.rune)
                    {
                        case Rune.Gather:channel.amount=node.width*channel.fraction;channel.rate=channel.amount/4;break;
                        case Rune.Form:channel.form=true;channel.formNode=node.id;channel.shape=drawing.Geometry(node);if(node.sketch.Count>1)channel.anchor=channel.shape.Aggregate(Vector3.zero,(a,p)=>a+p)/channel.shape.Count;break;
                        case Rune.Direct:channel.direct=true;channel.worldPath=node.worldPath;channel.path=drawing.Geometry(node);break;
                        case Rune.Spin:channel.spin=node.spin*2;channel.axis=node.axis.normalized;channel.bodySpin=node.attached;break;
                        case Rune.Hold:channel.hold=true;channel.attached=node.attached;channel.tracking=node.tracking;channel.duration=node.duration==1?4:node.duration==2?12:45;break;
                        case Rune.Spread:channel.spread=true;channel.width=Mathf.Clamp(node.width,.05f,50);break;
                        case Rune.Combine:case Rune.Junction:channel.combine=node.id;break;
                        case Rune.Target:channel.targetId=node.targetId;channel.tracking=node.tracking;channel.anchor=drawing.target;break;
                        case Rune.End:channel.end=true;break;
                        case Rune.Assemble:channel.assemble=true;channel.assemblyGroup=node.id;break;
                        case Rune.Launch:channel.assemble=true;channel.launchGroup=node.id;channel.launchRange=node.width;channel.speed=drawing.launchSpeed;break;
                    }
                }
                if(node.rune==Rune.Combine||node.rune==Rune.Junction)
                {
                    var localShape=channels.FirstOrDefault(c=>c.form);
                    if(localShape!=null)foreach(var c in channels.Where(c=>!c.form)){c.form=true;c.formNode=localShape.formNode;c.shape=new List<Vector3>(localShape.shape);c.anchor=localShape.anchor;}
                }
                foreach(var channel in channels)
                {
                    if(node.forceN>0)channel.force=node.forceN*channel.fraction;
                    if(node.powerW>0){channel.power=node.powerW*channel.fraction;channel.explicitPower=true;}
                    if(node.seconds>0)channel.duration=node.seconds;
                    if(node.flowSpeed>0)channel.speed=node.flowSpeed;
                }
                if(channels.Count>128)throw new InvalidOperationException("Graph expansion exceeds 128 channels. Reduce repeated split/merge branches.");
                output[node.id]=channels;
            }
            var terminals=plan.order.Where(n=>!drawing.edges.Any(e=>e.from==n.id));var result=terminals.SelectMany(n=>output[n.id]).ToList();
            foreach(var group in result.Where(c=>c.combine>=0).GroupBy(c=>c.combine))
            {
                var shaped=group.FirstOrDefault(c=>c.form);if(shaped!=null)foreach(var c in group.Where(c=>!c.form)){c.form=true;c.formNode=shaped.formNode;c.shape=new List<Vector3>(shaped.shape);c.anchor=shaped.anchor;}
            }
            foreach(var channel in result)
            {
                if(channel.element==Rune.Frost&&!channel.explicitPower)channel.power=channel.power>=500000?16000000:channel.power>=150000?500000:50000;
                var source=Source(channel.sourceId);
                if(channel.direct&&!channel.worldPath&&channel.path.Count>1&&source!=null)
                {var offset=source.Position-channel.path[0];for(int i=0;i<channel.path.Count;i++)channel.path[i]+=offset;}
                if(channel.path.Count==0)channel.path.Add(channel.form&&channel.shape.Count>0?channel.shape[0]:channel.anchor);
                channel.length=DrawnGeometry.Length(channel.path);if(channel.amount<=0)channel.amount=channel.rate*Mathf.Min(channel.duration,4);if(channel.formNode!=0)BuildFormCells(drawing,drawing.nodes.Find(n=>n.id==channel.formNode),channel);
            }
            SizeChannels(drawing,result);PlaceLaunchChannels(drawing,result);return result;
        }
        void BuildFormCells(Construction drawing,GlyphNode node,Channel channel)
        {
            var outline=drawing.Sketch(node);channel.cells.Clear();channel.cellRotation=drawing.Frame(node)*Quaternion.Euler(-90,0,0);
            if(node.fillMode==3){BuildVolumeCells(drawing,node,channel,outline);return;}
            if(node.fillMode==1||node.fillMode==0&&channel.element!=Rune.Water||outline.Count<4||Vector2.Distance(outline[0],outline[outline.Count-1])>.08f)return;
            // A closed polygon supplies the occupied region, never a named construction.
            float minX=outline.Min(p=>p.x),maxX=outline.Max(p=>p.x),minY=outline.Min(p=>p.y),maxY=outline.Max(p=>p.y),scale=drawing.scale*node.geometryScale;
            float width=(maxX-minX)*scale,depth=(maxY-minY)*scale;if(width<.02f||depth<.02f)return;
            int budget=node.fillMode==0?32:48;
            int nx=Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(budget*width/depth)),1,budget),nz=Mathf.Clamp(budget/nx,1,budget);float sx=width/nx,sz=depth/nz;
            var rotation=drawing.Frame(node);var center=drawing.Map(Vector2.zero,true)+node.geometryOffset;
            for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)
            {
                var p=new Vector2(Mathf.Lerp(minX,maxX,(x+.5f)/nx),Mathf.Lerp(minY,maxY,(z+.5f)/nz));
                if(DrawnGeometry.Contains(outline,p))channel.cells.Add(center+rotation*new Vector3(p.x*scale,p.y*scale,DrawnGeometry.Profile(node.depthProfile,(p.x-minX)/(maxX-minX))*node.profileDepth));
            }
            if(channel.cells.Count==0)return;
            float density=channel.element==Rune.Water?1000:2400;
            channel.cellSize=new Vector3(sx,(channel.element==Rune.Water||channel.element==Rune.Stone)?channel.amount/(density*channel.cells.Count*sx*sz):Mathf.Max(.1f,channel.width),sz);
            channel.cellRotation=rotation*Quaternion.Euler(-90,0,0);
        }
        public bool Validate(Construction drawing,out GlyphPlan plan,out List<Channel> channels,out string reason)
        {
            plan=GlyphPlan.Compile(drawing);channels=new List<Channel>();reason=plan.reason;if(!plan.valid)return false;
            if(drawing.committed){reason="This seal already committed. Reopen to create a fresh draft.";return false;}
            if(casts.Count(c=>c.active)>=CastLimit){reason="Capacity: eight active seals. End one before casting.";return false;}
            if(ledger.remaining<2){reason="Not enough charge for the two-charge initial seal cost.";return false;}
            try{channels=Evaluate(drawing,plan);}catch(InvalidOperationException e){reason=e.Message;return false;}if(channels.Count>128){reason="Capacity: at most 128 output channels.";return false;}var needed=new Dictionary<string,double>();
            foreach(var channel in channels)
            {
                if(channel.element==Rune.Water||channel.element==Rune.Stone)
                {
                    if(channel.requiredMass>channel.amount+.01f){reason="Volume needs "+channel.requiredMass.ToString("F1")+" kg. Increase GATHER mass or reduce the drawn profile / scale.";return false;}
                    var source=Source(channel.sourceId);if(source==null){reason=channel.element+" needs a selected finite source.";return false;}
                    if(channel.element==Rune.Water&&source.kind!=MatterKind.Water||channel.element==Rune.Stone&&source.kind!=MatterKind.Stone&&source.kind!=MatterKind.Soil){reason="Selected material does not match the element.";return false;}
                    if(Vector3.Distance(channel.launchGroup>=0?motor.transform.position:drawing.origin,source.Position)>55){reason=source.label+" is beyond 55 m. Move closer or select a nearer source.";return false;}
                    if(!needed.ContainsKey(source.stableId))needed[source.stableId]=0;needed[source.stableId]+=channel.amount;
                }
                if(channel.path.Concat(channel.shape).Any(p=>Vector3.Distance(channel.launchGroup>=0?motor.transform.position:drawing.origin,p)>100)||Vector3.Distance(channel.launchGroup>=0?motor.transform.position:drawing.origin,channel.anchor)>100){reason="A shape, path or target exceeds the 100 m seal reach.";return false;}
                if(!string.IsNullOrEmpty(channel.targetId)&&!bodies.Any(b=>b.id==channel.targetId)&&!posts.Any(p=>p.stableId==channel.targetId)){reason="Marked target no longer exists. Select it again.";return false;}
            }
            foreach(var amount in needed)
            {
                double reserved=casts.Where(c=>c.active).SelectMany(c=>c.channels).Where(c=>c.sourceId==amount.Key&&(c.element==Rune.Water||c.element==Rune.Stone)).Sum(c=>Math.Max(0,c.amount-c.emitted));
                if(Source(amount.Key).Available-reserved+1e-6<amount.Value){reason="Not enough unreserved material in "+Source(amount.Key).label+": requested "+amount.Value.ToString("F1")+" kg, available "+Math.Max(0,Source(amount.Key).Available-reserved).ToString("F1")+" kg. Use SMALLER, choose another source, or reset its area.";return false;}
            }
            int Packets(Channel c)=>c.element!=Rune.Water&&c.element!=Rune.Stone?0:Mathf.CeilToInt(Mathf.Max(0,c.amount-c.emitted)/(c.cells.Count>0?c.amount/c.cells.Count:Mathf.Max(.5f,c.amount/48))-.001f);
            int required=bodies.Count+channels.Sum(Packets)+casts.Where(c=>c.active).Sum(c=>c.channels.Sum(Packets));
            if(required>BodyLimit){reason="Material capacity: this graph and active reservations need "+required+" / "+BodyLimit+" bodies. Release and collect material, or simplify a volume.";return false;}
            if(!LaunchSpaceClear(channels,out reason))return false;
            reason="Ready · initial 2 charge · sustain + measured work · "+channels.Count+" output branches";return true;
        }
        public bool CloseSeal(Construction drawing)
        {
            if(!DrawnGeometry.ClosedSeal(drawing.seal,drawing.nodes,out string reason)){Say(reason);return false;}
            if(!DrawnGeometry.NestedSealsValid(drawing,out reason)){Say("Dormant: "+reason);return false;}
            drawing.sealClosed=true;if(!Validate(drawing,out _,out var channels,out reason)){Say("Dormant: "+reason);return false;}
            ledger.Pay(2*ChargeLedger.JoulesPerCharge,"overhead");drawing.committed=true;
            casts.Add(new CastState{id=drawing.id,drawing=drawing.Copy(),channels=channels});feedback?.Cast(drawing.origin+Vector3.up);Say("Seal active. X releases control; material and momentum remain.");return true;
        }
        public void Cancel(string id=null)
        {
            if(casts.Any(c=>c.active&&(id==null||c.id==id)))feedback?.Release(motor.transform.position+Vector3.up);
            foreach(var cast in casts)if(id==null||cast.id==id){cast.active=false;cast.status="Released";}
            foreach(var body in bodies)if(id==null||body.owner==id){body.owner="";body.Refresh();}
            guided.RemoveAll(g=>id==null||g.castId==id);Say("Control ended. Released matter keeps its velocity and physical state.");
        }
        public MatterBody Spawn(MaterialLot lot,Vector3 position,string owner="",bool slab=false)
        {
            if(bodies.Count>=BodyLimit)return null;
            MatterBody body;
            if(pool.Count>0){body=pool.Dequeue();body.gameObject.SetActive(true);}else
            {var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Authoritative material packet";go.GetComponent<Renderer>().sharedMaterial=bodyMaterial;go.AddComponent<Rigidbody>();body=go.AddComponent<MatterBody>();}
            body.world=this;body.id=Guid.NewGuid().ToString("N");body.origin=lot.origin;body.kind=lot.kind;body.kg=lot.kg;body.enthalpy=lot.enthalpy;body.owner=owner;body.wetness=0;body.electricalJ=0;body.burning=false;body.bridge=false;
            body.sheetSize=Vector3.zero;body.fractured=false;body.SetSheet(Vector3.zero);
            body.radius=Mathf.Max(.06f,Mathf.Pow((float)lot.kg/(lot.kind==MatterKind.Water?1000:2400)*3/(4*Mathf.PI),1f/3));
            body.Body.interpolation=RigidbodyInterpolation.None;body.Body.constraints=RigidbodyConstraints.None;body.transform.localScale=Vector3.one*body.radius*2;body.transform.SetPositionAndRotation(position,Quaternion.identity);body.Body.position=position;body.Body.rotation=Quaternion.identity;body.Body.linearVelocity=Vector3.zero;body.Body.angularVelocity=Vector3.zero;body.Body.detectCollisions=true;body.Body.isKinematic=false;body.Refresh();bodies.Add(body);return body;
        }
        public void Retire(MatterBody body){if(!bodies.Remove(body))return;guided.RemoveAll(g=>g.bodyId==body.id);foreach(var joint in body.GetComponents<FixedJoint>())Destroy(joint);body.kg=0;body.Body.detectCollisions=false;body.gameObject.SetActive(false);if(!fixtures.ContainsKey(body.id))pool.Enqueue(body);}
        MatterBody RestoreBody(BodyState state)
        {
            MatterBody b;
            if(fixtures.TryGetValue(state.id,out b)){b.gameObject.SetActive(true);if(!bodies.Contains(b))bodies.Add(b);}
            else b=Spawn(new MaterialLot{origin=state.origin,kind=state.kind,kg=state.kg,enthalpy=state.enthalpy},state.position,state.owner);
            b.id=state.id;b.origin=state.origin;b.owner=state.owner;b.kind=state.kind;b.kg=state.kg;b.enthalpy=state.enthalpy;b.wetness=state.wetness;b.electricalJ=state.electricalJ;b.fuelKg=state.fuelKg;b.burning=state.burning;b.bridge=false;b.radius=state.radius;
            b.sheetSize=state.sheetSize;b.Body.interpolation=RigidbodyInterpolation.None;b.transform.localScale=state.scale;b.transform.SetPositionAndRotation(state.position,state.rotation);b.Body.position=state.position;b.Body.rotation=state.rotation;b.Body.detectCollisions=true;b.Body.isKinematic=false;b.Body.linearVelocity=state.velocity;b.Body.angularVelocity=state.angularVelocity;b.Body.isKinematic=state.kinematic;b.Refresh();b.fractured=state.fractured;return b;
        }
        void FixedUpdate(){if(input.Paused)return;Step(Time.fixedDeltaTime);}
        public void Step(float dt)
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();simulationSteps++;
            foreach(var cast in casts)
            {
                if(!cast.active)continue;if(cast.channels.All(c=>c.ended)){Cancel(cast.id);continue;}cast.elapsed+=dt;
                UpdateLaunches(cast,dt);
                if(ledger.remaining<=1e-9||cast.elapsed>cast.channels.Max(c=>c.duration)){Cancel(cast.id);cast.status="Charge/duration ended";continue;}
                ledger.Pay(dt*cast.channels.Count*40,"overhead");
                for(int ci=0;ci<cast.channels.Count;ci++)
                {
                    var channel=cast.channels[ci];if(channel.ended||cast.elapsed>channel.duration)continue;
                    if(channel.element==Rune.Water||channel.element==Rune.Stone)
                    {
                        float due=Mathf.Min(channel.amount,cast.elapsed*channel.rate)-channel.emitted;
                        float packet=channel.cells.Count>0?channel.amount/channel.cells.Count:Mathf.Max(.5f,channel.amount/48);
                        while(due>=packet-.0001f&&bodies.Count<BodyLimit)
                        {
                            var source=Source(channel.sourceId);if(source==null)break;var lots=source.Withdraw(Math.Min(packet,due));if(lots.Count==0){cast.status="Source depleted; existing material remains controlled";break;}
                            foreach(var lot in lots)
                            {
                                ledger.Pay(lot.kg*(9.81*Math.Max(0,source.Position.y-source.transform.position.y)+.02),"drive");
                                var body=Spawn(lot,source.Position,cast.id);float fraction=channel.amount<=.5f?0:channel.emitted/channel.amount;
                                int cell=Mathf.Min(channel.cells.Count-1,Mathf.FloorToInt(channel.emitted/packet+.01f));Vector3 target=channel.cells.Count>0?channel.cells[cell]:channel.form?DrawnGeometry.At(channel.shape,DrawnGeometry.Length(channel.shape)*fraction,out _):channel.anchor;
                                if(channel.cells.Count>0){body.sheetSize=channel.cellSize;body.SetSheet(channel.cellSize);body.Body.rotation=channel.cellRotation;}
                                guided.Add(new GuidedState{bodyId=body.id,castId=cast.id,channel=ci,heldPoint=target,entry=source.Position,offset=channel.spread?new Vector3(Mathf.Sin(channel.emitted*7),0,Mathf.Cos(channel.emitted*7))*channel.width:Vector3.zero});
                                FluidVolumeContacts(body,cast,ci);body.Body.linearVelocity=Vector3.up*.2f;channel.emitted+=(float)lot.kg;due-=(float)lot.kg;
                            }
                        }
                    }
                    else ApplyElement(cast,channel,dt);
                }
            }
            for(int i=guided.Count-1;i>=0;i--)
            {
                var guide=guided[i];var cast=casts.Find(c=>c.id==guide.castId);var body=bodies.Find(b=>b.id==guide.bodyId);
                if(body==null||cast==null||!cast.active||cast.channels[guide.channel].ended){guided.RemoveAt(i);continue;}Track(body,guide,cast,dt);
            }
            ThermalAndContacts(dt);UpdateEffects(dt);lastStepMs=timer.Elapsed.TotalMilliseconds;
            if(Physics.Raycast(motor.transform.position+Vector3.up*.2f,Vector3.down,out var floor,.5f,~(1<<2),QueryTriggerInteraction.Ignore)){var support=floor.collider.GetComponentInParent<MatterBody>();if(support!=null&&support.phase==MatterPhase.Ice)support.Body.AddForceAtPosition(Vector3.down*(80*9.81f),floor.point);}
        }
        void Track(MatterBody body,GuidedState guide,CastState cast,float dt)
        {
            var c=cast.channels[guide.channel];guide.age+=dt;Vector3 target=(c.form?guide.heldPoint:c.anchor)+guide.offset,tangent=Vector3.zero,velocity=Vector3.zero;
            float forceLimit=c.force*body.Body.mass/Mathf.Max(c.amount,body.Body.mass);
            if(c.attached)target+=motor.transform.position-cast.drawing.origin;
            if(c.tracking){var tracked=bodies.Find(b=>b.id==c.targetId);if(tracked!=null)target=tracked.Body.position;ledger.Pay(dt*100,"overhead");}
            bool waiting=WaitingForAssembly(cast,c);float motionTime=MotionTime(cast,c);
            if(c.form&&!c.direct&&(c.spin==0||waiting)&&guide.formStage<2)
            {
                var launchOffset=LaunchOffset(cast,c);var destination=target+launchOffset;
                float clearance=Mathf.Max(guide.entry.y,destination.y)+1.25f;var approach=guide.formStage==0?new Vector3(guide.entry.x,clearance,guide.entry.z):new Vector3(destination.x,clearance,destination.z);
                if(body.kind==MatterKind.Stone&&(c.cells.Count>0||waiting&&c.launchGroup>=0))
                {
                    var normal=c.cellRotation*Vector3.up;if(Vector3.Dot(guide.entry-destination,normal)<0)normal=-normal;
                    // Approach each face from outside its plane. Descending every
                    // tile through already placed rows jams a vertical wall.
                    if(guide.formStage==0)
                    {
                        if(body.Body.position.y>guide.entry.y+.5f)guide.formStage++;
                    }
                    else
                    {
                        approach=destination+normal*1.25f+(target-c.anchor)*.5f+Vector3.up*.8f;
                        if(Vector3.Distance(body.Body.position,approach)<.08f&&body.Body.linearVelocity.magnitude<.3f&&(c.cells.Count==0||Quaternion.Angle(body.Body.rotation,c.cellRotation)<5))guide.formStage++;
                    }
                }
                else if(waiting&&guide.formStage==0&&body.Body.position.y>guide.entry.y+.5f)guide.formStage++;
                else if(Vector3.Distance(body.Body.position,approach)<.18f&&body.Body.linearVelocity.magnitude<.5f)guide.formStage++;
                // Stage completion and the force target use the same world frame.
                // The common launch translation below is applied exactly once.
                target=approach-launchOffset;
            }
            if(c.direct&&!waiting)
            {
                var current=DrawnGeometry.At(c.path,guide.distance,out tangent);float error=Vector3.Distance(current+LaunchOffset(cast,c)+(c.form?guide.heldPoint-c.anchor:Vector3.zero),body.Body.position);
                float previous=guide.distance;guide.distance=c.form?Mathf.Min(c.length,Mathf.Max(0,c.launchGroup>=0||c.assemblyGroup>=0?motionTime:cast.elapsed-4)*c.speed):Mathf.Min(c.length,guide.distance+c.speed*dt*Mathf.Clamp01(guide.age*.5f)*Mathf.Clamp01(1-error/2));target=DrawnGeometry.At(c.path,guide.distance,out tangent)+guide.offset+(c.form?guide.heldPoint-c.anchor:Vector3.zero);
                velocity=guide.distance<c.length?tangent*((guide.distance-previous)/dt):Vector3.zero;
                if(guide.distance>=c.length&&c.end){body.owner="";body.Refresh();guided.Remove(guide);return;}
            }
            if(c.spin!=0&&!waiting)
            {
                if(c.bodySpin){Vector3 torque=Vector3.ClampMagnitude((c.axis*c.spin-body.Body.angularVelocity)*body.Body.mass,forceLimit*.15f);float paid=PayForce(torque,body.Body.angularVelocity,body.Body.mass,dt);body.Body.AddTorque(torque*paid,ForceMode.Force);}
                else
                {
                    Vector3 center=c.direct?DrawnGeometry.At(c.path,guide.distance,out _):c.anchor;center+=RegionOffset(cast,c);
                    if(c.form)
                    {
                        var rotated=Quaternion.AngleAxis(motionTime*c.spin*Mathf.Rad2Deg,c.axis)*(guide.heldPoint-c.anchor);
                        target=center+rotated+guide.offset;velocity+=Vector3.Cross(c.axis*c.spin,rotated);
                    }
                    else{Vector3 radial=body.Body.position-center;radial-=c.axis*Vector3.Dot(radial,c.axis);velocity+=Vector3.Cross(c.axis*c.spin,radial);target=center+radial.normalized*Mathf.Max(.5f,c.width)+c.axis*Vector3.Dot(target-center,c.axis);}
                }
            }
            if(c.spin==0||waiting||c.bodySpin)target+=LaunchOffset(cast,c);
            velocity+=LaunchVelocity(cast,c);
            if(c.end&&c.launchGroup>=0&&Launch(cast,c)?.distance>=c.launchRange){body.owner="";body.Refresh();guided.Remove(guide);return;}
            if(!c.direct&&!c.hold&&!c.form&&c.spin==0){body.owner="";body.Refresh();guided.Remove(guide);return;}
            if(waiting&&(body.kind==MatterKind.Stone||body.kind==MatterKind.Water))
            {
                var routing=body.GetComponent<AssemblyRouting>()??body.gameObject.AddComponent<AssemblyRouting>();
                target=routing.Route(body,target,(c.form?guide.heldPoint:c.anchor)+guide.offset+RegionOffset(cast,c),dt);
                if(routing.Phasing){guide.formStage=2;return;}
                if(Vector3.Distance(body.Body.position,(c.form?guide.heldPoint:c.anchor)+guide.offset+RegionOffset(cast,c))<.12f)guide.formStage=2;
            }
            else body.GetComponent<AssemblyRouting>()?.Restore();
            Vector3 acceleration=(target-body.Body.position)*50+(velocity-body.Body.linearVelocity)*14-Physics.gravity;
            Vector3 force=Vector3.ClampMagnitude(acceleration*body.Body.mass,forceLimit);
            float factor=PayForce(force,body.Body.linearVelocity,body.Body.mass,dt);body.Body.AddForce(force*factor,ForceMode.Force);
            if(body.sheetSize.sqrMagnitude>0){var desiredRotation=c.spin!=0&&!c.bodySpin&&!waiting?Quaternion.AngleAxis(motionTime*c.spin*Mathf.Rad2Deg,c.axis)*c.cellRotation:c.cellRotation;var delta=desiredRotation*Quaternion.Inverse(body.Body.rotation);delta.ToAngleAxis(out float angle,out var axis);if(angle>180)angle-=360;var angular=axis*angle*Mathf.Deg2Rad*(body.kind==MatterKind.Stone?100:36)-body.Body.angularVelocity*(body.kind==MatterKind.Stone?20:12);var inertiaRotation=body.Body.rotation*body.Body.inertiaTensorRotation;var torque=Vector3.ClampMagnitude(inertiaRotation*Vector3.Scale(body.Body.inertiaTensor,Quaternion.Inverse(inertiaRotation)*angular),forceLimit);if(float.IsFinite(torque.x))body.Body.AddTorque(torque*PayForce(torque,body.Body.angularVelocity,body.Body.mass,dt));}
            if(!cast.launches.Any(l=>!l.launched&&!l.impacted))
                cast.status=(target-body.Body.position).magnitude>1?"Control limited: weight, turn or obstruction exceeds guidance":"Following drawn geometry";
        }
        float PayForce(Vector3 force,Vector3 velocity,float mass,float dt)
        {
            double work=Math.Abs(Vector3.Dot(force,velocity))*dt+.5*force.sqrMagnitude/Math.Max(.01,mass)*dt*dt;
            if(work<=1e-12)return 1;double wanted=work/0.8;double paid=ledger.Pay(wanted*.8,"drive")+ledger.Pay(wanted*.2,"loss");return (float)Math.Min(1,paid/wanted);
        }
        public Dictionary<string,double> MaterialTotals()
        {
            var totals=new Dictionary<string,double>();void Add(string id,double kg){if(!totals.ContainsKey(id))totals[id]=0;totals[id]+=kg;}
            foreach(var source in sources)foreach(var lot in source.lots)Add(lot.origin,lot.kg);
            foreach(var body in bodies)Add(body.origin,body.kg);return totals;
        }
        public void Contact(MatterBody body,Vector3 point,float speed){if(speed>1){Burst(point,body.kind==MatterKind.Water?new Color(.25f,.7f,1):Palette.Stone,Mathf.Min(16,Mathf.CeilToInt(speed)),Vector3.up*speed*.2f,false,body.kind==MatterKind.Water?.06f:.14f,.65f,body.kind==MatterKind.Water?0:2);feedback?.Impact(point,speed);}}
    }
}
