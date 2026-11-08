using UnityEngine;
namespace Arcana.Elemental
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GlyphIcon : UnityEngine.UI.Graphic
    {
        public Rune rune;
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();if(rune==Rune.Unknown)return;float scale=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.38f;
            foreach(var path in GlyphAlphabet.Get(rune).strokes)for(int i=1;i<path.Length;i++)
            {
                var a=rectTransform.rect.center+path[i-1]*scale;var b=rectTransform.rect.center+path[i]*scale;var d=b-a;if(d.sqrMagnitude<1e-9)continue;var n=new Vector2(-d.y,d.x).normalized;int index=mesh.currentVertCount;
                mesh.AddVert(a-n,color,Vector2.zero);mesh.AddVert(a+n,color,Vector2.zero);mesh.AddVert(b+n,color,Vector2.zero);mesh.AddVert(b-n,color,Vector2.zero);mesh.AddTriangle(index,index+1,index+2);mesh.AddTriangle(index,index+2,index+3);
            }
        }
    }
}
