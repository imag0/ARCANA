using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arcana.Elemental
{
    public enum Rune { Unknown, Fire, Water, Air, Stone, Frost, Lightning, Light, Gather, Form, Direct, Spin, Hold, Spread, Split, Combine, Target, End, Junction, Assemble, Launch }
    public enum InkLayer { Glyphs, Connectors, Form, Trajectory, Seal, Depth }
    public enum MatterKind { Water, Stone, Soil, Fuel, Air }
    public enum MatterPhase { Liquid, Ice, Steam, Solid, Gas }
    [Serializable] public sealed class InkStroke { public InkLayer layer; public List<Vector2> points=new List<Vector2>(); }
    [Serializable] public sealed class GlyphNode
    {
        public int id; public Rune rune; public Vector2 position; public float confidence=1; public string sourceId="",targetId="";
        public int intensity=1,duration=2; public float width=1,spin=1; public bool tracking,attached; public Vector3 axis=Vector3.up;
        // Optional continuous settings. Zero inherits the original dot/bar setting.
        public float forceN,powerW,seconds,flowSpeed;
        public List<Vector2> sketch=new List<Vector2>(),depthProfile=new List<Vector2>();
        public Vector3 geometryEuler,geometryOffset;
        public float geometryScale=1,profileDepth=4;
        public int fillMode; // 0 legacy, 1 outline, 2 filled polygon
        public bool worldPath;
    }
    [Serializable] public sealed class GlyphEdge { public int from,to; public List<Vector2> points=new List<Vector2>(); }
    [Serializable] public sealed class SealRegion { public int id;public List<Vector2> points=new List<Vector2>();public bool closed; }
    [Serializable] public sealed class Construction
    {
        public string id=Guid.NewGuid().ToString("N"),name="Untitled construction",drawingSurfaceId="";
        public List<GlyphNode> nodes=new List<GlyphNode>(); public List<GlyphEdge> edges=new List<GlyphEdge>(); public List<InkStroke> ink=new List<InkStroke>();
        public List<Vector2> form=new List<Vector2>(),trajectory=new List<Vector2>(),seal=new List<Vector2>();
        public List<SealRegion> innerSeals=new List<SealRegion>();public float spellSize=1;public float canvasZoom=1;public Vector2 canvasPan;
        public Vector3 origin,forward=Vector3.forward,up=Vector3.up,target; public float scale=8,height=2,depth=1,speed=3,launchSpeed=15;
        public bool vertical,sealClosed,committed; public int nextId=1;
        public Construction Copy()=>JsonUtility.FromJson<Construction>(JsonUtility.ToJson(this));
        public GlyphNode Add(Rune rune,Vector2 point){var node=new GlyphNode{id=nextId++,rune=rune,position=point};if(rune==Rune.Launch){node.width=40;}nodes.Add(node);return node;}
        public void Connect(int from,int to){edges.Add(new GlyphEdge{from=from,to=to});}
        public Vector3 Map(Vector2 p,bool shape=false)
        {
            var f=forward.normalized;var r=Vector3.Cross(up.normalized,f).normalized;var y=vertical?up.normalized:f;
            return origin+r*p.x*scale+y*p.y*scale+up.normalized*height+(shape?Vector3.zero:f*depth);
        }
        public IReadOnlyList<Vector2> Sketch(GlyphNode n)=>n.sketch!=null&&n.sketch.Count>1?n.sketch:n.rune==Rune.Form?form:trajectory;
        public Quaternion Frame(GlyphNode n)
        {
            var x=Vector3.Cross(up.normalized,forward.normalized).normalized;var y=vertical?up.normalized:forward.normalized;
            return Quaternion.LookRotation(Vector3.Cross(x,y),y)*Quaternion.Euler(n.geometryEuler);
        }
        public List<Vector3> Geometry(GlyphNode n)
        {
            var ink=DrawnGeometry.Resample(Sketch(n),.02f);var result=new List<Vector3>();float length=0,travel=0;
            for(int i=1;i<ink.Count;i++)length+=Vector2.Distance(ink[i-1],ink[i]);
            var rotation=Frame(n);var center=Map(Vector2.zero,n.rune==Rune.Form)+n.geometryOffset;
            for(int i=0;i<ink.Count;i++){if(i>0)travel+=Vector2.Distance(ink[i-1],ink[i]);float depth=DrawnGeometry.Profile(n.depthProfile,length>0?travel/length:0)*n.profileDepth;result.Add(center+rotation*new Vector3(ink[i].x*scale*n.geometryScale,ink[i].y*scale*n.geometryScale,depth));}
            return result;
        }
    }
    public sealed class GlyphPlan
    {
        public bool valid;public string reason;public List<GlyphNode> order=new List<GlyphNode>();public List<GlyphNode> elements=new List<GlyphNode>();
        public static bool Element(Rune r)=>r>=Rune.Fire&&r<=Rune.Light;
        public bool Has(Rune rune)=>order.Any(n=>n.rune==rune);
        public GlyphNode Find(Rune rune)=>order.FirstOrDefault(n=>n.rune==rune);
        public static GlyphPlan Compile(Construction c)
        {
            var p=new GlyphPlan();bool Fail(string why){p.reason=why;return false;}
            if(c.nodes.Count==0){Fail("Draw an element glyph in a socket first.");return p;}
            if(c.nodes.Count>32||c.edges.Count>64){Fail("Capacity: at most 32 glyphs and 64 connections.");return p;}
            var nodes=new Dictionary<int,GlyphNode>();foreach(var n in c.nodes){if(nodes.ContainsKey(n.id)){Fail("Duplicate node identity.");return p;}nodes.Add(n.id,n);}
            var degree=nodes.Keys.ToDictionary(id=>id,id=>0);var outgoing=nodes.Keys.ToDictionary(id=>id,id=>new List<int>());
            foreach(var e in c.edges){if(!nodes.ContainsKey(e.from)||!nodes.ContainsKey(e.to)||e.from==e.to){Fail("A connector must run from one socket to a different socket.");return p;}outgoing[e.from].Add(e.to);degree[e.to]++;}
            var queue=new Queue<int>(degree.Where(k=>k.Value==0).Select(k=>k.Key));
            while(queue.Count>0){int id=queue.Dequeue();p.order.Add(nodes[id]);foreach(int to in outgoing[id])if(--degree[to]==0)queue.Enqueue(to);}
            if(p.order.Count!=nodes.Count){Fail("Data-flow cycle: remove a connector. The outer seal is a separate layer.");return p;}
            foreach(var n in p.order)
            {
                if(n.rune==Rune.Unknown||n.confidence<.58f){Fail("Uncertain glyph: select its socket and redraw or correct it with the alphabet.");return p;}
                int incoming=c.edges.Count(e=>e.to==n.id);
                if(Element(n.rune)){p.elements.Add(n);if(incoming>0){Fail("Elements start a branch; connect their outputs to operators.");return p;}if(outgoing[n.id].Count==0){Fail(n.rune+" is disconnected. Draw an arrow to an operator.");return p;}}
                else if(incoming==0){Fail(n.rune+" has no input. Connect it or erase the unused socket.");return p;}
                if(incoming>1&&n.rune!=Rune.Combine&&n.rune!=Rune.Junction){Fail("Multiple inputs require an explicit COMBINE or junction dot.");return p;}
                if(outgoing[n.id].Count>1&&n.rune!=Rune.Split){Fail("Multiple outputs require SPLIT; quantities are divided, never copied.");return p;}
                if(n.rune==Rune.End&&outgoing[n.id].Count>0){Fail("END cannot have an outgoing connector.");return p;}
            }
            if(p.elements.Count==0){Fail("This construction has no element.");return p;}
            if(c.innerSeals.Count>16){Fail("Capacity: at most 16 nested seal regions.");return p;}
            foreach(var n in p.order)
            {
                if((n.rune==Rune.Form||n.rune==Rune.Direct)&&c.Sketch(n).Count<2){Fail(n.rune+" needs its own sketch or the shared shape/path.");return p;}
                var values=new[]{n.width,n.spin,n.forceN,n.powerW,n.seconds,n.flowSpeed,n.geometryScale,n.profileDepth,n.axis.x,n.axis.y,n.axis.z,n.geometryEuler.x,n.geometryEuler.y,n.geometryEuler.z,n.geometryOffset.x,n.geometryOffset.y,n.geometryOffset.z};
                if(values.Any(v=>!float.IsFinite(v))||n.geometryScale<=0||n.width<=0||n.forceN<0||n.powerW<0||n.seconds<0||n.flowSpeed<0||n.forceN>1000000||n.powerW>100000000||n.seconds>3600||n.flowSpeed>60||Mathf.Abs(n.spin)>30||n.width>100000){Fail("Invalid numeric setting. Force ≤1 MN, power ≤100 MW, time ≤3600 s, speed ≤60 m/s, spin ≤60 rad/s.");return p;}
                if(n.rune==Rune.Launch&&n.width>100){Fail("LAUNCH range must be at most 100 m.");return p;}
                if(n.rune==Rune.Spin&&n.axis.sqrMagnitude<1e-6){Fail("SPIN needs a nonzero axis vector.");return p;}
                if(n.sketch!=null&&n.sketch.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y))||n.depthProfile!=null&&n.depthProfile.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y))){Fail("Drawing contains a non-finite point.");return p;}
            }
            if(!float.IsFinite(c.spellSize)||c.spellSize<.1f||c.spellSize>4){Fail("Spell size must be 0.1–4×. Use SMALLER / BIGGER.");return p;}
            if(!float.IsFinite(c.scale)||!float.IsFinite(c.speed)||c.scale<.1f||c.scale>100||c.speed<.1f||c.speed>60){Fail("Scale must be 0.1–100 m; speed 0.1–60 m/s.");return p;}
            p.valid=true;p.reason="Ready: close the outer seal gap to cast.";return p;
        }
    }
    public static class DrawnGeometry
    {
        // Profile x is progress along the ink, y is depth; drawing order is irrelevant.
        public static float Profile(IReadOnlyList<Vector2> profile,float t)
        {
            if(profile==null||profile.Count==0)return 0;float x=t*2-1;Vector2 a=new Vector2(-1,profile[0].y),b=new Vector2(1,profile[profile.Count-1].y);
            foreach(var p in profile){if(p.x<=x&&p.x>=a.x)a=p;if(p.x>=x&&p.x<=b.x)b=p;}
            return Mathf.Lerp(a.y,b.y,Mathf.InverseLerp(a.x,b.x,x));
        }
        public static List<Vector2> Resample(IReadOnlyList<Vector2> points,float spacing=.02f)
        {
            var result=new List<Vector2>();if(points.Count==0)return result;result.Add(points[0]);float carried=0;
            for(int i=1;i<points.Count;i++)
            {
                Vector2 a=points[i-1],b=points[i];float length=Vector2.Distance(a,b);if(length<1e-7)continue;var direction=(b-a)/length;
                while(carried+length>=spacing){float step=spacing-carried;a+=direction*step;result.Add(a);length-=step;carried=0;}carried+=length;
            }
            if(Vector2.Distance(result[result.Count-1],points[points.Count-1])>.0001f)result.Add(points[points.Count-1]);return result;
        }
        public static float Length(IReadOnlyList<Vector3> points){float l=0;for(int i=1;i<points.Count;i++)l+=Vector3.Distance(points[i-1],points[i]);return l;}
        public static Vector3 At(IReadOnlyList<Vector3> points,float distance,out Vector3 tangent)
        {
            tangent=Vector3.forward;if(points.Count==0)return Vector3.zero;if(points.Count==1)return points[0];
            for(int i=1;i<points.Count;i++){var d=points[i]-points[i-1];float l=d.magnitude;if(l<1e-6f)continue;tangent=d/l;if(distance<=l)return points[i-1]+tangent*Mathf.Max(0,distance);distance-=l;}return points[points.Count-1];
        }
        public static bool JoinSeal(List<Vector2> contour,IReadOnlyList<Vector2> stroke)
        {
            if(stroke.Count<2)return false;if(contour.Count==0){contour.AddRange(stroke);return true;}
            float[] gaps={Vector2.Distance(contour[contour.Count-1],stroke[0]),Vector2.Distance(contour[contour.Count-1],stroke[stroke.Count-1]),Vector2.Distance(contour[0],stroke[stroke.Count-1]),Vector2.Distance(contour[0],stroke[0])};
            int best=0;for(int i=1;i<4;i++)if(gaps[i]<gaps[best])best=i;if(gaps[best]>.12f)return false;
            var ink=stroke.ToList();if(best==1||best==3)ink.Reverse();if(best<2)contour.AddRange(ink.Skip(1));else contour.InsertRange(0,ink.Take(ink.Count-1));return true;
        }
        public static void TrimClosureTail(List<Vector2> points)
        {
            // Permit a small overshoot after joining the start, while preserving
            // all earlier geometry for the self-intersection and enclosure checks.
            if(points.Count<4)return;float tail=0;int nearest=points.Count-1;float gap=Vector2.Distance(points[0],points[nearest]);
            for(int i=points.Count-2;i>=3;i--){tail+=Vector2.Distance(points[i],points[i+1]);if(tail>.18f)break;float d=Vector2.Distance(points[0],points[i]);if(d<gap){nearest=i;gap=d;}}
            if(gap<=.035f){if(nearest<points.Count-1)points.RemoveRange(nearest+1,points.Count-nearest-1);points[points.Count-1]=points[0];}
        }
        public static bool ClosedSeal(IReadOnlyList<Vector2> points,IEnumerable<GlyphNode> nodes,out string reason)
            =>ClosedContour(points,nodes,.35f,out reason);
        public static bool ClosedContour(IReadOnlyList<Vector2> points,IEnumerable<GlyphNode> nodes,float minimumArea,out string reason)
        {
            reason="Draw a boundary around the sockets, then join its two highlighted endpoints.";if(points.Count<4||Vector2.Distance(points[0],points[points.Count-1])>.07f)return false;
            float area=0;for(int i=1;i<points.Count;i++)area+=points[i-1].x*points[i].y-points[i].x*points[i-1].y;
            if(Mathf.Abs(area)<minimumArea){reason="Seal is too small. Enclose the instruction sockets.";return false;}
            for(int i=1;i<points.Count;i++)for(int j=i+2;j<points.Count;j++){if(i==1&&j==points.Count-1)continue;if(Crosses(points[i-1],points[i],points[j-1],points[j])){reason="Overlapping/self-crossing seal: use one simple outer boundary.";return false;}}
            foreach(var node in nodes)if(!Contains(points,node.position)){reason=node.rune+" socket #"+node.id+" lies outside the outer seal. Enlarge or redraw the seal around every socket.";return false;}
            reason="Closed";return true;
        }
        static bool Crosses(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {float Cross(Vector2 p,Vector2 q)=>p.x*q.y-p.y*q.x;return Cross(b-a,c-a)*Cross(b-a,d-a)<-1e-8&&Cross(d-c,a-c)*Cross(d-c,b-c)<-1e-8;}
        public static bool Contains(IReadOnlyList<Vector2> p,Vector2 q){bool inside=false;for(int i=0,j=p.Count-1;i<p.Count;j=i++)if((p[i].y>q.y)!=(p[j].y>q.y)&&q.x<(p[j].x-p[i].x)*(q.y-p[i].y)/(p[j].y-p[i].y)+p[i].x)inside=!inside;return inside;}
        public static bool ContoursCross(IReadOnlyList<Vector2> a,IReadOnlyList<Vector2> b){for(int i=1;i<a.Count;i++)for(int j=1;j<b.Count;j++)if(Crosses(a[i-1],a[i],b[j-1],b[j]))return true;return false;}
        public static bool NestedSealsValid(Construction c,out string reason)
        {
            reason="Nested regions ready";
            foreach(var inner in c.innerSeals)
            {
                if(!inner.closed){reason="Inner seal #"+inner.id+" is open. Finish its boundary, or undo/remove that inner seal.";return false;}
                if(!ClosedContour(inner.points,Array.Empty<GlyphNode>(),.005f,out reason)){reason="Inner seal #"+inner.id+": "+reason;return false;}
                if(inner.points.Any(p=>!Contains(c.seal,p))||ContoursCross(c.seal,inner.points)){reason="The outer seal must enclose every inner seal without touching/crossing it.";return false;}
                if(!c.nodes.Any(n=>Contains(inner.points,n.position))){reason="Each inner seal must enclose at least one instruction socket.";return false;}
                foreach(var other in c.innerSeals)if(inner!=other&&ContoursCross(inner.points,other.points)){reason="Inner seals may nest or sit beside each other; intersecting boundaries are ambiguous.";return false;}
            }
            return true;
        }
    }
}
