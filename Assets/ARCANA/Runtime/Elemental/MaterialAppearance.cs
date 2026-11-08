using System.Collections.Generic;
using UnityEngine;

namespace Arcana.Elemental
{
    public static class MaterialAppearance
    {
        static Mesh rock;static Material water;
        public static Material Water(Material fallback)
        {
            if(water==null){water=new Material(fallback){name="Physical water surface"};water.SetColor("_BaseColor",new Color(.075f,.39f,.57f));water.SetFloat("_Smoothness",.94f);water.SetFloat("_Metallic",.32f);}
            return water;
        }
        public static Mesh Rock()
        {
            if(rock!=null)return rock;var vertices=new List<Vector3>();var triangles=new List<int>();
            var ring=new[]{Vector3.forward,Vector3.right,Vector3.back,Vector3.left};
            void Face(Vector3 a,Vector3 b,Vector3 c,int depth)
            {
                if(depth>0){var ab=(a+b).normalized;var bc=(b+c).normalized;var ca=(c+a).normalized;Face(a,ab,ca,depth-1);Face(ab,b,bc,depth-1);Face(ca,bc,c,depth-1);Face(ab,bc,ca,depth-1);return;}
                foreach(var p in new[]{a,b,c}){float d=.94f+.06f*Mathf.Sin(Vector3.Dot(p,new Vector3(13.1f,27.3f,39.7f)));triangles.Add(vertices.Count);vertices.Add(p*.5f*d);}
            }
            for(int i=0;i<4;i++){Face(Vector3.up,ring[i],ring[(i+1)%4],1);Face(Vector3.down,ring[(i+1)%4],ring[i],1);}
            rock=new Mesh{name="Faceted physical stone"};rock.SetVertices(vertices);rock.SetTriangles(triangles,0);rock.RecalculateNormals();rock.RecalculateBounds();return rock;
        }
    }
}
