using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arcana.Elemental
{
    // Surface reconstruction reads authoritative liquid cells. The mesh has no
    // hidden force or collision logic; all external collisions use those cells.
    public sealed class FluidSurface : MonoBehaviour
    {
        public ElementalWorld world;
        public double lastBuildMs,lastUpdateMs;public long rebuildCount,cachedFrames;
        sealed class Surface
        {
            public Mesh mesh;public MeshRenderer renderer;public bool built;public float nextBuild;
            public readonly List<MatterBody> cells=new List<MatterBody>();
            public readonly List<string> ids=new List<string>();public readonly List<Vector3> local=new List<Vector3>();public readonly List<Quaternion> rotations=new List<Quaternion>();
        }
        readonly Dictionary<string,Surface> surfaces=new Dictionary<string,Surface>();readonly Queue<Surface> pool=new Queue<Surface>();
        readonly Dictionary<string,MatterBody> bodyLookup=new Dictionary<string,MatterBody>();readonly HashSet<string> liveKeys=new HashSet<string>();readonly List<string> expired=new List<string>();
        readonly List<Renderer> hidden=new List<Renderer>();
        const int N=22;readonly float[] field=new float[N*N*N];readonly Vector3[] points=new Vector3[N*N*N],gradient=new Vector3[N*N*N];
        readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();readonly List<int> triangles=new List<int>();
        static readonly int[,] tetra={{0,5,1,6},{0,1,2,6},{0,2,3,6},{0,3,7,6},{0,7,4,6},{0,4,5,6}};
        readonly int[] cube=new int[8],inside=new int[4],outside=new int[4];
        readonly float[] kernelX=new float[192*N],kernelY=new float[192*N],kernelZ=new float[192*N],qX=new float[192*N],qY=new float[192*N],qZ=new float[192*N];
        Vector3 meshCenter;Quaternion meshRotation;
        int Index(int x,int y,int z)=>x+N*(y+N*z);
        void LateUpdate()
        {
            if(world==null)return;var watch=System.Diagnostics.Stopwatch.StartNew();
            foreach(var r in hidden)if(r!=null)r.enabled=true;hidden.Clear();foreach(var s in surfaces.Values)s.renderer.enabled=false;
            bodyLookup.Clear();foreach(var body in world.bodies)bodyLookup[body.id]=body;liveKeys.Clear();
            foreach(var cast in world.casts)
            {
                if(!cast.active)continue;
                for(int channel=0;channel<cast.channels.Count;channel++)
                {
                    var ch=cast.channels[channel];if(!ch.volume||ch.ended||ch.element!=Rune.Water)continue;
                    string key=cast.id+":"+channel;liveKeys.Add(key);
                    if(!surfaces.TryGetValue(key,out var surface))
                    {
                        if(pool.Count>0){surface=pool.Dequeue();surface.built=false;surface.nextBuild=0;}else
                        {
                            var go=new GameObject("Reconstructed liquid volume");go.transform.SetParent(transform,false);var filter=go.AddComponent<MeshFilter>();surface=new Surface{mesh=new Mesh{name="State-derived fluid isosurface"},renderer=go.AddComponent<MeshRenderer>()};surface.mesh.MarkDynamic();filter.sharedMesh=surface.mesh;surface.renderer.sharedMaterial=MaterialAppearance.Water(world.bodyMaterial);
                        }
                        surfaces.Add(key,surface);
                    }
                    surface.cells.Clear();foreach(var g in world.guided)if(g.castId==cast.id&&g.channel==channel&&bodyLookup.TryGetValue(g.bodyId,out var body)&&body.phase==MatterPhase.Liquid)surface.cells.Add(body);
                    var cells=surface.cells;if(cells.Count<4)continue;
                    var center=Vector3.zero;var bounds=new Bounds(cells[0].transform.position,Vector3.zero);float size=0;
                    foreach(var cell in cells){center+=cell.transform.position;bounds.Encapsulate(cell.transform.position);size=Mathf.Max(size,cell.sheetSize.magnitude);}center/=cells.Count;bounds.Expand(size*1.5f);
                    if(bounds.size.maxComponent()>16||bounds.size.minComponent()<.01f)continue;
                    var rotation=cells[0].transform.rotation;var inverse=Quaternion.Inverse(rotation);bool coherent=surface.built&&surface.ids.Count==cells.Count;
                    float tolerance=Mathf.Max(.006f,cells[0].sheetSize.minComponent()*.06f);
                    if(coherent)for(int i=0;i<cells.Count;i++)if(surface.ids[i]!=cells[i].id||Vector3.Distance(inverse*(cells[i].transform.position-center),surface.local[i])>tolerance||Quaternion.Angle(inverse*cells[i].transform.rotation,surface.rotations[i])>6){coherent=false;break;}
                    if(!coherent&&Time.unscaledTime>=surface.nextBuild)
                    {
                        surface.nextBuild=Time.unscaledTime+.15f;meshCenter=center;meshRotation=rotation;var buildWatch=System.Diagnostics.Stopwatch.StartNew();Reconstruct(cells.ToArray(),bounds,surface.mesh);lastBuildMs=buildWatch.Elapsed.TotalMilliseconds;rebuildCount++;
                        surface.ids.Clear();surface.local.Clear();surface.rotations.Clear();foreach(var cell in cells){surface.ids.Add(cell.id);surface.local.Add(inverse*(cell.transform.position-center));surface.rotations.Add(inverse*cell.transform.rotation);}surface.built=surface.mesh.vertexCount>0;
                    }
                    else if(coherent)cachedFrames++;
                    // The cached surface follows the actual rigid motion every rendered
                    // frame. Deformation/phase/membership changes invalidate the cache.
                    surface.renderer.transform.SetPositionAndRotation(center,rotation);surface.renderer.enabled=surface.built;
                    if(surface.built)foreach(var cell in cells){var r=cell.GetComponent<Renderer>();r.enabled=false;hidden.Add(r);}
                }
            }
            expired.Clear();foreach(var key in surfaces.Keys)if(!liveKeys.Contains(key))expired.Add(key);foreach(var key in expired){pool.Enqueue(surfaces[key]);surfaces.Remove(key);}
            lastUpdateMs=watch.Elapsed.TotalMilliseconds;
        }
        void Reconstruct(MatterBody[] cells,Bounds bounds,Mesh mesh)
        {
            var centers=cells.Select(c=>c.transform.position).ToArray();var rotations=cells.Select(c=>Quaternion.Inverse(c.transform.rotation)).ToArray();var half=cells.Select(c=>c.sheetSize*.5f).ToArray();
            var step=bounds.size/(N-1);bool isotropic=half.All(h=>Mathf.Abs(h.x-h.y)<1e-6f&&Mathf.Abs(h.x-h.z)<1e-6f);
            if(isotropic)for(int i=0;i<cells.Length;i++)for(int t=0;t<N;t++)
            {
                int k=i*N+t;float scale=1/(half[i].x*half[i].x);var d=bounds.min+step*t-centers[i];qX[k]=d.x*d.x*scale;qY[k]=d.y*d.y*scale;qZ[k]=d.z*d.z*scale;kernelX[k]=Mathf.Exp(-qX[k]*.35f);kernelY[k]=Mathf.Exp(-qY[k]*.35f);kernelZ[k]=Mathf.Exp(-qZ[k]*.35f);
            }
            for(int z=0;z<N;z++)for(int y=0;y<N;y++)for(int x=0;x<N;x++)
            {
                int index=Index(x,y,z);var p=bounds.min+Vector3.Scale(new Vector3(x,y,z),step);points[index]=p;float density=0;
                for(int i=0;i<cells.Length;i++)
                {
                    if(isotropic){int k=i*N;if(qX[k+x]+qY[k+y]+qZ[k+z]<25)density+=kernelX[k+x]*kernelY[k+y]*kernelZ[k+z];}
                    else {var local=rotations[i]*(p-centers[i]);var h=half[i];float q=local.x*local.x/(h.x*h.x)+local.y*local.y/(h.y*h.y)+local.z*local.z/(h.z*h.z);if(q<25)density+=Mathf.Exp(-q*.35f);}
                }
                field[index]=1.5f-density;
            }
            for(int z=0;z<N;z++)for(int y=0;y<N;y++)for(int x=0;x<N;x++)gradient[Index(x,y,z)]=new Vector3((field[Index(Mathf.Min(N-1,x+1),y,z)]-field[Index(Mathf.Max(0,x-1),y,z)])/step.x,(field[Index(x,Mathf.Min(N-1,y+1),z)]-field[Index(x,Mathf.Max(0,y-1),z)])/step.y,(field[Index(x,y,Mathf.Min(N-1,z+1))]-field[Index(x,y,Mathf.Max(0,z-1))])/step.z).normalized;
            vertices.Clear();normals.Clear();triangles.Clear();
            for(int z=0;z<N-1;z++)for(int y=0;y<N-1;y++)for(int x=0;x<N-1;x++)
            {
                cube[0]=Index(x,y,z);cube[1]=Index(x+1,y,z);cube[2]=Index(x+1,y+1,z);cube[3]=Index(x,y+1,z);cube[4]=Index(x,y,z+1);cube[5]=Index(x+1,y,z+1);cube[6]=Index(x+1,y+1,z+1);cube[7]=Index(x,y+1,z+1);
                for(int t=0;t<6;t++)
                {
                    int ni=0,no=0;for(int v=0;v<4;v++){int id=cube[tetra[t,v]];if(field[id]<0)inside[ni++]=id;else outside[no++]=id;}
                    if(ni==0||ni==4)continue;
                    if(ni==1)Triangle(Edge(inside[0],outside[0]),Edge(inside[0],outside[1]),Edge(inside[0],outside[2]));
                    else if(ni==3)Triangle(Edge(outside[0],inside[0]),Edge(outside[0],inside[1]),Edge(outside[0],inside[2]));
                    else {int a=Edge(inside[0],outside[0]),b=Edge(inside[0],outside[1]),c=Edge(inside[1],outside[1]),d=Edge(inside[1],outside[0]);Triangle(a,b,c);Triangle(a,c,d);}
                }
            }
            var inverseFrame=Quaternion.Inverse(meshRotation);for(int i=0;i<vertices.Count;i++){vertices[i]=inverseFrame*(vertices[i]-meshCenter);normals[i]=inverseFrame*normals[i];}
            mesh.Clear();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
        int Edge(int a,int b){float t=field[a]/(field[a]-field[b]);int i=vertices.Count;vertices.Add(Vector3.Lerp(points[a],points[b],t));normals.Add(Vector3.Lerp(gradient[a],gradient[b],t).normalized);return i;}
        void Triangle(int a,int b,int c){if(Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),normals[a]+normals[b]+normals[c])<0){int swap=b;b=c;c=swap;}triangles.Add(a);triangles.Add(b);triangles.Add(c);}
        void OnDestroy(){foreach(var r in hidden)if(r!=null)r.enabled=true;foreach(var s in surfaces.Values.Concat(pool))if(s.mesh!=null)Destroy(s.mesh);}
    }
    static class VectorExtent {public static float maxComponent(this Vector3 v)=>Mathf.Max(v.x,Mathf.Max(v.y,v.z));public static float minComponent(this Vector3 v)=>Mathf.Min(v.x,Mathf.Min(v.y,v.z));}
}
