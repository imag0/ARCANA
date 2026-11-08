using System;
using System.Linq;
using UnityEngine;

namespace Arcana.Elemental
{
    public sealed partial class ElementalWorld
    {
        public int impactEvents;
        public void LaunchContact(MatterBody body,Collider other,Vector3 point,Vector3 normal)
        {
            var guide=guided.Find(g=>g.bodyId==body.id);if(guide==null)return;
            var cast=casts.Find(c=>c.id==guide.castId);if(cast==null)return;var channel=cast.channels[guide.channel];var state=Launch(cast,channel);
            if(channel.launchGroup<0||state==null||!state.launched||state.impacted||state.distance<.05f||other.GetComponentInParent<FirstPersonMotor>()!=null)return;
            var peer=other.GetComponentInParent<MatterBody>();var peerGuide=peer==null?null:guided.Find(g=>g.bodyId==peer.id);
            if(peerGuide!=null&&peerGuide.castId==cast.id&&AssemblyGroup(cast.channels[peerGuide.channel])==state.node)return;
            BreakLaunch(cast,state,point,normal);
        }
        void BreakLaunch(CastState cast,LaunchState state,Vector3 point,Vector3 normal)
        {
            if(state.impacted)return;state.impacted=true;state.impactPoint=point;impactEvents++;
            var indices=cast.channels.Select((c,i)=>(c,i)).Where(v=>AssemblyGroup(v.c)==state.node).Select(v=>v.i).ToArray();
            var members=guided.Where(g=>g.castId==cast.id&&indices.Contains(g.channel)).ToArray();var center=state.center+state.direction*state.distance;
            foreach(var guide in members)
            {
                var body=bodies.Find(b=>b.id==guide.bodyId);if(body==null)continue;RemoveIceBonds(body);body.owner="";body.fractured=true;body.sheetSize=Vector3.zero;body.SetSheet(Vector3.zero);body.Refresh();
                var radial=(body.Body.position-center).normalized;if(radial.sqrMagnitude<.01f)radial=normal;
                var before=body.Body.linearVelocity;var after=before*.4f+radial*(2+state.speed*.35f)+normal*.8f;
                double increase=.5*body.kg*(after.sqrMagnitude-before.sqrMagnitude);float fraction=1;
                if(increase>0)fraction=(float)(ledger.Pay(increase,"drive")/increase);else body.AddHeat(-increase);
                body.Body.AddForce((after-before)*fraction,ForceMode.VelocityChange);
                Burst(body.Body.position,body.kind==MatterKind.Water?new Color(.2f,.65f,.9f):Palette.Stone,4,after,false,body.kind==MatterKind.Water?.09f:.15f,.7f,body.kind==MatterKind.Water?0:2);
            }
            guided.RemoveAll(g=>g.castId==cast.id&&indices.Contains(g.channel));foreach(int i in indices)cast.channels[i].ended=true;
            if(members.Length==0)Burst(point,Palette.Teal,40,normal*3,false,.18f,.7f);
            if(cast.channels.All(c=>c.ended)){cast.active=false;cast.status="Impacted: material fragments released";}
            feedback?.Impact(point,6);Say("Impact! The launched compound broke into physical fragments. Material is preserved; breakup energy is accounted for.");
        }
        void SweepFieldLaunch(CastState cast,LaunchState state,Vector3 previous)
        {
            if(cast.channels.Any(c=>AssemblyGroup(c)==state.node&&(c.element==Rune.Water||c.element==Rune.Stone)))return;
            var now=state.center+state.direction*state.distance;var delta=now-previous;if(delta.sqrMagnitude<1e-8)return;
            foreach(var hit in Physics.SphereCastAll(previous,.2f,delta.normalized,delta.magnitude,~(1<<2),QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {if(hit.collider.GetComponentInParent<FirstPersonMotor>()!=null)continue;BreakLaunch(cast,state,hit.point,hit.normal);break;}
        }
    }
}
