using System;
using System.Collections.Generic;
using System.Linq;
using Arcana.Core;

namespace Arcana.Mechanics
{
    public sealed class Node { public D3 position;public string surface,owner;public int polarization;public bool bonded;public double u,reaction; }
    public sealed class Edge { public int a,b;public Stroke stroke;public double length,strain;public double Area=>Math.PI*stroke.radius*stroke.radius;public double Stiffness=>stroke.C*Area/length; }
    public sealed class Component {public List<int> nodes=new List<int>(),edges=new List<int>();public bool Closed=>edges.Count>=nodes.Count;}
    public sealed class Graph
    {
        public const double ContactTolerance=.022;
        public readonly List<Node> nodes=new List<Node>();
        public readonly List<Edge> edges=new List<Edge>();
        public readonly List<Component> components=new List<Component>();
        public bool supported=true;
        public string limitation="";
        sealed class Piece {public D3 a,b;public Stroke stroke;public List<double> cuts=new List<double>{0,1};}
        public static Graph Build(Geometry geometry)
        {
            var g=new Graph();var pieces=new List<Piece>();
            foreach(var s in geometry.strokes)
            {
                if(!s.supported){g.supported=false;g.limitation="Nonplanar/unsupported surface: drawing remains editable";continue;}
                for(int i=1;i<s.points.Count;i++)if(D3.Distance(s.points[i-1],s.points[i])>1e-6)pieces.Add(new Piece{a=s.points[i-1],b=s.points[i],stroke=s});
            }
            if(pieces.Count>768){g.supported=false;g.limitation="768 segment numerical budget exceeded";return g;}
            for(int i=0;i<pieces.Count;i++)for(int j=i+1;j<pieces.Count;j++)
            {
                var p=pieces[i];var q=pieces[j];
                if(p.stroke.surface!=q.stroke.surface||p.stroke.polarization!=q.stroke.polarization||(!(p.stroke.bonded&&q.stroke.bonded)&&p.stroke.id!=q.stroke.id))continue;
                Closest(p.a,p.b,q.a,q.b,out double t,out double u,out double distance);
                if(distance<=ContactTolerance){p.cuts.Add(t);q.cuts.Add(u);}
            }
            foreach(var p in pieces)
            {
                var cuts=p.cuts.OrderBy(v=>v).ToList();int last=-1;
                foreach(var t in cuts)
                {
                    var point=D3.Lerp(p.a,p.b,t);int index=g.NodeAt(point,p.stroke);
                    if(last>=0&&index!=last){double l=D3.Distance(g.nodes[last].position,g.nodes[index].position);if(l>1e-6)g.edges.Add(new Edge{a=last,b=index,length=l,stroke=p.stroke});}
                    last=index;
                }
            }
            var visited=new HashSet<int>();
            for(int n=0;n<g.nodes.Count;n++)if(visited.Add(n))
            {
                var c=new Component();var queue=new Queue<int>();queue.Enqueue(n);
                while(queue.Count>0){int k=queue.Dequeue();c.nodes.Add(k);for(int e=0;e<g.edges.Count;e++){var edge=g.edges[e];if(edge.a!=k&&edge.b!=k)continue;if(!c.edges.Contains(e))c.edges.Add(e);int other=edge.a==k?edge.b:edge.a;if(visited.Add(other))queue.Enqueue(other);}}
                g.components.Add(c);
            }
            return g;
        }
        int NodeAt(D3 point,Stroke s)
        {
            for(int i=0;i<nodes.Count;i++){var n=nodes[i];if(n.surface==s.surface&&n.polarization==s.polarization&&(n.bonded&&s.bonded||n.owner==s.id)&&D3.Distance(point,n.position)<=ContactTolerance)return i;}
            nodes.Add(new Node{position=point,surface=s.surface,owner=s.id,polarization=s.polarization,bonded=s.bonded});return nodes.Count-1;
        }
        public int FindSupport(Support s)
        {
            int best=-1;double distance=ContactTolerance;
            for(int i=0;i<nodes.Count;i++){var n=nodes[i];if(n.surface!=s.surface||(!n.bonded&&n.owner!=s.strokeId))continue;double d=D3.Distance(n.position,s.position);if(d<=distance){best=i;distance=d;}}
            return best;
        }
        public static void Closest(D3 a,D3 b,D3 c,D3 d,out double s,out double t,out double distance)
        {
            var u=b-a;var v=d-c;var w=a-c;double aa=D3.Dot(u,u),bb=D3.Dot(u,v),cc=D3.Dot(v,v),dd=D3.Dot(u,w),ee=D3.Dot(v,w),den=aa*cc-bb*bb;
            s=den>1e-18?Numbers.Clamp((bb*ee-cc*dd)/den,0,1):0;
            t=cc>1e-18?Numbers.Clamp((bb*s+ee)/cc,0,1):0;
            s=aa>1e-18?Numbers.Clamp((bb*t-dd)/aa,0,1):0;
            distance=D3.Distance(a+u*s,c+v*t);
        }
    }
    public sealed class MechanicalResult
    {
        public Graph graph;public double energy,maxStrain,maxReaction,forceResidual;public bool valid;public string reason;
    }
    public static class Solver
    {
        public static MechanicalResult Solve(Geometry geometry)
        {
            var graph=Graph.Build(geometry);var r=new MechanicalResult{graph=graph,valid=graph.supported,reason=graph.limitation};
            int n=graph.nodes.Count;if(n==0)return r;
            var a=new double[n,n];var rhs=new double[n];var fixedU=new Dictionary<int,double>();
            foreach(var e in graph.edges){double k=e.Stiffness;a[e.a,e.a]+=k;a[e.b,e.b]+=k;a[e.a,e.b]-=k;a[e.b,e.a]-=k;}
            foreach(var s in geometry.supports){int k=graph.FindSupport(s);if(k<0)continue;if(fixedU.TryGetValue(k,out double value)&&Math.Abs(value-s.displacement)>1e-12){r.valid=false;r.reason="Conflicting supports";return r;}fixedU[k]=s.displacement;}
            // An unanchored connected component has one rigid null mode. Explicit u=0 gauge.
            foreach(var c in graph.components)if(!c.nodes.Any(k=>fixedU.ContainsKey(k)))fixedU[c.nodes[0]]=0;
            foreach(var entry in fixedU){int k=entry.Key;for(int i=0;i<n;i++)if(i!=k){rhs[i]-=a[i,k]*entry.Value;a[i,k]=a[k,i]=0;}a[k,k]=1;rhs[k]=entry.Value;}
            try
            {
                var u=LinearSolve(a,rhs);for(int i=0;i<n;i++)graph.nodes[i].u=u[i];
                foreach(var e in graph.edges){double du=u[e.b]-u[e.a];e.strain=du/e.length;r.energy+=e.Stiffness*du*du/2;r.maxStrain=Math.Max(r.maxStrain,Math.Abs(e.strain));graph.nodes[e.a].reaction-=e.Stiffness*du;graph.nodes[e.b].reaction+=e.Stiffness*du;}
                for(int i=0;i<n;i++)if(!fixedU.ContainsKey(i))r.forceResidual=Math.Max(r.forceResidual,Math.Abs(graph.nodes[i].reaction));
                foreach(var s in geometry.supports){int k=graph.FindSupport(s);if(k>=0)r.maxReaction=Math.Max(r.maxReaction,Math.Abs(graph.nodes[k].reaction)/s.loadLimit);}
                if(!Numbers.Finite(r.energy))throw new ArithmeticException("Nonfinite mechanical result");
            }
            catch(Exception ex){r.valid=false;r.reason=ex.Message;}
            return r;
        }
        static double[] LinearSolve(double[,] a,double[] rhs)
        {
            int n=rhs.Length;
            for(int k=0;k<n;k++)
            {
                int p=k;for(int i=k+1;i<n;i++)if(Math.Abs(a[i,k])>Math.Abs(a[p,k]))p=i;
                if(Math.Abs(a[p,k])<1e-14)throw new ArithmeticException("Singular mechanical graph after explicit gauge fixing");
                if(p!=k){for(int j=k;j<n;j++){double tmp=a[k,j];a[k,j]=a[p,j];a[p,j]=tmp;}double v=rhs[k];rhs[k]=rhs[p];rhs[p]=v;}
                for(int i=k+1;i<n;i++){double f=a[i,k]/a[k,k];for(int j=k+1;j<n;j++)a[i,j]-=f*a[k,j];rhs[i]-=f*rhs[k];}
            }
            var x=new double[n];for(int i=n-1;i>=0;i--){double sum=rhs[i];for(int j=i+1;j<n;j++)sum-=a[i,j]*x[j];x[i]=sum/a[i,i];}return x;
        }
        public static MechanicalResult Commit(Geometry geometry,Ledger ledger,out string adjustment)
        {
            var r=Solve(geometry);adjustment="";if(!r.valid)return r;
            double factor=1;
            if(r.maxStrain>.05)factor=Math.Min(factor,.05/r.maxStrain);
            if(r.maxReaction>1)factor=Math.Min(factor,1/r.maxReaction);
            if(r.energy>ledger.stored+ledger.reserve)factor=Math.Min(factor,Math.Sqrt((ledger.stored+ledger.reserve)/r.energy));
            if(factor<1){foreach(var s in geometry.supports)s.displacement*=factor;r=Solve(geometry);adjustment=factor<1e-8?"Insufficient energy; fixture stays passive":"Preload reduced to satisfy energy, strain or support load limit";}
            if(r.valid)ledger.Reconcile(r.energy);return r;
        }
        public static bool InstallFixture(Geometry geometry,Graph graph,Component component,double displacement)
        {
            if(component.nodes.Count<2||geometry.supports.Any(s=>component.nodes.Contains(graph.FindSupport(s))))return false;
            int a=component.nodes.OrderBy(k=>graph.nodes[k].position.x).ThenBy(k=>graph.nodes[k].position.y).ThenBy(k=>graph.nodes[k].position.z).First();
            int b=component.nodes.OrderByDescending(k=>D3.Distance(graph.nodes[k].position,graph.nodes[a].position)).First();
            foreach(int k in new[]{a,b}){var n=graph.nodes[k];geometry.supports.Add(new Support{surface=n.surface,strokeId=n.owner,position=n.position,displacement=k==a?0:displacement});}return true;
        }
    }
}
