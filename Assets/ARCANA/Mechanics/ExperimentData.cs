using System;
using System.Collections.Generic;
using Arcana.Core;

namespace Arcana.Mechanics
{
    [Serializable] public sealed class Stroke
    {
        public string id=Guid.NewGuid().ToString("N"),surface="paper",material="paper";
        public bool bonded=true,supported=true;
        public double C=50,rho=50,radius=.012;
        public int polarization=0;
        public List<D3> points=new List<D3>();
        public D3 normal=new D3(0,0,1);
        public Stroke Copy()=>new Stroke{id=id,surface=surface,material=material,bonded=bonded,supported=supported,C=C,rho=rho,radius=radius,polarization=polarization,normal=normal,points=new List<D3>(points)};
    }
    [Serializable] public sealed class Support
    {
        public string id=Guid.NewGuid().ToString("N"),surface,strokeId;
        public D3 position;
        public double displacement,loadLimit=.15;
        public Support Copy()=>new Support{id=id,surface=surface,strokeId=strokeId,position=position,displacement=displacement,loadLimit=loadLimit};
    }
    [Serializable] public sealed class Geometry
    {
        public List<Stroke> strokes=new List<Stroke>();
        public List<Support> supports=new List<Support>();
        public Geometry Copy(){var g=new Geometry();foreach(var s in strokes)g.strokes.Add(s.Copy());foreach(var s in supports)g.supports.Add(s.Copy());return g;}
    }
    [Serializable] public sealed class Ledger
    {
        public double initial=.002,reserve=.002,stored,released,external;
        public double Residual=>initial+external-reserve-stored-released;
        public void Reconcile(double energy)
        {
            if(!Numbers.Finite(energy)||energy<0)throw new ArgumentException("Invalid energy");
            double change=energy-stored;
            if(change>reserve+1e-12)throw new InvalidOperationException("Insufficient energy");
            if(change>0)reserve=Math.Max(0,reserve-change);else released-=change;
            stored=energy;
        }
        public void Wind(double work){if(!Numbers.Finite(work)||work<=0)throw new ArgumentException("Positive external work required");reserve+=work;external+=work;}
        public Ledger Copy()=>new Ledger{initial=initial,reserve=reserve,stored=stored,released=released,external=external};
    }
    [Serializable] public sealed class Snapshot
    {
        public int version=1;
        public Geometry geometry=new Geometry();
        public Ledger ledger=new Ledger();
        public D3 probePosition=new D3(0,1,0),probeAxis=new D3(1,0,0);
        public double probeLength=.05,probeRadius=.002,preload=.008;
        public void Validate()
        {
            if(version!=1||geometry==null||ledger==null||geometry.strokes==null||geometry.supports==null||geometry.strokes.Count>256||geometry.supports.Count>512)throw new ArgumentException("Unsupported snapshot version or structure");
            var ids=new HashSet<string>();int count=0;
            foreach(var s in geometry.strokes){if(s==null||string.IsNullOrEmpty(s.id)||!ids.Add(s.id)||string.IsNullOrEmpty(s.surface)||s.points==null||s.points.Count<2||s.points.Count>2048||s.C<=0||s.rho<=0||s.radius<.001||s.radius>.05||!Numbers.Finite(s.C+s.rho+s.radius)||!s.normal.Finite)throw new ArgumentException("Invalid stroke");foreach(var p in s.points)if(!p.Finite||p.Length>100)throw new ArgumentException("Invalid coordinate");count+=s.points.Count;}
            if(count>4096)throw new ArgumentException("Snapshot exceeds geometry budget");
            foreach(var s in geometry.supports)if(s==null||!s.position.Finite||!Numbers.Finite(s.displacement+s.loadLimit)||Math.Abs(s.displacement)>.5||s.loadLimit<=0||string.IsNullOrEmpty(s.surface))throw new ArgumentException("Invalid support");
            if(!probePosition.Finite||probePosition.Length>100||!probeAxis.Finite||Math.Abs(probeAxis.Length-1)>1e-5||probeLength<=0||probeLength>1||probeRadius<=0||probeRadius>.05||!Numbers.Finite(probeLength+probeRadius+preload))throw new ArgumentException("Invalid probe");
            if(!Numbers.Finite(ledger.initial+ledger.external+ledger.reserve+ledger.stored+ledger.released)||ledger.initial<0||ledger.external<0||ledger.reserve<0||ledger.stored<0||ledger.released<0||Math.Abs(ledger.Residual)>1e-9)throw new ArgumentException("Invalid energy ledger");
        }
    }
}
