using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arcana.Elemental
{
    public sealed partial class ElementalWorld
    {
        [Serializable] public sealed class Mote {public Vector3 p,v;public float life,size,initialLife;public int style;public Color color;public bool hot;public double heat;}
        readonly List<Mote> motes=new List<Mote>();ParticleSystem particles,airParticles,dustParticles;ParticleSystem.Particle[] visualParticles=new ParticleSystem.Particle[1200];
        readonly Dictionary<string,LineRenderer> beams=new Dictionary<string,LineRenderer>();readonly Dictionary<string,Light> lamps=new Dictionary<string,Light>();
        readonly Dictionary<string,float> settled=new Dictionary<string,float>();public readonly List<Vector3> LastDischargeRoute=new List<Vector3>();public float windMechanismAngle;
        public string LastDischargeDiagnostics="";
        Vector3 ChannelPoint(CastState cast,Channel c)
        {
            Vector3 p=c.direct?DrawnGeometry.At(c.path,Mathf.Min(c.length,cast.elapsed*c.speed),out _):c.anchor;
            if(c.attached)p+=motor.transform.position-cast.drawing.origin;
            if(c.tracking){var tracked=bodies.Find(b=>b.id==c.targetId);if(tracked!=null)p=tracked.Body.position;}return p+LaunchOffset(cast,c);
        }
        Vector3 RegionOffset(CastState cast,Channel c)
        {
            Vector3 offset=c.attached?motor.transform.position-cast.drawing.origin:Vector3.zero;
            if(c.tracking){var target=bodies.Find(b=>b.id==c.targetId);if(target!=null)offset+=target.Body.position-c.anchor;}
            return offset+LaunchOffset(cast,c);
        }
        float Proximity(CastState cast,Channel c,Vector3 p,out Vector3 tangent,bool formOnly=false)
        {
            tangent=Vector3.forward;float best=float.PositiveInfinity;
            var path=c.form?c.shape:c.direct&&!formOnly?c.path:null;
            p-=RegionOffset(cast,c);if(c.form&&c.direct)p-=DrawnGeometry.At(c.path,Mathf.Min(c.length,cast.elapsed*c.speed),out _)-c.anchor;if(c.form&&c.cells.Count>0)
            {
                float region=float.PositiveInfinity;foreach(var cell in c.cells){var local=Quaternion.Inverse(c.cellRotation)*(p-cell);var gap=new Vector3(Mathf.Max(0,Mathf.Abs(local.x)-c.cellSize.x*.5f),Mathf.Max(0,Mathf.Abs(local.y)-c.cellSize.y*.5f),Mathf.Max(0,Mathf.Abs(local.z)-c.cellSize.z*.5f));region=Mathf.Min(region,gap.magnitude);}tangent=c.axis.normalized;return region;
            }
            if(path==null||path.Count<2)return Vector3.Distance(p,c.anchor);
            for(int i=1;i<path.Count;i++){var d=path[i]-path[i-1];float t=Mathf.Clamp01(Vector3.Dot(p-path[i-1],d)/Mathf.Max(d.sqrMagnitude,1e-6f));float gap=Vector3.Distance(p,path[i-1]+d*t);if(gap<best){best=gap;tangent=d.normalized;}}return best;
        }
        Vector3 WindChannel(CastState cast,Channel c,Vector3 position)
        {
            if(c.ended||cast.elapsed>c.duration)return Vector3.zero;
            float distance=Proximity(cast,c,position,out var tangent),range=Mathf.Max(1,c.width*2);if(distance>range)return Vector3.zero;
            var center=c.form&&c.direct?DrawnGeometry.At(c.path,Mathf.Min(c.length,cast.elapsed*c.speed),out _):c.anchor;
            var requested=tangent*c.speed;
            if(c.spin!=0)requested+=Vector3.Cross(c.axis*c.spin,position-center-RegionOffset(cast,c));
            return Vector3.ClampMagnitude(requested*(1-distance/range),60);
        }
        public Vector3 WindAt(Vector3 position)
        {
            Vector3 wind=Vector3.zero;
            foreach(var cast in casts.Where(c=>c.active))foreach(var c in cast.channels.Where(c=>c.element==Rune.Air))wind+=WindChannel(cast,c,position);
            return Vector3.ClampMagnitude(wind,60);
        }
        Vector3 EmissionPoint(CastState cast,Channel c,Vector3 fallback)
        {
            float t=Mathf.Repeat(simulationSteps*.618034f,1),a=simulationSteps*2.39996f;
            var center=c.form?(c.cells.Count>0?c.cells[Mathf.Min(c.cells.Count-1,(int)(t*c.cells.Count))]:DrawnGeometry.At(c.shape,DrawnGeometry.Length(c.shape)*t,out _))+RegionOffset(cast,c):fallback;
            if(c.form&&c.direct)center+=ChannelPoint(cast,c)-c.anchor-RegionOffset(cast,c);
            var axis=c.axis.normalized;var u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;var v=Vector3.Cross(axis,u);
            float radius=Mathf.Max(.4f,c.width)*.5f*Mathf.Sqrt(Mathf.Repeat(simulationSteps*.75487766f,1));
            return center+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*radius;
        }
        void ApplyElement(CastState cast,Channel channel,float dt)
        {
            Vector3 point=ChannelPoint(cast,channel);float area=Mathf.Max(.4f,channel.width);
            if(channel.element==Rune.Air)
            {
                var forces=new List<(MatterBody body,Vector3 force)>();float totalForce=0;
                foreach(var body in bodies)
                {
                    if(body.Body.isKinematic)continue;
                    float distance=Proximity(cast,channel,body.Body.position,out var tangent);float range=Mathf.Max(1,channel.width*2);if(distance>range)continue;
                    Vector3 wind=WindChannel(cast,channel,body.Body.position);
                    Vector3 relative=wind-body.Body.linearVelocity;float drag=body.kind==MatterKind.Water?.8f:.45f;Vector3 force=Vector3.ClampMagnitude(relative*relative.magnitude*(Mathf.PI*body.radius*body.radius*drag*1.2f),channel.force);
                    forces.Add((body,force));totalForce+=force.magnitude;
                }
                float allocation=Mathf.Min(1,channel.force/Mathf.Max(.001f,totalForce));foreach(var item in forces)
                {
                    var body=item.body;var force=item.force*allocation;
                    float paid=PayForce(force,body.Body.linearVelocity,body.Body.mass,dt);
                    if(body.name=="Wind rotor")body.Body.AddForceAtPosition(force*paid,body.Body.position+Vector3.up*body.radius);else body.Body.AddForce(force*paid);
                }
                if(simulationSteps%4==0){var emission=EmissionPoint(cast,channel,point);Burst(emission,Color.Lerp(Palette.Teal,Palette.Paper,.65f),3,WindAt(emission),false,Mathf.Clamp(channel.width*.02f,.035f,.12f),.8f,1);}
                ledger.Pay(100*dt,"overhead");return;
            }
            if(channel.element==Rune.Fire||channel.element==Rune.Frost)
            {
                var formOffset=channel.form&&channel.direct?point-channel.shape.Aggregate(Vector3.zero,(a,p)=>a+p)/Math.Max(1,channel.shape.Count)-RegionOffset(cast,channel):Vector3.zero;
                var affected=bodies.Where(b=>(channel.form?Proximity(cast,channel,b.Body.position,out _,true):Vector3.Distance(point,b.Body.position))<=Mathf.Max(.6f,area*2)).ToList();
                if(channel.element==Rune.Frost)affected.RemoveAll(b=>b.enthalpy<=-b.kg*(b.kind==MatterKind.Water?350000:160000)+1);
                double requested=channel.power*dt,energy=ledger.Pay(requested*.8,channel.element==Rune.Fire?"heat":"cooling");ledger.Pay(requested*.2,"loss");
                if(channel.element==Rune.Fire)
                {
                    foreach(var body in affected)body.AddHeat(energy*.6/Math.Max(1,affected.Count));if(affected.Count==0)ambientHeatJ+=energy*.6;
                    var emissionPoint=EmissionPoint(cast,channel,point);
                    if(motes.Count<1200)motes.Add(new Mote{p=emissionPoint,v=WindAt(emissionPoint)+Vector3.up*1.5f,life=Mathf.Clamp(channel.width,1.5f,4),size=Mathf.Clamp(channel.width*.18f,.32f,1.5f),color=new Color(1,.35f,.06f),hot=true,heat=energy*.4});else{ambientHeatJ+=energy*.4;cast.status="Hot-gas capacity reached; excess heat disperses to ambient";}
                }
                else
                {
                    foreach(var body in affected){double removed=Math.Min(energy/Math.Max(1,affected.Count)*.5,Math.Max(0,body.enthalpy+body.kg*(body.kind==MatterKind.Water?350000:160000)));body.AddHeat(-removed);ambientHeatJ+=removed;}
                    if(simulationSteps%6==0)Burst(point,new Color(.5f,.8f,1),2,Vector3.up*.3f,false,.09f,.7f);
                }
                return;
            }
            if(channel.element==Rune.Lightning){Discharge(cast,channel,dt);return;}
            if(channel.element==Rune.Light)Illuminate(cast,channel,dt);
        }
        void ThermalAndContacts(float dt)
        {
            var active=bodies.ToArray();
            if(simulationSteps%10==0)WeldIce();
            foreach(var body in active)
            {
                if(body.kg<=0||!body.gameObject.activeSelf)continue;
                if(body.phase==MatterPhase.Steam)body.Body.AddForce(-Physics.gravity*body.Body.mass*1.6f+(WindAt(body.Body.position)-body.Body.linearVelocity)*body.Body.mass*.8f);
                if(body.kind==MatterKind.Fuel&&body.fuelKg>0&&body.Temperature>230)body.burning=true;
                if(body.burning)
                {
                    double consumed=Math.Min(body.fuelKg,.015*dt);body.fuelKg-=consumed;double released=consumed*18000000;fuelReleasedJ+=released;body.AddHeat(released*.12);ambientHeatJ+=released*.58;
                    var neighbours=active.Where(b=>b!=body&&Vector3.Distance(body.Body.position,b.Body.position)<1.5f).ToList();foreach(var other in neighbours)other.AddHeat(released*.3/Math.Max(1,neighbours.Count));if(neighbours.Count==0)ambientHeatJ+=released*.3;
                    if(simulationSteps%3==0)Burst(body.Body.position,new Color(1,.45f,.08f),3,WindAt(body.Body.position)+Vector3.up*1.8f,true,.12f,1.2f);
                    if(body.fuelKg<=0||body.Temperature<160)body.burning=false;
                }
                for(int j=0;j<active.Length;j++)
                {
                    var other=active[j];if(other.kg<=0||string.CompareOrdinal(body.id,other.id)>=0||Vector3.Distance(body.Body.position,other.Body.position)>body.radius+other.radius+.08f)continue;
                    double difference=body.Temperature-other.Temperature;double transfer=difference*100*dt;transfer=Math.Sign(transfer)*Math.Min(Math.Abs(transfer),Math.Abs(difference)*Math.Min(body.kg,other.kg)*300);
                    body.AddHeat(-transfer);other.AddHeat(transfer);
                    if(body.kind==MatterKind.Water&&body.phase==MatterPhase.Liquid&&other.burning){other.burning=false;double heat=Math.Min(Math.Max(0,other.enthalpy),body.kg*200000);body.AddHeat(heat);other.AddHeat(-heat);}
                    if(other.kind==MatterKind.Water&&other.phase==MatterPhase.Liquid&&body.burning){body.burning=false;double heat=Math.Min(Math.Max(0,body.enthalpy),other.kg*200000);other.AddHeat(heat);body.AddHeat(-heat);}
                    // Wet loose soil remains a mixed physical pair; solid rock is never relabelled as slurry.
                    if(body.kind==MatterKind.Soil&&other.kind==MatterKind.Water)body.wetness=Math.Max(body.wetness,.001);
                    if(other.kind==MatterKind.Soil&&body.kind==MatterKind.Water)other.wetness=Math.Max(other.wetness,.001);
                    if(body.kind==MatterKind.Soil&&other.kind==MatterKind.Water||other.kind==MatterKind.Soil&&body.kind==MatterKind.Water)
                    {var relative=other.Body.linearVelocity-body.Body.linearVelocity;var force=Vector3.ClampMagnitude(relative*2,15);body.Body.AddForce(force);other.Body.AddForce(-force);double dissipated=Vector3.Dot(force,relative)*dt;body.AddHeat(dissipated*.5);other.AddHeat(dissipated*.5);}
                    if(body.sheetSize.sqrMagnitude==0&&other.sheetSize.sqrMagnitude==0&&body.phase==MatterPhase.Ice&&other.phase==MatterPhase.Ice&&!body.GetComponents<FixedJoint>().Any(joint=>joint.connectedBody==other.Body))
                    {var joint=body.gameObject.AddComponent<FixedJoint>();joint.connectedBody=other.Body;joint.breakForce=20000*body.radius*body.radius;joint.breakTorque=joint.breakForce*body.radius;body.bridge=true;other.bridge=true;}
                }
                if(body.kind==MatterKind.Water&&body.phase==MatterPhase.Liquid&&!body.Controlled&&body.Body.linearVelocity.sqrMagnitude<.04f)
                {
                    settled.TryGetValue(body.id,out float time);settled[body.id]=time+dt;if(time>2&&Physics.Raycast(body.Body.position,Vector3.down,body.radius+.08f,~(1<<2),QueryTriggerInteraction.Ignore))CollectSettled(body);
                }
                else settled.Remove(body.id);
                if(body.Body.position.y<-12)Recover(body);
            }
        }
        bool ReadyToBond(MatterBody body)
        {
            if(!body.Controlled)return false;
            var guide=guided.Find(g=>g.bodyId==body.id);if(guide==null||guide.formStage<2)return false;
            var cast=casts.Find(c=>c.id==guide.castId);var channel=cast.channels[guide.channel];
            // Bond actual settled contacts near the drawn form. Requiring an
            // ideal angle strands edge cells that rest against their neighbours.
            return !channel.direct&&channel.spin==0&&Vector3.Distance(body.Body.position,guide.heldPoint+RegionOffset(cast,channel))<.055f&&body.Body.linearVelocity.sqrMagnitude<.0025f&&body.Body.angularVelocity.sqrMagnitude<.01f;
        }
        void WeldIce()
        {
            var tiles=bodies.Where(b=>(b.phase==MatterPhase.Ice||b.kind==MatterKind.Stone&&ReadyToBond(b))&&!b.fractured&&b.sheetSize.sqrMagnitude>0&&b.Body.linearVelocity.sqrMagnitude<.04f).ToArray();
            foreach(var tile in tiles)
            {
                if(tile.GetComponents<FixedJoint>().Any(j=>j.connectedBody==null))continue;
                if(Physics.Raycast(tile.Body.position,Vector3.down,out var contact,tile.GetComponent<BoxCollider>().bounds.extents.y+.045f,~(1<<2),QueryTriggerInteraction.Ignore)&&contact.rigidbody==null&&contact.normal.y>.8f)
                {
                    var anchor=tile.gameObject.AddComponent<FixedJoint>();anchor.autoConfigureConnectedAnchor=false;anchor.anchor=tile.transform.InverseTransformPoint(contact.point);anchor.connectedAnchor=contact.point;
                    float width=Mathf.Max(tile.sheetSize.x,tile.sheetSize.z);anchor.breakForce=1000000*tile.sheetSize.y*width;anchor.breakTorque=5000000*width*tile.sheetSize.y*tile.sheetSize.y/6;tile.bridge=true;
                }
            }
            for(int i=0;i<tiles.Length;i++)for(int j=i+1;j<tiles.Length;j++)
            {
                var a=tiles[i];var b=tiles[j];var bounds=a.GetComponent<BoxCollider>().bounds;bounds.Expand(.045f);
                if(!bounds.Intersects(b.GetComponent<BoxCollider>().bounds)||a.GetComponents<FixedJoint>().Any(x=>x.connectedBody==b.Body)||b.GetComponents<FixedJoint>().Any(x=>x.connectedBody==a.Body))continue;
                var aCollider=a.GetComponent<BoxCollider>();var bCollider=b.GetComponent<BoxCollider>();
                var aPoint=aCollider.ClosestPoint(b.Body.position);var bPoint=bCollider.ClosestPoint(aPoint);aPoint=aCollider.ClosestPoint(bPoint);
                if(Vector3.Distance(aPoint,bPoint)>.045f)continue;
                var contactPoint=(aPoint+bPoint)*.5f;
                var joint=a.gameObject.AddComponent<FixedJoint>();joint.autoConfigureConnectedAnchor=false;joint.connectedBody=b.Body;joint.anchor=a.transform.InverseTransformPoint(contactPoint);joint.connectedAnchor=b.transform.InverseTransformPoint(contactPoint);joint.enableCollision=false;
                float thickness=Mathf.Min(a.sheetSize.y,b.sheetSize.y),width=Mathf.Min(Mathf.Max(a.sheetSize.x,a.sheetSize.z),Mathf.Max(b.sheetSize.x,b.sheetSize.z));
                joint.breakForce=1000000*thickness*width;joint.breakTorque=5000000*width*thickness*thickness/6;a.bridge=b.bridge=true;
            }
        }
        public void RemoveIceBonds(MatterBody melted){foreach(var body in bodies)foreach(var joint in body.GetComponents<FixedJoint>())if(body==melted||joint.connectedBody==melted.Body)Destroy(joint);}
        void CollectSettled(MatterBody body)
        {
            var collector=sources.Find(s=>s.collector&&s.kind==MatterKind.Water&&Vector3.Distance(s.transform.position,body.Body.position)<1.5f);
            if(collector==null)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Collected water pool";go.transform.position=body.Body.position;go.transform.localScale=new Vector3(.5f,.015f,.5f);go.GetComponent<Renderer>().sharedMaterial=bodyMaterial;go.GetComponent<Renderer>().material.color=new Color(.12f,.55f,.8f);go.GetComponent<Collider>().isTrigger=true;
                collector=go.AddComponent<MaterialSource>();collector.stableId="pool-"+Guid.NewGuid().ToString("N");collector.label="Collected pool";collector.kind=MatterKind.Water;collector.initialKg=0;collector.collector=true;sources.Add(collector);
            }
            collector.Collect(body);
        }
        void Recover(MatterBody body)
        {
            var source=Source(body.origin);if(source==null)return;source.Deposit(new MaterialLot{origin=body.origin,kind=body.kind,kg=body.kg,enthalpy=body.enthalpy});Retire(body);Say("Out-of-bounds recovery returned material to its original reservoir.");
        }
        sealed class Conductor {public Vector3 p;public float sigma;public ConductivePost post;public MatterBody body;}
        void Discharge(CastState cast,Channel channel,float dt)
        {
            if(EnergizeConductors(cast,channel,dt))return;
            var nodes=new List<Conductor>{new Conductor{p=cast.drawing.origin+Vector3.up*1.5f,sigma=2}};
            nodes.AddRange(posts.Where(p=>!p.insulated).Select(p=>new Conductor{p=p.transform.position,sigma=p.conductivity,post=p}));
            nodes.AddRange(bodies.Where(b=>b.kind==MatterKind.Water&&b.phase==MatterPhase.Liquid).Select(b=>new Conductor{p=b.Body.position,sigma=4,body=b}));
            foreach(var source in sources.Where(s=>s.kind==MatterKind.Water&&s.Available>.1))nodes.Add(new Conductor{p=source.Position,sigma=3});
            int end=nodes.FindIndex(n=>n.post!=null&&n.post.receiver&&(string.IsNullOrEmpty(channel.targetId)||n.post.stableId==channel.targetId));if(end<0){cast.status="Lightning: select a receiver";return;}
            var distance=Enumerable.Repeat(float.PositiveInfinity,nodes.Count).ToArray();var previous=Enumerable.Repeat(-1,nodes.Count).ToArray();var visited=new bool[nodes.Count];distance[0]=0;
            for(int count=0;count<nodes.Count;count++)
            {
                int from=-1;for(int i=0;i<nodes.Count;i++)if(!visited[i]&&(from<0||distance[i]<distance[from]))from=i;if(from<0||float.IsInfinity(distance[from]))break;visited[from]=true;if(from==end)break;
                for(int to=1;to<nodes.Count;to++)
                {
                    if(visited[to])continue;float gap=Vector3.Distance(nodes[from].p,nodes[to].p);if(gap>2||Vector3.Distance(nodes[0].p,nodes[to].p)>30)continue;
                    var hits=Physics.RaycastAll(nodes[from].p,(nodes[to].p-nodes[from].p).normalized,gap,~(1<<2),QueryTriggerInteraction.Ignore);
                    bool blocked=hits.Any(h=>h.collider.GetComponentInParent<ConductivePost>()?.insulated==true||h.collider.gameObject.CompareTag("Untagged")&&h.collider.GetComponentInParent<MatterBody>()==null&&h.collider.GetComponentInParent<ConductivePost>()==null&&h.distance>.1f&&h.distance<gap-.1f);
                    if(blocked)continue;float resistance=gap*(gap>.35f?20:1)/Mathf.Max(.01f,Mathf.Min(nodes[from].sigma,nodes[to].sigma));float value=distance[from]+resistance;
                    if(value<distance[to]){distance[to]=value;previous[to]=from;}
                }
            }
            LastDischargeDiagnostics="Reachable "+visited.Count(v=>v)+" / "+nodes.Count+" nodes; receiver index "+end+"; "+string.Join("; ",nodes.Select((n,i)=>i+":"+n.p+" cost="+distance[i]));
            if(previous[end]<0){LastDischargeRoute.Clear();cast.status="Lightning blocked: gap >2 m or insulating obstruction";SetBeam(cast.id+"electric",Array.Empty<Vector3>(),Color.yellow);return;}
            var route=new List<int>();for(int i=end;i>=0;i=previous[i])route.Add(i);route.Reverse();LastDischargeRoute.Clear();LastDischargeRoute.AddRange(route.Select(i=>nodes[i].p));SetBeam(cast.id+"electric",LastDischargeRoute.ToArray(),new Color(.7f,.8f,1));
            double energy=ledger.Pay(channel.power*dt,"discharge"),delivered=energy/(1+distance[end]*.03);nodes[end].post.Receive(delivered);double dissipated=energy-delivered;var wet=route.Where(i=>nodes[i].body!=null).ToArray();foreach(int i in wet)nodes[i].body.AddHeat(dissipated/Math.Max(1,wet.Length));if(wet.Length==0)ambientHeatJ+=dissipated;cast.status="Conductive route energized receiver";
        }
        void Illuminate(CastState cast,Channel channel,float dt,string suffix="")
        {
            if(channel.form&&channel.shape.Count>1&&suffix=="")
            {
                int count=8;float length=DrawnGeometry.Length(channel.shape);var center=channel.shape.Aggregate(Vector3.zero,(a,p)=>a+p)/channel.shape.Count;var offset=channel.direct?ChannelPoint(cast,channel)-center:Vector3.zero;
                var patternDirection=(channel.anchor-center-offset).normalized;if(patternDirection.sqrMagnitude<.5f)patternDirection=cast.drawing.forward;
                for(int i=0;i<count;i++){var emitter=channel.Copy();emitter.form=false;emitter.direct=true;var emitterPoint=DrawnGeometry.At(channel.shape,length*i/(count-1),out _)+offset;emitter.path=new List<Vector3>{emitterPoint};emitter.anchor=emitterPoint+patternDirection*30;emitter.length=0;emitter.power=channel.power/count;Illuminate(cast,emitter,dt,"-"+i);}return;
            }
            Vector3 origin=channel.direct?ChannelPoint(cast,channel):cast.drawing.origin+Vector3.up*1.7f;
            Vector3 direction=(channel.anchor-origin).normalized;if(direction.sqrMagnitude<.5f)direction=cast.drawing.forward;Vector3 initialDirection=direction,exitPoint=Vector3.zero;bool refracted=false;
            var path=new List<Vector3>{origin};float remaining=30;float power=1;
            for(int bounce=0;bounce<3&&remaining>.01f;bounce++)
            {
                if(!Physics.Raycast(origin,direction,out var hit,remaining,~(1<<2),QueryTriggerInteraction.Ignore)){path.Add(origin+direction*remaining);break;}
                path.Add(hit.point);remaining-=hit.distance;var water=hit.collider.GetComponentInParent<MatterBody>();
                if(water!=null&&water.kind==MatterKind.Water)
                {
                    // Single-interface Snell refraction, n_air/n_water=1/1.33, and bounded scattering.
                    float cosine=-Vector3.Dot(hit.normal,direction),eta=1/1.33f,k=1-eta*eta*(1-cosine*cosine);direction=k<0?Vector3.Reflect(direction,hit.normal):(eta*direction+(eta*cosine-Mathf.Sqrt(k))*hit.normal).normalized;
                    if(simulationSteps%5==0)Burst(hit.point,new Color(1,.92f,.6f),2,direction*.5f,false,.05f,.4f);power*=.75f;origin=hit.point+direction*(water.radius*2+.02f);exitPoint=origin;refracted=true;continue;
                }
                break;
            }
            SetBeam(cast.id+"light"+suffix,path.ToArray(),new Color(1,.85f,.35f)*power);
            string lampId=cast.id+suffix;if(!lamps.TryGetValue(lampId,out var lamp)){lamp=new GameObject("Occluding spell light").AddComponent<Light>();lamp.type=LightType.Spot;lamp.range=30;lamp.spotAngle=channel.spread?70:25;lamp.shadows=LightShadows.Hard;lamps.Add(lampId,lamp);}
            lamp.transform.SetPositionAndRotation(path[0],Quaternion.LookRotation(initialDirection));lamp.color=new Color(1,.88f,.6f);lamp.intensity=5*channel.power/50000;lamp.spotAngle=suffix.Length>0?3:channel.spread?70:25;lamp.enabled=true;
            string refractedId=lampId+"-refraction";if(refracted){if(!lamps.TryGetValue(refractedId,out var exitLight)){exitLight=new GameObject("Refracted surface illumination").AddComponent<Light>();exitLight.type=LightType.Spot;exitLight.range=30;exitLight.spotAngle=15;exitLight.shadows=LightShadows.Hard;lamps[refractedId]=exitLight;}exitLight.transform.SetPositionAndRotation(exitPoint,Quaternion.LookRotation(direction));exitLight.intensity=lamp.intensity*power;exitLight.color=lamp.color;exitLight.enabled=true;}else if(lamps.TryGetValue(refractedId,out var inactive))inactive.enabled=false;
            ledger.Pay(channel.power*.01*dt,"light");
        }
        void SetBeam(string id,Vector3[] points,Color color)
        {
            if(!beams.TryGetValue(id,out var line)){line=new GameObject("Calculated route "+id).AddComponent<LineRenderer>();line.sharedMaterial=trailMaterial;line.startWidth=line.endWidth=.035f;beams.Add(id,line);}line.positionCount=points.Length;line.SetPositions(points);line.startColor=line.endColor=color;
        }
        public void Burst(Vector3 position,Color color,int count,Vector3 velocity,bool hot=false,float size=.08f,float life=.6f,int style=0)
        {
            for(int i=0;i<count&&motes.Count<1200;i++){float a=(simulationSteps+i)*2.39996f;var jitter=new Vector3(Mathf.Cos(a),Mathf.Sin(a*.7f),Mathf.Sin(a))*.15f;motes.Add(new Mote{p=position+jitter,v=velocity+jitter,life=life,initialLife=life,style=style,size=size,color=color,hot=hot});}
        }
        ParticleSystem CreateMoteRenderer(string name,ParticleSystemRenderMode mode,float length,float velocity)
        {
            var system=new GameObject(name).AddComponent<ParticleSystem>();var main=system.main;main.loop=false;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=1200;main.startColor=mode==ParticleSystemRenderMode.Billboard?Palette.Floor:Palette.Teal;var emission=system.emission;emission.enabled=false;system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var r=system.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=effectMaterial??trailMaterial;r.renderMode=mode;r.lengthScale=length;r.velocityScale=velocity;return system;
        }
        void UpdateEffects(float dt)
        {
            if(particles==null)
            {
                particles=new GameObject("State-derived fluid and heat motes").AddComponent<ParticleSystem>();var main=particles.main;main.loop=false;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=1200;main.startColor=Palette.Teal;var emission=particles.emission;emission.enabled=false;particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=effectMaterial!=null?effectMaterial:trailMaterial;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=1.5f;renderer.velocityScale=.12f;
            }
            for(int i=motes.Count-1;i>=0;i--)
            {
                var m=motes[i];m.life-=dt;if(m.life<=0){ambientHeatJ+=m.heat;motes.RemoveAt(i);continue;}
                if(m.hot)
                {
                    m.v=Vector3.Lerp(m.v,WindAt(m.p)+Vector3.up*1.8f,dt*3);
                    if(m.heat>0){var hitBody=bodies.Find(b=>Vector3.Distance(b.Body.position,m.p)<b.radius+.35f);if(hitBody!=null){double exchanged=m.heat*Math.Min(1,dt*10);hitBody.AddHeat(exchanged);m.heat-=exchanged;}}
                }
                else if(m.style==1)m.v=Vector3.Lerp(m.v,WindAt(m.p),dt*5);else m.v+=Physics.gravity*dt*(m.style==2?.7f:.15f);
                var displacement=m.v*dt;if(Physics.Raycast(m.p,displacement.normalized,out var hit,displacement.magnitude,~(1<<2),QueryTriggerInteraction.Ignore)){m.p=hit.point;m.v=Vector3.Reflect(m.v,hit.normal)*.3f;}else m.p+=displacement;
            }
            if(airParticles==null)airParticles=CreateMoteRenderer("Flow-aligned air filaments",ParticleSystemRenderMode.Stretch,3,.25f);
            if(dustParticles==null)dustParticles=CreateMoteRenderer("Earth impact dust",ParticleSystemRenderMode.Billboard,1,0);
            for(int style=0;style<3;style++)
            {
                int count=0;foreach(var m in motes.Where(m=>m.style==style)){if(m.initialLife<=0)m.initialLife=m.life;float fade=Mathf.Clamp01(m.life/Mathf.Max(.01f,m.initialLife));var color=style==2?Color.Lerp(m.color,Palette.Wood,.65f):m.color;color.a*=fade*(style==1?.48f:style==2?.28f:1);visualParticles[count++]=new ParticleSystem.Particle{position=m.p,velocity=m.v,startColor=color,startSize=m.size*(style==2?2.5f:1)*Mathf.Sqrt(fade),remainingLifetime=m.life,startLifetime=m.initialLife};}
                (style==0?particles:style==1?airParticles:dustParticles).SetParticles(visualParticles,count);
            }
            foreach(var beam in beams)if(!casts.Any(c=>c.active&&beam.Key.StartsWith(c.id)))beam.Value.positionCount=0;
            foreach(var lamp in lamps)if(!casts.Any(c=>c.active&&lamp.Key.StartsWith(c.id)))lamp.Value.enabled=false;
        }
    }
}
