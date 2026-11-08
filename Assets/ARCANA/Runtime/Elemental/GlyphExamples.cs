using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arcana.Elemental
{
    public static class GlyphExamples
    {
        public static Construction Create(string name,Vector3 origin,Vector3 forward,IEnumerable<MaterialSource> sources)
        {
            var c=new Construction{name=name,origin=origin,forward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized,target=origin+Vector3.ProjectOnPlane(forward,Vector3.up).normalized*6+Vector3.up*2,scale=7,height=2,vertical=true,speed=3};
            GlyphNode Add(Rune rune,float x,float y){return c.Add(rune,new Vector2(x,y));}
            void Edge(GlyphNode a,GlyphNode b)=>c.Connect(a.id,b.id);
            MaterialSource Source(MatterKind kind)=>sources.Where(s=>s.kind==kind&&s.Available>0).OrderBy(s=>Vector3.Distance(s.Position,origin)).FirstOrDefault();
            var element=Add(name=="Stone orbit"?Rune.Stone:name=="Flame vortex"?Rune.Air:Rune.Water,-.68f,.4f);element.sourceId=Source(name=="Stone orbit"?MatterKind.Stone:MatterKind.Water)?.stableId??"";element.intensity=2;
            var gather=Add(Rune.Gather,-.28f,.4f);gather.width=name=="Ice bridge"?500:8;Edge(element,gather);
            if(name=="Water route")
            {
                var direct=Add(Rune.Direct,.15f,.4f);var end=Add(Rune.End,.58f,.4f);Edge(gather,direct);Edge(direct,end);
                c.trajectory=new List<Vector2>{new Vector2(-.7f,-.2f),new Vector2(-.3f,.15f),new Vector2(.05f,.32f),new Vector2(.4f,.1f),new Vector2(.7f,-.3f)};
            }
            else if(name=="Stone orbit")
            {
                var form=Add(Rune.Form,.1f,.4f);var spin=Add(Rune.Spin,.48f,.4f);var hold=Add(Rune.Hold,.65f,-.15f);Edge(gather,form);Edge(form,spin);Edge(spin,hold);hold.duration=3;c.vertical=false;c.scale=3;c.height=2;
                for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;c.form.Add(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.35f);}c.origin=c.target-Vector3.up*2;
            }
            else if(name=="Ice bridge")
            {
                element.intensity=3;
                var form=Add(Rune.Form,.12f,.4f);var combine=Add(Rune.Combine,.52f,.1f);var frost=Add(Rune.Frost,-.2f,-.4f);var hold=Add(Rune.Hold,.65f,-.42f);frost.intensity=3;frost.duration=3;hold.duration=3;Edge(gather,form);Edge(form,combine);Edge(frost,combine);Edge(combine,hold);
                c.vertical=false;c.scale=8;c.height=.04f;c.target=origin+forward*6+Vector3.up*.04f;c.origin=c.target-Vector3.up*.04f;
                // Editable polygon and frame; the nearby fixture places this across the actual banks.
                if(sources.Any(s=>s.stableId=="bridge-water")&&Vector3.Distance(origin,new Vector3(24,0,4))<12){c.origin=new Vector3(30,0,10);c.forward=Vector3.forward;c.target=c.origin+Vector3.up*c.height;element.sourceId="bridge-water";}
                c.form=new List<Vector2>{new Vector2(-.5f,-.05f),new Vector2(.5f,-.05f),new Vector2(.5f,.05f),new Vector2(-.5f,.05f),new Vector2(-.5f,-.05f)};
            }
            else
            {
                var spin=Add(Rune.Spin,.12f,.4f);var fire=Add(Rune.Fire,-.25f,-.35f);var combine=Add(Rune.Combine,.52f,.1f);var hold=Add(Rune.Hold,.64f,-.4f);fire.intensity=2;fire.duration=3;hold.duration=3;Edge(gather,spin);Edge(spin,combine);Edge(fire,combine);Edge(combine,hold);c.height=2;
            }
            // The example is editable, dormant data: only a player's final seal stroke commits it.
            for(int i=0;i<61;i++){float a=(20+i*5.3f)*Mathf.Deg2Rad;c.seal.Add(new Vector2(Mathf.Cos(a)*.93f,Mathf.Sin(a)*.88f));}
            return c;
        }
    }
}
