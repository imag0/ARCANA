using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arcana.Elemental
{
    [Serializable] public sealed class MaterialLot
    {
        public string origin;public MatterKind kind;public double kg,enthalpy;
        public MaterialLot Copy()=>new MaterialLot{origin=origin,kind=kind,kg=kg,enthalpy=enthalpy};
    }
    [Serializable] public sealed class SourceState { public string id,area,label;public MatterKind kind;public Vector3 position;public bool collector;public List<MaterialLot> lots=new List<MaterialLot>(); }
    public sealed class MaterialSource : MonoBehaviour
    {
        public int introducedVersion=1;public string stableId,area,label;public MatterKind kind;public double initialKg;public Transform fill,outlet;public bool collector;
        public List<MaterialLot> lots=new List<MaterialLot>();
        public Vector3 Position=>outlet!=null?outlet.position:transform.position+Vector3.up;
        public double Available{get{double sum=0;foreach(var lot in lots)if(lot.kind==kind)sum+=lot.kg;return sum;}}
        void Awake(){if(lots.Count==0&&initialKg>0)lots.Add(new MaterialLot{origin=stableId,kind=kind,kg=initialKg,enthalpy=kind==MatterKind.Water?initialKg*84000:0});}
        void Update(){if(fill!=null){var s=fill.localScale;s.y=Mathf.Max(.02f,(float)(Available/Math.Max(initialKg,100)));fill.localScale=s;}}
        public List<MaterialLot> Withdraw(double amount)
        {
            var taken=new List<MaterialLot>();for(int i=0;i<lots.Count&&amount>1e-9;i++)
            {var lot=lots[i];if(lot.kind!=kind||lot.kg<=0)continue;double n=Math.Min(amount,lot.kg),ratio=n/lot.kg;var copy=lot.Copy();copy.kg=n;copy.enthalpy=lot.enthalpy*ratio;taken.Add(copy);lot.kg-=n;lot.enthalpy-=copy.enthalpy;amount-=n;}
            lots.RemoveAll(l=>l.kg<1e-10);return taken;
        }
        public void Deposit(MaterialLot lot){var current=lots.Find(l=>l.origin==lot.origin&&l.kind==lot.kind);if(current==null)lots.Add(lot.Copy());else{current.kg+=lot.kg;current.enthalpy+=lot.enthalpy;}}
        public void Collect(MatterBody body)
        {if(!collector||body==null||body.kind!=kind||body.phase!=MatterPhase.Liquid||body.kg<=0||body.Controlled||body.Body.linearVelocity.magnitude>2||Vector3.Distance(body.Body.position,transform.position)>2)return;Deposit(new MaterialLot{origin=body.origin,kind=body.kind,kg=body.kg,enthalpy=body.enthalpy+body.electricalJ});body.world.Retire(body);}
        void OnTriggerStay(Collider other){Collect(other.GetComponentInParent<MatterBody>());}
    }
}
