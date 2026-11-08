using System;
using System.Linq;
using UnityEngine;

namespace Arcana.Elemental
{
    public sealed partial class ElementalWorld
    {
        // A finite magical field stores paid electrical energy in a conducting
        // material. This is an explicit game constitutive rule, not water's capacitance.
        bool EnergizeConductors(CastState cast,Channel channel,float dt)
        {
            if(channel.combine<0||!string.IsNullOrEmpty(channel.targetId))return false;
            var indices=cast.channels.Select((c,i)=>(c,i)).Where(x=>x.c.combine==channel.combine&&x.c.element==Rune.Water).Select(x=>x.i).ToArray();
            if(indices.Length==0)return false;
            var ids=guided.Where(g=>g.castId==cast.id&&indices.Contains(g.channel)).Select(g=>g.bodyId).ToHashSet();
            var conductors=bodies.Where(b=>ids.Contains(b.id)&&b.kind==MatterKind.Water&&b.phase==MatterPhase.Liquid).ToArray();
            double capacity=conductors.Sum(b=>Math.Max(0,b.kg*10000-b.electricalJ));
            if(capacity>0)
            {
                double paid=ledger.Pay(Math.Min(capacity,channel.power*dt),"discharge");
                foreach(var b in conductors)b.electricalJ+=paid*Math.Max(0,b.kg*10000-b.electricalJ)/capacity;
            }
            if(conductors.Length>1)
            {
                int start=(int)(simulationSteps/7)%conductors.Length;var first=conductors[start];
                var nearest=conductors.Where(b=>b!=first&&Vector3.Distance(first.Body.position,b.Body.position)<first.radius+b.radius+1).OrderBy(b=>Vector3.Distance(first.Body.position,b.Body.position)).Take(3).ToArray();
                var center=conductors.Aggregate(Vector3.zero,(sum,b)=>sum+b.Body.position)/conductors.Length;
                Vector3 Surface(MatterBody b)=>b.Body.position+(b.Body.position-center).normalized*Mathf.Max(b.radius,b.sheetSize.magnitude*.35f);
                var points=new System.Collections.Generic.List<Vector3>{Surface(first)};
                foreach(var b in nearest){points.Add((Surface(first)+Surface(b))*.5f+Vector3.up*.08f);points.Add(Surface(b));}
                SetBeam(cast.id+"stored-electric-"+channel.combine,points.ToArray(),new Color(.65f,.9f,1));
            }
            return true;
        }
        public void ElectricalContact(MatterBody body,Collider other,Vector3 point)
        {
            if(body.electricalJ<=0)return;
            var material=other.GetComponentInParent<MatterBody>();var post=other.GetComponentInParent<ConductivePost>();
            if(material!=null&&material.owner==body.owner&&body.Controlled)return;
            if(post!=null&&post.insulated)return;
            bool conductive=post!=null||material!=null&&(material.kind==MatterKind.Water&&material.phase==MatterPhase.Liquid||material.wetness>0);
            if(!conductive)return;
            double delivered=body.electricalJ;body.electricalJ=0;
            if(post!=null)post.Receive(delivered*.9);else material.AddHeat(delivered*.9);
            body.AddHeat(delivered*.1);Burst(point,new Color(.7f,.9f,1),12,Vector3.up);
        }
    }
}
