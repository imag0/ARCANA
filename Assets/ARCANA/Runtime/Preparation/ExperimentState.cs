using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Arcana.Core;
using Arcana.Mechanics;

namespace Arcana
{
    public enum ExperimentPhase {Passive,Eligible,Prepared,Computing,Measured,InsufficientEnergy,Unsupported,Overstressed,Broken}
    public sealed class ExperimentState : MonoBehaviour
    {
        public LabConfig config;public DrawingSurface[] surfaces;public Geometry Geometry {get;private set;}=new Geometry();
        public Ledger Ledger {get;private set;}=new Ledger();
        public MechanicalResult Mechanics {get;private set;}
        public int Revision {get;private set;}
        public ExperimentPhase Phase {get;set;}=ExperimentPhase.Passive;
        public string Notice {get;private set;}="Draw a passive line on a panel. Close a polygon to install a mechanical fixture.";
        public double preload=.008;
        public event Action Changed;public event Action Latched;
        readonly Stack<Geometry> undo=new Stack<Geometry>(),redo=new Stack<Geometry>();
        int nextSupport;
        void Awake(){config.Validate();Ledger=new Ledger{initial=config.initialReserve,reserve=config.initialReserve};preload=config.defaultPreload;Mechanics=Solver.Solve(Geometry);}
        public DrawingSurface Surface(string id)=>Array.Find(surfaces,s=>s.stableId==id);
        public void Message(string message){Notice=message;}
        public bool Apply(Geometry next,string message,bool autoPrepare=false,bool remember=true)
        {
            var graph=Graph.Build(next);bool installed=false;
            if(autoPrepare&&graph.supported)foreach(var c in graph.components.Where(c=>c.Closed))installed|=Solver.InstallFixture(next,graph,c,preload);
            var trialLedger=Ledger.Copy();var result=Solver.Commit(next,trialLedger,out var adjustment);
            if(!result.valid){Notice=result.reason;Phase=ExperimentPhase.Unsupported;return false;}
            if(remember){undo.Push(Geometry.Copy());if(undo.Count>40){var top=undo.Take(40).Reverse().ToArray();undo.Clear();foreach(var item in top)undo.Push(item);}redo.Clear();}
            Geometry=next;Ledger=trialLedger;Mechanics=result;Revision++;
            Phase=result.energy>1e-18?ExperimentPhase.Prepared:(graph.components.Any(c=>c.Closed)?ExperimentPhase.Eligible:ExperimentPhase.Passive);
            Notice=adjustment.Length>0?adjustment:message;
            if(adjustment.StartsWith("Insufficient"))Phase=ExperimentPhase.InsufficientEnergy;
            Changed?.Invoke();if(installed&&result.energy>0)Latched?.Invoke();return true;
        }
        public bool AddStroke(Stroke stroke){if(stroke.points.Count<2)return false;var next=Geometry.Copy();next.strokes.Add(stroke);return Apply(next,stroke.bonded?"Bonded stroke committed":"Insulated stroke: crossings remain independent",true);}
        public void Prepare(string strokeId)
        {
            var next=Geometry.Copy();var graph=Graph.Build(next);var c=graph.components.FirstOrDefault(c=>c.edges.Any(e=>graph.edges[e].stroke.id==strokeId));
            if(c==null){Notice="Aim at a stroke to install supports";return;}
            if(!Solver.InstallFixture(next,graph,c,preload)){Notice="Fixture already installed; wheel changes its preload";return;}
            if(Apply(next,"Two panel-mounted supports installed; work debited"))Latched?.Invoke();
        }
        public void AdjustPreload(double delta)
        {
            preload=Numbers.Clamp(preload+delta,-.08,.08);var next=Geometry.Copy();foreach(var support in next.supports)if(Math.Abs(support.displacement)>1e-14)support.displacement=preload;
            // Zero preload must remain adjustable: the second support in each fixture retains its role.
            for(int i=1;i<next.supports.Count;i+=2)next.supports[i].displacement=preload;
            Apply(next,$"Requested relative displacement {preload:G4} ell");
        }
        public void Undo(){if(undo.Count==0)return;var candidate=undo.Peek().Copy();var old=Geometry.Copy();if(Apply(candidate,"Undo: work/release reconciled; no energy refund",false,false)){undo.Pop();redo.Push(old);}}
        public void Redo(){if(redo.Count==0)return;var candidate=redo.Peek().Copy();var old=Geometry.Copy();if(Apply(candidate,"Redo: mechanical work debited if needed",false,false)){redo.Pop();undo.Push(old);}}
        public void Delete(string id){var next=Geometry.Copy();next.strokes.RemoveAll(s=>s.id==id);next.supports.RemoveAll(s=>s.strokeId==id);Apply(next,"Stroke removed; released energy is unrecovered");}
        public bool Cut(string id,int segment,D3 point)
        {
            var next=Geometry.Copy();var s=next.strokes.Find(s=>s.id==id);if(s==null||segment<0||segment>=s.points.Count-1)return false;
            var a=s.points[segment];var b=s.points[segment+1];var direction=(b-a).Unit;double gap=Graph.ContactTolerance*2.5;
            var left=s.Copy();var right=s.Copy();right.id=Guid.NewGuid().ToString("N");
            left.points=s.points.Take(segment+1).ToList();right.points=s.points.Skip(segment+1).ToList();
            if(D3.Distance(a,point)>gap)left.points.Add(point-direction*gap/2);
            if(D3.Distance(b,point)>gap)right.points.Insert(0,point+direction*gap/2);
            next.strokes.Remove(s);if(left.points.Count>=2)next.strokes.Add(left);if(right.points.Count>=2)next.strokes.Add(right);
            foreach(var support in next.supports.Where(v=>v.strokeId==s.id))if(right.points.Any(p=>D3.Distance(p,support.position)<Graph.ContactTolerance))support.strokeId=right.id;
            bool ok=Apply(next,"Cut: remaining supports re-solved; released work -> unrecovered ledger");if(ok)Phase=ExperimentPhase.Broken;return ok;
        }
        public bool Join(string id)
        {
            var original=Geometry.strokes.Find(s=>s.id==id);if(original==null)return false;
            double best=.15;Stroke otherBest=null;D3 from=default,to=default;
            foreach(var other in Geometry.strokes)if(other.surface==original.surface&&other.polarization==original.polarization)
                foreach(var a in new[]{original.points[0],original.points[original.points.Count-1]})foreach(var b in new[]{other.points[0],other.points[other.points.Count-1]}){double d=D3.Distance(a,b);if(d>Graph.ContactTolerance&&d<best){best=d;otherBest=other;from=a;to=b;}}
            if(otherBest==null){Notice="No compatible endpoint within 0.15 ell";return false;}
            if(!original.bonded||!otherBest.bonded){Notice="Joining requires bonded endpoints; insulated strands stay independent";return false;}
            var connector=original.Copy();connector.id=Guid.NewGuid().ToString("N");connector.points=new List<D3>{from,to};return AddStroke(connector);
        }
        public void Wind(){Ledger.Wind(.001);Notice="Winding station: +1.000e-3 external work";Changed?.Invoke();}
        public void ReleaseSupports(string strokeId)
        {
            var graph=Mechanics.graph;var component=graph.components.FirstOrDefault(c=>c.edges.Any(e=>graph.edges[e].stroke.id==strokeId));if(component==null){Notice="Select a stroke with E first";return;}
            var next=Geometry.Copy();next.supports.RemoveAll(s=>component.nodes.Contains(graph.FindSupport(s)));Apply(next,"Supports released; a free closed loop remains passive. Released work is unrecovered.");
        }
        public void RepositionSupport(string surfaceId,D3 point)
        {
            var graph=Mechanics.graph;int node=-1;double best=.12;for(int i=0;i<graph.nodes.Count;i++){var n=graph.nodes[i];double d=D3.Distance(n.position,point);if(n.surface==surfaceId&&d<best){node=i;best=d;}}
            var next=Geometry.Copy();var supports=next.supports.Where(s=>s.surface==surfaceId).ToArray();if(node<0||supports.Length<2){Notice="Install a fixture, then aim within 0.12 ell of a vertex; E moves supports alternately";return;}
            var target=graph.nodes[node];int index=nextSupport%supports.Length;for(int i=0;i<supports.Length;i++)if(i!=index&&D3.Distance(supports[i].position,target.position)<Graph.ContactTolerance){Notice="The two supports must occupy distinct vertices";return;}
            supports[index].position=target.position;supports[index].strokeId=target.owner;if(Apply(next,"Moved support "+(index+1)+"; next E moves support "+((index+1)%supports.Length+1)))nextSupport++;
        }
        public void LoadSnapshot(Snapshot snapshot)
        {
            snapshot.Validate();foreach(var s in snapshot.geometry.strokes){var surface=Surface(s.surface);if(surface==null||s.points.Any(p=>!surface.Contains(p)))throw new ArgumentException("Unknown surface or out-of-bounds geometry");}
            var mechanics=Solver.Solve(snapshot.geometry);if(!mechanics.valid||mechanics.maxStrain>.05000001||mechanics.maxReaction>1.000001||Math.Abs(mechanics.energy-snapshot.ledger.stored)>1e-9)throw new ArgumentException("Snapshot mechanics/ledger mismatch");
            Geometry=snapshot.geometry.Copy();Ledger=snapshot.ledger.Copy();preload=snapshot.preload;Mechanics=mechanics;undo.Clear();redo.Clear();Revision++;Phase=ExperimentPhase.Prepared;Notice="Snapshot restored, including geometry, fixtures and complete energy ledger";Changed?.Invoke();
        }
        public void ResetExperiment(){var next=new Geometry();Apply(next,"Cleared experiment; released work recorded. Wind to replenish.");}
        public void Demo(bool benchmark)
        {
            var next=new Geometry();var panel=Surface(benchmark?"stone":"paper");var s=panel.NewStroke(true,.03);
            s.points=benchmark?new List<D3>{new D3(-.2,0,0),new D3(.2,0,0)}:new List<D3>{new D3(-.42,-.3,0),new D3(.42,-.3,0),new D3(.42,.3,0),new D3(-.42,.3,0),new D3(-.42,-.3,0)};
            next.strokes.Add(s);var graph=Graph.Build(next);Solver.InstallFixture(next,graph,graph.components[0],benchmark?.008:.016);
            if(Apply(next,benchmark?"W fixture: L=0.4, r=0.03, strain=0.02; probe at research (0,0.5,0)":"Guided polygon: two visible supports; inspect the calculated strain and signal"))Latched?.Invoke();
        }
        public void CrossingDemo()
        {
            var next=new Geometry();var panel=Surface("metal");
            foreach(bool bonded in new[]{true,false}){double offset=bonded?-.62:.62;var a=panel.NewStroke(bonded,.018);a.points.Add(new D3(offset-.4,0,0));a.points.Add(new D3(offset+.4,0,0));var b=panel.NewStroke(bonded,.018);b.points.Add(new D3(offset,-.4,0));b.points.Add(new D3(offset,.4,0));next.strokes.Add(a);next.strokes.Add(b);}
            Apply(next,"LEFT: bonded junction (one component). RIGHT: insulated crossing (two components).");
        }
    }
}
