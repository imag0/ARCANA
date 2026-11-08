using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arcana.Elemental
{
    [Serializable] public sealed class LaunchState
    {
        public int node;public bool launched,impacted,ready;public Vector3 center,offset,referenceCenter,impactPoint;public float stable,elapsed,distance,speed;public Vector3 direction;
    }
    public sealed partial class ElementalWorld
    {
        int AssemblyGroup(Channel c)=>c.launchGroup>=0?c.launchGroup:c.assemblyGroup;
        LaunchState Launch(CastState cast,Channel c)=>cast.launches.Find(l=>l.node==AssemblyGroup(c));
        bool WaitingForAssembly(CastState cast,Channel c)=>AssemblyGroup(c)>=0&&Launch(cast,c)?.launched!=true;
        float MotionTime(CastState cast,Channel c)=>AssemblyGroup(c)<0?cast.elapsed:Launch(cast,c)?.elapsed??0;
        Vector3 LaunchOffset(CastState cast,Channel c){var l=Launch(cast,c);return l==null?Vector3.zero:l.offset+(l.launched?l.direction*l.distance:Vector3.zero);}
        Vector3 LaunchVelocity(CastState cast,Channel c){var l=Launch(cast,c);return l!=null&&l.launched&&l.distance<c.launchRange?l.direction*l.speed:Vector3.zero;}
        public void RequestLaunch()
        {
            bool fired=false;
            foreach(var cast in casts.Where(c=>c.active))foreach(var state in cast.launches.Where(l=>l.ready&&!l.launched&&!l.impacted))
            {state.launched=true;state.direction=motor.view.transform.forward.normalized;fired=true;}
            Say(fired?"Launched along your current aim.":"Still assembling — wait for READY, then left-click again.");
        }
        public void AdjustSpin(float factor)
        {
            foreach(var c in casts.Where(c=>c.active).SelectMany(c=>c.channels).Where(c=>c.spin!=0))c.spin=Mathf.Clamp(c.spin*factor,-60,60);
            Say("Spin rad/s: "+string.Join(", ",casts.Where(c=>c.active).SelectMany(c=>c.channels).Where(c=>c.spin!=0).Select(c=>c.spin.ToString("F2"))));
        }
        void FluidVolumeContacts(MatterBody body,CastState cast,int channel)
        {
            if(body.kind!=MatterKind.Water||!cast.channels[channel].volume)return;
            // Liquid cells are samples of one continuum, not stacked rigid bricks.
            // External contacts remain enabled, including ground and other spells.
            var ids=guided.Where(g=>g.castId==cast.id&&g.channel==channel).Select(g=>g.bodyId).ToHashSet();
            foreach(var peer in bodies.Where(b=>b!=body&&ids.Contains(b.id)))
                foreach(var a in body.GetComponents<Collider>().Where(c=>c.enabled))foreach(var b in peer.GetComponents<Collider>().Where(c=>c.enabled))Physics.IgnoreCollision(a,b);
        }
        void UpdateLaunches(CastState cast,float dt)
        {
            foreach(var group in cast.channels.Where(c=>AssemblyGroup(c)>=0).GroupBy(c=>AssemblyGroup(c)))
            {
                var state=cast.launches.Find(l=>l.node==group.Key);
                if(state==null){state=new LaunchState{node=group.Key,referenceCenter=group.First().assemblyCenter,center=group.First().assemblyCenter};cast.launches.Add(state);}
                if(state.impacted)continue;
                if(!state.launched)
                {
                    if(group.Any(c=>c.launchGroup>=0)){state.center=FrontCenter(group.Max(c=>c.frontDepth),group.Max(c=>c.frontDown));state.offset=state.center-state.referenceCenter;}
                    bool ready=true;float maximumError=0;
                    foreach(var c in group.Where(c=>c.element==Rune.Water||c.element==Rune.Stone))
                    {
                        if(c.emitted<c.amount-.01f){ready=false;break;}
                        int index=cast.channels.IndexOf(c);var members=guided.Where(g=>g.castId==cast.id&&g.channel==index).ToArray();
                        if(members.Length==0){ready=false;break;}
                        foreach(var g in members)
                        {
                            var b=bodies.Find(body=>body.id==g.bodyId);
                            var target=(c.form?g.heldPoint:c.anchor)+g.offset+RegionOffset(cast,c);
                            maximumError=Mathf.Max(maximumError,b==null?999:Vector3.Distance(b.Body.position,target));
                            if(b==null||Vector3.Distance(b.Body.position,target)>.18f||b.Body.linearVelocity.magnitude>.6f){ready=false;break;}
                        }
                    }
                    state.stable=ready?state.stable+dt:0;
                    cast.status=cast.elapsed>8&&!ready?"Assembly blocked or moving (error "+maximumError.ToString("F2")+" m). Hold aim steady; move to open space, increase FORCE, or use SMALLER.":"Assembling in front of you. Hold aim steady; waits for your left-click when ready.";
                    if(bodies.Any(b=>b.owner==cast.id&&b.GetComponent<AssemblyRouting>()?.Phasing==true))cast.status="Recovering a stuck particle: phasing through obstacles into its slot. Hold aim steady.";
                    state.ready=state.ready||state.stable>=.3f;
                    if(state.ready&&group.Any(c=>c.launchGroup>=0))cast.status="READY — left-click to launch. [ / ] spin: "+string.Join(", ",group.Where(c=>c.spin!=0).Select(c=>c.spin.ToString("F2")))+" rad/s.";
                    if(state.ready&&!group.Any(c=>c.launchGroup>=0))
                    {
                        state.launched=true;state.direction=group.Any(c=>c.launchGroup>=0)?motor.view.transform.forward.normalized:Vector3.zero;
                        feedback?.Cast(cast.drawing.target);Say("Compound assembled. Launching all connected branches along captured aim.");
                    }
                }
                else
                {
                    var previous=state.center+state.direction*state.distance;state.elapsed+=dt;state.speed=Mathf.MoveTowards(state.speed,group.Min(c=>c.speed),dt*12);
                    state.distance=Mathf.Min(group.Min(c=>c.launchRange),state.distance+state.speed*dt);SweepFieldLaunch(cast,state,previous);
                }
            }
        }
        void BuildVolumeCells(Construction drawing,GlyphNode node,Channel c,IReadOnlyList<Vector2> profile)
        {
            if(profile.Count<4||Vector2.Distance(profile[0],profile[profile.Count-1])>.08f)throw new InvalidOperationException("VOLUME needs a closed profile; it revolves around the page's vertical axis.");
            float scale=drawing.scale*node.geometryScale;
            float radius=profile.Max(p=>Mathf.Abs(p.x)),lo=profile.Min(p=>p.y),hi=profile.Max(p=>p.y);
            if(radius<.001f||hi-lo<.002f)throw new InvalidOperationException("Volume profile has no thickness.");
            const int n=5;float sx=radius*2*scale/n,sy=(hi-lo)*scale/n;
            var frame=drawing.Frame(node);var center=drawing.Map(Vector2.zero,true)+node.geometryOffset;
            for(int x=0;x<n;x++)for(int y=0;y<n;y++)for(int z=0;z<n;z++)
            {
                float px=-radius+(x+.5f)*radius*2/n,pz=-radius+(z+.5f)*radius*2/n,py=lo+(y+.5f)*(hi-lo)/n;
                float radial=Mathf.Sqrt(px*px+pz*pz);
                if(DrawnGeometry.Contains(profile,new Vector2(radial,py))||DrawnGeometry.Contains(profile,new Vector2(-radial,py)))c.cells.Add(center+frame*new Vector3(px*scale,py*scale,pz*scale));
            }
            if(c.cells.Count==0)throw new InvalidOperationException("Volume is below the material grid resolution. Enlarge its profile.");
            c.volume=true;c.cellRotation=frame;c.cellSize=new Vector3(sx,sy,sx);c.anchor=center+frame*new Vector3(0,(lo+hi)*.5f*scale,0);
            if(c.element==Rune.Water||c.element==Rune.Stone)
            {
                c.requiredMass=c.cells.Count*sx*sy*sx*(c.element==Rune.Water?1000:2400);
                if(c.amount>=c.requiredMass){c.amount=c.requiredMass;c.rate=c.amount/4;}
            }
        }
    }
}
