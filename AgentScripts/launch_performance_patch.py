from pathlib import Path
p=Path('Unity/ARCANA/Assets/ARCANA/Runtime/Elemental/FluidSurface.cs');s=p.read_text();a=s.index('        public ElementalWorld world;');b=s.index('        void Reconstruct(',a);s=s[:a]+'''        public ElementalWorld world;float nextUpdate;
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
'''+s[b:];s=s.replace('mesh.Clear();mesh.indexFormat=', 'var inverseFrame=Quaternion.Inverse(meshRotation);for(int i=0;i<vertices.Count;i++){vertices[i]=inverseFrame*(vertices[i]-meshCenter);normals[i]=inverseFrame*normals[i];}\n            mesh.Clear();mesh.indexFormat=');p.write_text(s)
p=Path('Unity/ARCANA/Assets/ARCANA/Runtime/Elemental/ElementalWorld.cs');s=p.read_text().replace('explicitPower,assemble,volume;', 'explicitPower,assemble,volume,ended;');p.write_text(s)
p=Path('Unity/ARCANA/Assets/ARCANA/Runtime/Elemental/MatterBody.cs');s=p.read_text().replace('public double AddHeat(double joules){enthalpy+=joules;Refresh();return joules;}', 'public double AddHeat(double joules){double before=enthalpy;enthalpy+=joules;if(enthalpy!=before)Refresh();return joules;}');s=s.replace('if(box!=null){box.enabled=shaped;box.size=Vector3.one;}sphere.enabled=!shaped;GetComponent<MeshFilter>().sharedMesh=shaped?cubeMesh:kind==MatterKind.Stone||kind==MatterKind.Soil?MaterialAppearance.Rock():sphereMesh;', 'if(box!=null){if(box.enabled!=shaped)box.enabled=shaped;if(box.size!=Vector3.one)box.size=Vector3.one;}if(sphere.enabled==shaped)sphere.enabled=!shaped;var filter=GetComponent<MeshFilter>();var wanted=shaped?cubeMesh:kind==MatterKind.Stone||kind==MatterKind.Soil?MaterialAppearance.Rock():sphereMesh;if(filter.sharedMesh!=wanted)filter.sharedMesh=wanted;');p.write_text(s)
