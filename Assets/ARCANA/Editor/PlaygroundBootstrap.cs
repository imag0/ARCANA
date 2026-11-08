using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using Arcana.Elemental;

namespace Arcana.Editor
{
    public static class PlaygroundBootstrap
    {
        public const string ScenePath="Assets/ARCANA/Scenes/ElementalPlayground.unity";
        static Material stone,earth,wood,water,ice,brass,ink,teal,red;static TMP_FontAsset font;static Transform root;
        [MenuItem("ARCANA/Bootstrap elemental playground")]
        public static void Ensure()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play mode before expanding the saved scene");
            if(!Application.unityVersion.StartsWith("6000.6."))throw new InvalidOperationException("Unity 6.6 required");
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null){EditorSceneManager.OpenScene(ScenePath);return;}
            Build();
        }
        static Material Mat(string name,Color color,float metal=0)
        {
            string path="Assets/ARCANA/Generated/Elemental "+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat!=null)return mat;
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",name=="Water"?.8f:.25f);mat.SetFloat("_Metallic",metal);AssetDatabase.CreateAsset(mat,path);return mat;
        }
        static GameObject Shape(string name,Vector3 point,Vector3 size,Material material,PrimitiveType shape=PrimitiveType.Cube,bool collider=true,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent==null?root:parent,false);go.transform.position=point;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;if(!collider){UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.layer=2;}return go;
        }
        static void Label(string title,Vector3 point,float size=2,float yaw=0)
        {
            var go=new GameObject("Wayfinding "+title);go.transform.SetParent(root,false);go.transform.position=point;go.transform.rotation=Quaternion.Euler(0,yaw,0);var text=go.AddComponent<TextMeshPro>();text.font=font;text.text=title;text.fontSize=size;text.color=Palette.Ink;text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=new Vector2(12,3);text.textWrappingMode=TextWrappingModes.Normal;
        }
        static void Station(string label,Vector3 point,string area,string example="",bool reset=false)
        {
            var stand=Shape(label,point+Vector3.up*.55f,new Vector3(1,.9f,.7f),reset?brass:teal);var fixture=stand.AddComponent<PlaygroundFixture>();fixture.area=area;fixture.example=example;fixture.reset=reset;fixture.help=label;Label("E · "+label,point+Vector3.up*1.35f,1.3f);
        }
        static MaterialSource Reservoir(string id,string label,MatterKind kind,Vector3 point,double kg,string area,bool collector=false)
        {
            var sourceRoot=new GameObject(label);sourceRoot.transform.SetParent(root,false);sourceRoot.transform.position=point;var source=sourceRoot.AddComponent<MaterialSource>();source.stableId=id;source.label=label;source.kind=kind;source.initialKg=kg;source.area=area;source.collector=collector;
            Material mat=kind==MatterKind.Water?water:kind==MatterKind.Soil?earth:stone;
            Shape(label+" floor",point+new Vector3(0,-.05f,0),new Vector3(3,.2f,3),stone,parent:sourceRoot.transform);
            foreach(float x in new[]{-1.55f,1.55f})Shape(label+" rim",point+new Vector3(x,.45f,0),new Vector3(.15f,1,3.2f),stone,parent:sourceRoot.transform);
            foreach(float z in new[]{-1.55f,1.55f})Shape(label+" rim",point+new Vector3(0,.45f,z),new Vector3(3.2f,1,.15f),stone,parent:sourceRoot.transform);
            var fill=Shape(label+" contents",point+new Vector3(0,.3f,0),new Vector3(2.9f,.35f,2.9f),mat,collider:false,parent:sourceRoot.transform);source.fill=fill.transform;
            source.outlet=new GameObject("Finite source outlet").transform;source.outlet.SetParent(sourceRoot.transform,false);source.outlet.localPosition=new Vector3(0,1.1f,0);
            if(collector){var trigger=sourceRoot.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=Vector3.up*.65f;trigger.size=new Vector3(3,1.2f,3);}
            Label(label+"\n"+kg+" kg · finite source",point+new Vector3(0,1.8f,.2f),1.6f);return source;
        }
        static MatterBody Prop(string id,Vector3 point,Vector3 size,float mass,MatterKind kind,Material material)
        {
            var go=Shape(id,point,size,material);go.AddComponent<Rigidbody>();var body=go.AddComponent<MatterBody>();body.id=id;body.origin="fixture:"+id;body.kind=kind;body.kg=mass;body.enthalpy=mass*800*20;body.radius=Mathf.Max(size.x,size.z)*.5f;if(kind==MatterKind.Fuel)body.fuelKg=mass*.7;return body;
        }
        static void Build()
        {
            LabBootstrap.Ensure();var scene=EditorSceneManager.GetActiveScene();
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            stone=Mat("Limestone",Palette.Stone);earth=Mat("Earth",new Color(.37f,.42f,.29f));wood=Mat("Timber",Palette.Wood);water=Mat("Water",new Color(.12f,.55f,.8f));ice=Mat("Ice",new Color(.65f,.85f,1));brass=Mat("Brass",Palette.Brass,.4f);ink=Mat("Ink",Palette.Ink);teal=Mat("Teal",Palette.Teal);red=Mat("Insulator",Palette.Red);
            var input=UnityEngine.Object.FindAnyObjectByType<LabInput>();var motor=UnityEngine.Object.FindAnyObjectByType<FirstPersonMotor>();var oldHud=UnityEngine.Object.FindAnyObjectByType<LabHud>();var tool=UnityEngine.Object.FindAnyObjectByType<DrawingTool>();
            var annex=new GameObject("Preserved mechanical laboratory annex");var original=scene.GetRootGameObjects();
            foreach(var go in original)
            {
                if(go==annex||go.GetComponent<Canvas>()!=null||go.GetComponent<UnityEngine.EventSystems.EventSystem>()!=null||go.GetComponent<Light>()!=null||go.GetComponent<Volume>()!=null||go.GetComponent<FirstPersonMotor>()!=null||go.GetComponent<ExperimentState>()!=null)continue;
                go.transform.SetParent(annex.transform,true);
            }
            annex.transform.position=new Vector3(-40,0,-42);
            var parapet=annex.transform.Find("South parapet");if(parapet!=null)UnityEngine.Object.DestroyImmediate(parapet.gameObject);
            root=new GameObject("Elemental playground / 120 metres").transform;
            for(int xi=0;xi<6;xi++)for(int zi=0;zi<6;zi++)
            {float x=-50+xi*20,z=-50+zi*20;if(x==30&&z==10)continue;Shape("Ground terrace",new Vector3(x,-.3f,z),new Vector3(20,.6f,20),earth);}
            // Real open freezing gap: no invisible floor under a supposed ice bridge.
            Shape("Gap west bank",new Vector3(23.5f,-.3f,10),new Vector3(7,.6f,20),stone);Shape("Gap east bank",new Vector3(36.5f,-.3f,10),new Vector3(7,.6f,20),stone);
            Shape("Gap north ground",new Vector3(30,-.3f,16),new Vector3(6,.6f,8),earth);Shape("Gap south ground",new Vector3(30,-.3f,4),new Vector3(6,.6f,8),earth);Shape("Gap catch floor",new Vector3(30,-3.3f,10),new Vector3(6,.5f,4),stone);
            var ramp=Shape("Recovery ramp",new Vector3(32,-1.5f,13),new Vector3(2,.3f,7),wood);ramp.transform.rotation=Quaternion.Euler(-27,0,0);
            foreach(float x in new[]{-60f,60f})Shape("Boundary stone wall",new Vector3(x,1.2f,0),new Vector3(.7f,2.4f,120),stone);
            foreach(float z in new[]{-60f,60f})Shape("Boundary stone wall",new Vector3(0,1.2f,z),new Vector3(120,2.4f,.7f),stone);
            Shape("Courtyard paving",new Vector3(-20,.02f,-13),new Vector3(22,.05f,18),stone);
            Reservoir("courtyard-water","COURTYARD WATER",MatterKind.Water,new Vector3(-27,.1f,-8),250,"court");Reservoir("courtyard-stone","LOOSE STONE",MatterKind.Stone,new Vector3(-13,.1f,-8),250,"court");Reservoir("soil","SAND / SOIL",MatterKind.Soil,new Vector3(-13,.1f,-16),150,"court");
            Station("DRAW A WATER ROUTE",new Vector3(-23,0,-5),"court","Water route");Station("RESET COURTYARD",new Vector3(-17,0,-5),"court",reset:true);
            Label("THE DRAWING COURTYARD",new Vector3(-20,4,-3),3);Label("G · GLYPH NOTEBOOK\nElements + operators + your geometry\nClose the outer seal to cast",new Vector3(-20,2,-21),2);
            for(int i=0;i<3;i++){var board=Shape("Portable rigid drawing board",new Vector3(-29+i*4,1.4f,-20),new Vector3(2.4f,1.6f,.12f),wood);var fixture=board.AddComponent<PlaygroundFixture>();fixture.portable=true;fixture.help="Portable drawing board · G to write";}
            // 50 metre trajectory lane, obstacles and physically movable impact targets.
            Shape("Trajectory lane",new Vector3(0,.025f,0),new Vector3(8,.05f,54),stone);Label("50 m TRAJECTORY LANE",new Vector3(0,3,-29),2.7f);
            for(int i=0;i<=5;i++){Shape("Distance stripe",new Vector3(0,.06f,-25+i*10),new Vector3(8,.015f,.1f),brass,PrimitiveType.Cube,false);Label((i*10)+" m",new Vector3(5,1,-25+i*10),1.5f);}
            Shape("Collision wall",new Vector3(0,1.8f,5),new Vector3(3.5f,3.6f,.5f),red);for(int i=0;i<4;i++)Prop("Impact block "+i,new Vector3(-2+i*1.3f,.55f,23),new Vector3(.7f,1,.4f),2+i*4,MatterKind.Stone,wood);
            var hanging=Prop("Hanging target",new Vector3(2,2,-8),Vector3.one,3,MatterKind.Stone,brass);var hinge=hanging.gameObject.AddComponent<HingeJoint>();hinge.anchor=new Vector3(0,2,0);hinge.axis=Vector3.forward;hinge.autoConfigureConnectedAnchor=false;hinge.connectedAnchor=new Vector3(2,4,-8);Shape("Target gantry",new Vector3(2,4.1f,-8),new Vector3(5,.2f,.3f),wood);
            var rotor=Prop("Wind rotor",new Vector3(-4,2.2f,-12),new Vector3(.4f,.4f,.4f),4,MatterKind.Stone,brass);rotor.radius=1.5f;var joint=rotor.gameObject.AddComponent<HingeJoint>();joint.axis=Vector3.forward;joint.autoConfigureConnectedAnchor=false;joint.connectedAnchor=rotor.transform.position;
            for(int i=0;i<4;i++){var blade=Shape("Wind blade",rotor.transform.position,new Vector3(.2f,2.6f,.1f),teal,PrimitiveType.Cube,false,rotor.transform);blade.transform.localPosition=Vector3.zero;blade.transform.localRotation=Quaternion.Euler(0,0,i*45);}
            Shape("Spin circle",new Vector3(-25,.03f,22),new Vector3(16,.03f,16),stone,PrimitiveType.Cylinder);Reservoir("spin-stone","RING FRAGMENTS",MatterKind.Stone,new Vector3(-35,.1f,20),200,"spin");Station("STONE ORBIT",new Vector3(-25,0,12),"spin","Stone orbit");Station("RESET SPIN AREA",new Vector3(-20,0,12),"spin",reset:true);Label("SPIN / DEFLECTION",new Vector3(-25,3,32),2.7f);
            Reservoir("bridge-water","BRIDGE WATER",MatterKind.Water,new Vector3(22,.1f,8),1000,"bridge");Station("FORM AN ICE BRIDGE",new Vector3(24,0,4),"bridge","Ice bridge");Station("RESET FREEZING GAP",new Vector3(36,0,4),"bridge",reset:true);Label("FREEZING GAP / 6 m\nWater + FORM + Frost + HOLD",new Vector3(30,3,15),2);
            Reservoir("thermal-water","HEAT EXPERIMENT WATER",MatterKind.Water,new Vector3(12,.1f,-18),150,"heat");for(int i=0;i<3;i++)Prop("Fuel log "+i,new Vector3(18+i,.35f,-17),new Vector3(.7f,.7f,1),2,MatterKind.Fuel,wood);Prop("Thermal stone",new Vector3(18,1,-21),new Vector3(1.5f,2,1),80,MatterKind.Stone,stone);Station("FLAME VORTEX",new Vector3(18,0,-26),"heat","Flame vortex");Station("RESET HEAT AREA",new Vector3(23,0,-26),"heat",reset:true);Label("FIRE / WATER / PHASE",new Vector3(18,3,-14),2.5f);
            // Conductive detour: direct route meets an insulating wall; water can bridge gaps around it.
            for(int i=0;i<7;i++)
            {var post=Shape("Conductive post "+i,new Vector3(31+i*1.5f,1,-35),new Vector3(.2f,2,.2f),brass);var node=post.AddComponent<ConductivePost>();node.stableId="post-"+i;node.receiver=i==6;node.requiredJ=2000;if(node.receiver){var light=new GameObject("Receiver indicator").AddComponent<Light>();light.transform.SetParent(post.transform,false);light.transform.localPosition=Vector3.up;light.type=LightType.Point;light.color=Palette.Teal;light.range=4;light.enabled=false;node.indicator=light;}}
            var insulator=Shape("Insulated barrier",new Vector3(35.5f,1.5f,-35),new Vector3(.3f,3,1.5f),red).AddComponent<ConductivePost>();insulator.stableId="insulator";insulator.insulated=true;
            Reservoir("conductive-water","CONDUCTIVE WATER",MatterKind.Water,new Vector3(35,.1f,-40),150,"electric");Label("LIGHTNING / CONDUCTIVE ROUTES\n2 m maximum air gap · insulation blocks",new Vector3(36,3.5f,-32),2.3f);Station("RESET CONDUCTIVITY",new Vector3(43,0,-39),"electric",reset:true);
            Shape("Alcove back",new Vector3(0,2.5f,46),new Vector3(16,5,.5f),stone);Shape("Alcove roof",new Vector3(0,5,41),new Vector3(16,.4f,10),wood);Shape("Alcove west",new Vector3(-8,2.5f,41),new Vector3(.4f,5,10),stone);Shape("Alcove east",new Vector3(8,2.5f,41),new Vector3(.4f,5,10),stone);
            Prop("Light occluder",new Vector3(0,1.5f,40),new Vector3(3,3,.3f),30,MatterKind.Stone,wood);Reservoir("optics-water","LIGHT / MIST WATER",MatterKind.Water,new Vector3(-5,.1f,39),150,"light");Label("LIGHT ALCOVE / OCCLUSION",new Vector3(0,4,35.8f),2.4f);Station("RESET LIGHT AREA",new Vector3(9,0,36),"light",reset:true);
            Reservoir("collection","COLLECTION BASIN",MatterKind.Water,new Vector3(0,.1f,-28),0,"lane",true);
            // Elevation and traversal, kept clear of active source outlets.
            Shape("Observation terrace",new Vector3(47,1.5f,30),new Vector3(18,3,18),stone);var upperRamp=Shape("Terrace ramp",new Vector3(47,1.4f,15),new Vector3(4,.3f,16),wood);upperRamp.transform.rotation=Quaternion.Euler(-11,0,0);Label("OBSERVATION TERRACE",new Vector3(47,5,35),2);
            for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Shape("Garden pillar",new Vector3(Mathf.Cos(a)*54,1.8f,Mathf.Sin(a)*54),new Vector3(1,3.6f,1),stone);}
            Label("MECHANICAL LAB ANNEX\nM switches back to the original instruments",new Vector3(-40,3,-49),2.5f);
            var goSystems=new GameObject("Elemental authoritative simulation");var world=goSystems.AddComponent<ElementalWorld>();world.input=input;world.motor=motor;world.bodyMaterial=water;world.trailMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ARCANA/Generated/Stroke unlit.mat");world.annexRoot=annex.transform;
            var notebook=goSystems.AddComponent<GlyphNotebook>();notebook.world=world;notebook.input=input;notebook.motor=motor;notebook.font=font;notebook.laboratoryCanvas=oldHud.gameObject;notebook.laboratoryHud=oldHud;notebook.laboratoryTool=tool;
            motor.spawn=new Vector3(-20,.15f,-14);motor.transform.position=motor.spawn;motor.view.farClipPlane=180;
            RenderSettings.fog=true;RenderSettings.fogColor=Palette.Sky;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=75;RenderSettings.fogEndDistance=165;
            Time.fixedDeltaTime=ElementalWorld.PhysicsStep;
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene(LabBootstrap.ScenePath,true)};
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Debug.Log("Elemental playground saved; mechanical source scene preserved.");
        }
    }
}
