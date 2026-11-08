using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Arcana.Elemental
{
    public sealed class WorldGlyphInk : MonoBehaviour
    {
        public ElementalWorld world;readonly Dictionary<string,List<LineRenderer>> pages=new Dictionary<string,List<LineRenderer>>();
        void LateUpdate()
        {
            var visible=world.casts.TakeLast(24).ToArray();foreach(var old in pages.Keys.Where(id=>!visible.Any(c=>c.id==id)).ToArray()){foreach(var line in pages[old])if(line!=null)Destroy(line.gameObject);pages.Remove(old);}
            foreach(var cast in visible)
            {
                if(!pages.ContainsKey(cast.id))Build(cast);
                foreach(var line in pages[cast.id])if(line!=null)line.startColor=line.endColor=cast.active?Palette.Teal:Palette.Brass;
            }
        }
        void Build(CastState cast)
        {
            var c=cast.drawing;var surface=world.bodies.Find(b=>b.id==c.drawingSurfaceId);Transform parent=surface!=null?surface.transform:transform;
            Vector3 right=Vector3.Cross(c.up,c.forward).normalized,up=surface!=null?c.up:c.forward,origin=c.origin+(surface!=null?c.forward*.075f:Vector3.up*.025f);var lines=new List<LineRenderer>();pages[c.id]=lines;
            Vector3 Map(Vector2 p)=>origin+(right*p.x+up*p.y)*.75f;
            void Line(IEnumerable<Vector2> points)
            {
                var positions=points.Select(Map).Select(p=>parent.InverseTransformPoint(p)).ToArray();if(positions.Length<2)return;
                var go=new GameObject("Committed glyph ink");go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=world.trailMaterial;line.startWidth=line.endWidth=.009f;line.positionCount=positions.Length;line.SetPositions(positions);line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;go.layer=2;lines.Add(line);
            }
            Line(c.seal);foreach(var edge in c.edges){var a=c.nodes.Find(n=>n.id==edge.from);var b=c.nodes.Find(n=>n.id==edge.to);if(a!=null&&b!=null)Line(new[]{a.position,b.position});}
            foreach(var node in c.nodes.Where(n=>n.rune!=Rune.Unknown))foreach(var stroke in GlyphAlphabet.Get(node.rune).strokes)Line(stroke.Select(p=>node.position+p*.075f));
        }
    }
}
