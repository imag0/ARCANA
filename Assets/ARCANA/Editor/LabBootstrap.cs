using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

namespace Arcana.Editor
{
    public static class LabBootstrap
    {
        public const string Root="Assets/ARCANA",ScenePath=Root+"/Scenes/ArcanaLab.unity";
        static Material stone,floor,wood,ink,brass,paper,teal,red;static TMP_FontAsset font;
        [MenuItem("ARCANA/Bootstrap laboratory")]
        public static void Ensure()
        {
            if(!Application.unityVersion.StartsWith("6000.6."))throw new InvalidOperationException("ARCANA requires Unity 6000.6.x");
            Directory.CreateDirectory(Root+"/Scenes");Directory.CreateDirectory(Root+"/Generated");Directory.CreateDirectory(Root+"/Prefabs");AssetDatabase.Refresh();
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null){EditorSceneManager.OpenScene(ScenePath);Debug.Log("ARCANA bootstrap: existing lab preserved");return;}
            BuildScene();
        }
        public static void ImportFonts()
        {
            if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset")!=null)return;
            var package=UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TextMeshProUGUI).Assembly);
            AssetDatabase.ImportPackage(Path.Combine(package.resolvedPath,"Package Resources/TMP Essential Resources.unitypackage"),false);
        }
        static Material Mat(string name,Color color,bool unlit=false)
        {
            string path=Root+"/Generated/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;
            m=new Material(Shader.Find(name=="Stroke unlit"?"ARCANA/Stroke":(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"))){name=name};m.SetColor("_BaseColor",color);if(!unlit){m.SetFloat("_Smoothness",.22f);if(name=="Brass")m.SetFloat("_Metallic",.45f);}AssetDatabase.CreateAsset(m,path);return m;
        }
        static GameObject Box(string name,Vector3 position,Vector3 scale,Material material,Transform parent=null,bool collision=true,PrimitiveType shape=PrimitiveType.Cube)
        {
            var g=GameObject.CreatePrimitive(shape);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;if(!collision){UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.layer=2;}return g;
        }
        static TMP_Text Sign(string text,Vector3 position,float size=3,Transform parent=null,float width=3)
        {
            var g=new GameObject("Label "+text.Split('\n')[0]);g.transform.SetParent(parent,false);g.transform.localPosition=position;var t=g.AddComponent<TextMeshPro>();t.font=font;t.text=text;t.fontSize=size;t.color=Palette.Ink;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(width,1);t.textWrappingMode=TextWrappingModes.NoWrap;return t;
        }
        static DrawingSurface Panel(string id,string label,Vector3 position,Material material,double c,float yaw=0)
        {
            var root=new GameObject("Drawing panel / "+label);root.transform.position=position;root.transform.rotation=Quaternion.Euler(0,yaw,0);
            var s=root.AddComponent<DrawingSurface>();s.stableId=id;s.materialLabel=label;s.coefficient=c;s.density=50;
            Box("Rigid panel",new Vector3(0,0,.07f),new Vector3(2.8f,1.6f,.14f),material,root.transform);
            Box("Top frame",new Vector3(0,.85f,.08f),new Vector3(3,.10f,.18f),wood,root.transform);
            Box("Bottom frame",new Vector3(0,-.85f,.08f),new Vector3(3,.10f,.18f),wood,root.transform);
            foreach(float x in new[]{-1.45f,1.45f})Box("Side frame",new Vector3(x,0,.08f),new Vector3(.1f,1.8f,.18f),wood,root.transform);
            Sign(label+"\nC="+c+"  rho=50  kappa=0",new Vector3(0,1.06f,-.02f),1.4f,root.transform);
            return s;
        }
        static void Station(string name,Vector3 position,LabStation.Kind kind,Material mat)
        {
            var root=Box(name,position,new Vector3(.65f,.30f,.42f),mat);root.AddComponent<LabStation>().kind=kind;
            // Label is independent so its size is not distorted by the station's primitive scale.
            Sign("[E] "+name,position+new Vector3(0,.25f,-.25f),.85f,null,2.4f);
        }
        public static void BuildScene()
        {
            ImportFonts();font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");if(font==null)throw new InvalidOperationException("TMP essentials import pending; run bootstrap again after import");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            stone=Mat("Limestone",Palette.Stone);floor=Mat("Floor",Palette.Floor);wood=Mat("Timber",Palette.Wood);ink=Mat("Ink",Palette.Ink);brass=Mat("Brass",Palette.Brass);paper=Mat("Paper",Palette.Paper);teal=Mat("Instrument",Palette.Teal,true);red=Mat("Insulator",Palette.Red);
            Box("Foundation",new Vector3(0,-.25f,0),new Vector3(16,.5f,12),floor);
            for(int x=-7;x<=7;x+=2)for(int z=-5;z<=5;z+=2)Box("Limestone floor slab",new Vector3(x,.008f,z),new Vector3(1.97f,.02f,1.97f),((x+z)/2)%3==0?stone:floor,null,false);
            Box("North wall",new Vector3(0,1.8f,5.3f),new Vector3(16,3.6f,.35f),stone);
            Box("West wall",new Vector3(-8,1.35f,0),new Vector3(.35f,2.7f,12),stone);
            Box("East parapet",new Vector3(8,.65f,0),new Vector3(.35f,1.3f,12),stone);
            Box("South parapet",new Vector3(0,.65f,-6),new Vector3(16,1.3f,.35f),stone);
            foreach(float x in new[]{-7.6f,-2.6f,2.6f,7.6f}){Box("Oak post",new Vector3(x,2,5.02f),new Vector3(.25f,4,.25f),wood);Box("Brass footing",new Vector3(x,.22f,4.98f),new Vector3(.38f,.4f,.35f),brass);}
            Box("Roof beam",new Vector3(0,3.8f,4.9f),new Vector3(16,.26f,.32f),wood);
            for(int i=0;i<5;i++)Box("Steps",new Vector3(-6.5f,.1f*(i+1),-4.5f+i*.35f),new Vector3(1.7f,.2f*(i+1),.38f),stone);
            Box("Raised landing",new Vector3(-6.5f,.5f,-1.8f),new Vector3(1.8f,1,2),floor);
            Box("Crouch clearance test",new Vector3(5.6f,1.48f,-3.5f),new Vector3(2.6f,.20f,1.2f),wood);
            foreach(float x in new[]{4.4f,6.8f})Box("Low shelf leg",new Vector3(x,.7f,-3.5f),new Vector3(.12f,1.4f,1.2f),wood);
            Sign("LOW SHELF / CTRL TO CROUCH",new Vector3(5.6f,1.85f,-4.12f),2,null,4);
            foreach(float x in new[]{-4f,0f,4f})
            {
                Box("Workbench",new Vector3(x,.77f,3.65f),new Vector3(3.3f,.16f,1.3f),wood);
                foreach(float dx in new[]{-1.35f,1.35f})Box("Bench leg",new Vector3(x+dx,.36f,3.7f),new Vector3(.15f,.72f,.9f),wood);
            }
            var surfaces=new[]{Panel("stone","STONE / W BENCH",new Vector3(-4,1.8f,4.15f),stone,50),Panel("paper","PAPER / DRAW HERE",new Vector3(0,1.8f,4.15f),paper,50),Panel("wood","WOOD / FREE STUDY",new Vector3(4,1.8f,4.15f),wood,30),Panel("metal","METAL / CONTACT STUDY",new Vector3(7.3f,1.8f,1.2f),brass,80,-90)};
            Station("W BENCH",new Vector3(-4,.98f,3.24f),LabStation.Kind.WBenchmark,brass);
            Station("GUIDED POLYGON",new Vector3(0,.98f,3.24f),LabStation.Kind.Guided,teal);
            Station("CONTACT DEMO",new Vector3(4,.98f,3.24f),LabStation.Kind.Crossings,red);
            Box("Winding table",new Vector3(-3,.65f,-2),new Vector3(1.4f,1.3f,.8f),wood);
            Station("WIND / EXTERNAL WORK",new Vector3(-3,1.45f,-2),LabStation.Kind.Winder,brass);
            var wheel=Box("Mechanical winding wheel",new Vector3(-3,1.8f,-2),new Vector3(.48f,.06f,.48f),brass,null,false,PrimitiveType.Cylinder);wheel.transform.rotation=Quaternion.Euler(90,0,0);
            Sign("ARCANA",new Vector3(0,3.8f,4.62f),2.2f,null,9);
            // Explicit original geometry props: rolled parchment, instrument cases and storage jars.
            for(int i=0;i<4;i++)Box("Parchment roll",new Vector3(2.85f+i*.18f,.97f,3.8f),new Vector3(.1f,.25f,.1f),paper,null,false,PrimitiveType.Cylinder);
            Box("Instrument case",new Vector3(-5.15f,1.05f,3.8f),new Vector3(.4f,.35f,.4f),ink);
            var sun=new GameObject("Afternoon sun").AddComponent<Light>();sun.type=LightType.Directional;sun.color=Palette.Sun;sun.intensity=1.25f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(43,-32,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=Palette.Sky*.8f;RenderSettings.ambientEquatorColor=Palette.Stone*.6f;RenderSettings.ambientGroundColor=Palette.Wood*.5f;RenderSettings.skybox=null;RenderSettings.fog=false;
            var config=AssetDatabase.LoadAssetAtPath<LabConfig>(Root+"/Generated/LabConfig.asset");if(config==null){config=ScriptableObject.CreateInstance<LabConfig>();AssetDatabase.CreateAsset(config,Root+"/Generated/LabConfig.asset");}
            var systems=new GameObject("Experiment systems");var state=systems.AddComponent<ExperimentState>();state.config=config;state.surfaces=surfaces;
            var actions=MakeInput();var input=systems.AddComponent<LabInput>();input.actions=actions;
            var player=new GameObject("First person researcher");player.transform.position=new Vector3(0,.08f,-2.5f);player.layer=8;
            var capsule=player.AddComponent<CharacterController>();capsule.height=1.8f;capsule.radius=.27f;capsule.center=Vector3.up*.9f;capsule.stepOffset=.32f;capsule.slopeLimit=50;capsule.skinWidth=.025f;capsule.minMoveDistance=0;
            var pivot=new GameObject("Pitch pivot").transform;pivot.SetParent(player.transform);pivot.localPosition=Vector3.up*1.64f;
            var cameraGO=new GameObject("Main Camera");cameraGO.tag="MainCamera";cameraGO.transform.SetParent(pivot,false);var camera=cameraGO.AddComponent<Camera>();camera.nearClipPlane=.04f;camera.farClipPlane=70;camera.fieldOfView=75;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Palette.Sky;cameraGO.AddComponent<AudioListener>();var cameraData=cameraGO.AddComponent<UniversalAdditionalCameraData>();cameraData.renderPostProcessing=true;cameraData.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var motor=player.AddComponent<FirstPersonMotor>();motor.input=input;motor.view=camera;motor.cameraPivot=pivot;motor.spawn=player.transform.position;
            var probeRoot=new GameObject("Independent resonator mount");probeRoot.layer=2;
            var rod=Box("Physical probe L=.05 r=.002",Vector3.zero,new Vector3(.004f,.025f,.004f),teal,probeRoot.transform,false,PrimitiveType.Cylinder);rod.transform.localRotation=Quaternion.Euler(0,0,90);
            foreach(float x in new[]{-.03f,.03f})Box("Probe end clamp",new Vector3(x,0,0),new Vector3(.01f,.04f,.04f),brass,probeRoot.transform,false);
            Box("Independent suspension",new Vector3(0,-.10f,.035f),new Vector3(.02f,.22f,.02f),brass,probeRoot.transform,false);
            Box("Probe instrument body",new Vector3(0,-.24f,.035f),new Vector3(.23f,.10f,.09f),ink,probeRoot.transform,false);
            var probe=systems.AddComponent<ProbeInstrument>();probe.experiment=state;probe.mount=probeRoot.transform;probe.rod=rod.transform;
            var persistence=systems.AddComponent<ExperimentPersistence>();persistence.experiment=state;persistence.probe=probe;
            var tool=systems.AddComponent<DrawingTool>();tool.input=input;tool.view=camera;tool.experiment=state;tool.probe=probe;tool.persistence=persistence;
            var strokes=new GameObject("Finite width strokes").AddComponent<StrokeView>();strokes.experiment=state;strokes.tool=tool;strokes.strokeMaterial=Mat("Stroke unlit",Palette.Paper,true);strokes.supportMaterial=brass;
            var sound=systems.AddComponent<LabSound>();sound.experiment=state;sound.source=systems.AddComponent<AudioSource>();sound.source.playOnAwake=false;sound.latch=MakeLatch();
            MakeHud(input,motor,state,tool,probe,persistence,actions);
            ConfigureRender(camera);
            // Serialize reusable real prefabs and fully wired scene, generated by Unity itself.
            PrefabUtility.SaveAsPrefabAsset(player,Root+"/Prefabs/FirstPersonResearcher.prefab");PrefabUtility.SaveAsPrefabAsset(probeRoot,Root+"/Prefabs/IndependentResonator.prefab");
            PlayerSettings.companyName="ARCANA";PlayerSettings.productName="ARCANA Laboratory";PlayerSettings.defaultScreenWidth=1920;PlayerSettings.defaultScreenHeight=1080;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);var handling=settings.FindProperty("activeInputHandler");if(handling!=null){handling.intValue=1;settings.ApplyModifiedProperties();}
            QualitySettings.vSyncCount=0;Application.targetFrameRate=120;
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Debug.Log("ARCANA bootstrap complete: "+ScenePath);
        }
        static void ConfigureRender(Camera camera)
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;if(pipeline==null)throw new InvalidOperationException("Existing project must use URP");
            string asset=Root+"/Generated/ArcanaURP.asset";var local=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(asset);if(local==null){local=UnityEngine.Object.Instantiate(pipeline);AssetDatabase.CreateAsset(local,asset);}local.supportsHDR=true;local.shadowDistance=28;local.msaaSampleCount=2;GraphicsSettings.defaultRenderPipeline=local;QualitySettings.renderPipeline=local;
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root+"/Generated/WorkshopVolume.asset");if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Root+"/Generated/WorkshopVolume.asset");var tone=profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.Neutral;var bloom=profile.Add<Bloom>(true);bloom.intensity.value=.12f;bloom.threshold.value=1.1f;var color=profile.Add<ColorAdjustments>(true);color.saturation.value=5;color.contrast.value=4;var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.12f;foreach(var c in profile.components)AssetDatabase.AddObjectToAsset(c,profile);}
            NoDistortion<DepthOfField>(profile).mode.Override(DepthOfFieldMode.Off);
            NoDistortion<MotionBlur>(profile).intensity.Override(0);NoDistortion<ChromaticAberration>(profile).intensity.Override(0);NoDistortion<LensDistortion>(profile).intensity.Override(0);NoDistortion<FilmGrain>(profile).intensity.Override(0);NoDistortion<PaniniProjection>(profile).distance.Override(0);
            var volume=new GameObject("Workshop color and light").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
        }
        static T NoDistortion<T>(VolumeProfile p) where T:VolumeComponent {if(p.TryGet<T>(out var c))return c;c=p.Add<T>(true);AssetDatabase.AddObjectToAsset(c,p);return c;}
        static InputActionAsset MakeInput()
        {
            string path=Root+"/Generated/ArcanaControls.inputactions";
            var existing=AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);if(existing!=null)return existing;
            var asset=ScriptableObject.CreateInstance<InputActionAsset>();var g=asset.AddActionMap("Gameplay");
            g.AddAction("Move",InputActionType.Value).AddCompositeBinding("2DVector").With("Up","<Keyboard>/w").With("Down","<Keyboard>/s").With("Left","<Keyboard>/a").With("Right","<Keyboard>/d");
            g.AddAction("Look",InputActionType.Value,"<Mouse>/delta");g.AddAction("Scroll",InputActionType.Value,"<Mouse>/scroll");
            string[,] binds={{"Sprint","<Keyboard>/leftShift"},{"Crouch","<Keyboard>/leftCtrl"},{"Jump","<Keyboard>/space"},{"Fire","<Mouse>/leftButton"},{"Cancel","<Mouse>/rightButton"},{"Interact","<Keyboard>/e"},{"RotateLeft","<Keyboard>/q"},{"RotateRight","<Keyboard>/r"},{"Bond","<Keyboard>/b"},{"Join","<Keyboard>/j"},{"ReleaseSupports","<Keyboard>/u"},{"Delete","<Keyboard>/delete"},{"Save","<Keyboard>/f5"},{"Load","<Keyboard>/f9"},{"Controls","<Keyboard>/f1"},{"Diagnostics","<Keyboard>/tab"}};
            for(int i=0;i<binds.GetLength(0);i++)g.AddAction(binds[i,0],InputActionType.Button,binds[i,1]);
            for(int i=1;i<=4;i++)g.AddAction("Tool"+i,InputActionType.Button,"<Keyboard>/digit"+i);
            g.AddAction("Undo",InputActionType.Button).AddCompositeBinding("ButtonWithOneModifier").With("Modifier","<Keyboard>/ctrl").With("Button","<Keyboard>/z");g.AddAction("Redo",InputActionType.Button).AddCompositeBinding("ButtonWithOneModifier").With("Modifier","<Keyboard>/ctrl").With("Button","<Keyboard>/y");
            var ui=asset.AddActionMap("UI");ui.AddAction("Pause",InputActionType.Button,"<Keyboard>/escape");ui.AddAction("Point",InputActionType.PassThrough,"<Mouse>/position");ui.AddAction("Click",InputActionType.PassThrough,"<Mouse>/leftButton");ui.AddAction("ScrollWheel",InputActionType.PassThrough,"<Mouse>/scroll");ui.AddAction("Submit",InputActionType.Button,"<Keyboard>/enter");ui.AddAction("Cancel",InputActionType.Button,"<Keyboard>/escape");ui.AddAction("Navigate",InputActionType.Value).AddCompositeBinding("2DVector").With("Up","<Keyboard>/upArrow").With("Down","<Keyboard>/downArrow").With("Left","<Keyboard>/leftArrow").With("Right","<Keyboard>/rightArrow");
            File.WriteAllText(path,asset.ToJson());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);UnityEngine.Object.DestroyImmediate(asset);return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }
        static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=rt.pivot=anchor;rt.anchoredPosition=position;rt.sizeDelta=size;return rt;}
        static GameObject PanelUi(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,float alpha=.91f)
        {var r=Rect(name,parent,anchor,position,size);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();var color=Palette.Ink;color.a=alpha;image.color=color;return r.gameObject;}
        static TMP_Text TextUi(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,int pointSize,Color color,string text="")
        {var r=Rect(name,parent,anchor,position,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=pointSize;t.color=color;t.text=text;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;return t;}
        static UnityEngine.UI.Button Button(string label,Transform parent,float y)
        {var go=PanelUi(label,parent,new Vector2(.5f,1),new Vector2(0,y),new Vector2(420,50));go.GetComponent<UnityEngine.UI.Image>().color=Palette.Brass;var button=go.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=go.GetComponent<UnityEngine.UI.Image>();var t=TextUi(label+" text",go.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(410,44),22,Palette.Ink,label);t.alignment=TextAlignmentOptions.Center;return button;}
        static UnityEngine.UI.Slider Slider(string label,Transform parent,float y,float lo,float hi)
        {
            TextUi(label,parent,new Vector2(.5f,1),new Vector2(0,y+30),new Vector2(420,28),18,Palette.Paper,label);
            var rt=Rect(label+" slider",parent,new Vector2(.5f,1),new Vector2(0,y),new Vector2(420,20));var bg=rt.gameObject.AddComponent<UnityEngine.UI.Image>();bg.color=Palette.Wood;
            var fill=Rect("Fill",rt,new Vector2(0,.5f),Vector2.zero,new Vector2(0,-4));fill.gameObject.AddComponent<UnityEngine.UI.Image>().color=Palette.Teal;
            var handle=Rect("Handle",rt,new Vector2(0,.5f),Vector2.zero,new Vector2(18,8));var handleImage=handle.gameObject.AddComponent<UnityEngine.UI.Image>();handleImage.color=Palette.Brass;
            var slider=rt.gameObject.AddComponent<UnityEngine.UI.Slider>();slider.minValue=lo;slider.maxValue=hi;slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handleImage;slider.direction=UnityEngine.UI.Slider.Direction.LeftToRight;return slider;
        }
        static void MakeHud(LabInput input,FirstPersonMotor motor,ExperimentState state,DrawingTool tool,ProbeInstrument probe,ExperimentPersistence persistence,InputActionAsset actions)
        {
            var canvas=new GameObject("ARCANA interface",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var hud=canvas.AddComponent<LabHud>();hud.input=input;hud.motor=motor;hud.experiment=state;hud.tool=tool;hud.probe=probe;hud.persistence=persistence;
            var upper=PanelUi("Tool card",canvas.transform,new Vector2(0,1),new Vector2(28,-28),new Vector2(570,150));hud.title=TextUi("Title",upper.transform,new Vector2(0,1),new Vector2(20,-14),new Vector2(530,36),27,Palette.Brass);hud.status=TextUi("State",upper.transform,new Vector2(0,1),new Vector2(20,-52),new Vector2(530,91),21,Palette.Paper);
            var instrument=PanelUi("Instrument readout",canvas.transform,new Vector2(1,1),new Vector2(-28,-28),new Vector2(470,150));hud.instrument=TextUi("Measurement",instrument.transform,new Vector2(0,1),new Vector2(20,-15),new Vector2(430,120),24,Palette.Teal);
            var reticle=TextUi("Reticle",canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(22,28),24,Palette.Ink,"+");reticle.alignment=TextAlignmentOptions.Center;
            var bottom=PanelUi("Context card",canvas.transform,new Vector2(.5f,0),new Vector2(0,24),new Vector2(1280,125));hud.hint=TextUi("Hint",bottom.transform,new Vector2(0,1),new Vector2(20,-12),new Vector2(1240,30),22,Palette.Teal);hud.notice=TextUi("Notice",bottom.transform,new Vector2(0,1),new Vector2(20,-47),new Vector2(1240,48),21,Palette.Paper);
            TextUi("Bindings strip",bottom.transform,new Vector2(0,0),new Vector2(20,3),new Vector2(1240,24),16,Palette.Brass,"1 PEN   2 CUT   3 PRELOAD   4 PROBE    B CONTACT    E USE    F5/F9 SAVE/LOAD    F1 HELP    TAB DIAGNOSTICS    ESC PAUSE");
            TextUi("Scientific scope",canvas.transform,new Vector2(.5f,1),new Vector2(0,-18),new Vector2(600,24),15,Palette.Ink,"WEAK-STATIC RESEARCH PROTOTYPE  ·  S1 / D1 UNPASSED").alignment=TextAlignmentOptions.Center;
            hud.diagnosticsPanel=PanelUi("Diagnostics",canvas.transform,new Vector2(1,.5f),new Vector2(-28,0),new Vector2(690,785));hud.diagnostics=TextUi("Diagnostics text",hud.diagnosticsPanel.transform,new Vector2(0,1),new Vector2(20,-15),new Vector2(650,755),18,Palette.Paper);hud.diagnostics.enableAutoSizing=true;hud.diagnostics.fontSizeMin=14;hud.diagnostics.fontSizeMax=18;hud.diagnosticsPanel.SetActive(false);
            hud.pausePanel=PanelUi("Pause settings",canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(550,770),1f);TextUi("Welcome",hud.pausePanel.transform,new Vector2(.5f,1),new Vector2(0,-25),new Vector2(450,105),32,Palette.Brass,"ARCANA\nThe instrument workshop").alignment=TextAlignmentOptions.Center;
            TextUi("Welcome help",hud.pausePanel.transform,new Vector2(.5f,1),new Vector2(0,-130),new Vector2(450,60),18,Palette.Paper,"Walk to the paper board and draw. Close a polygon, then place a probe. F1 shows all controls.");
            hud.resume=Button("ENTER LAB / RESUME",hud.pausePanel.transform,-210);hud.save=Button("SAVE EXPERIMENT",hud.pausePanel.transform,-270);hud.load=Button("LOAD EXPERIMENT",hud.pausePanel.transform,-330);
            hud.sensitivity=Slider("Mouse sensitivity",hud.pausePanel.transform,-435,.03f,.4f);hud.fov=Slider("Field of view",hud.pausePanel.transform,-500,55,100);
            var toggleGo=PanelUi("Invert Y",hud.pausePanel.transform,new Vector2(.5f,1),new Vector2(-195,-540),new Vector2(28,28));var image=toggleGo.GetComponent<UnityEngine.UI.Image>();image.color=Palette.Brass;hud.invert=toggleGo.AddComponent<UnityEngine.UI.Toggle>();hud.invert.targetGraphic=image;var check=Rect("Check",toggleGo.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(16,16));var checkImage=check.gameObject.AddComponent<UnityEngine.UI.Image>();checkImage.color=Palette.Teal;hud.invert.graphic=checkImage;TextUi("Invert label",hud.pausePanel.transform,new Vector2(.5f,1),new Vector2(10,-540),new Vector2(360,32),20,Palette.Paper,"Invert mouse Y");
            hud.reset=Button("CLEAR EXPERIMENT / RESET POSITION",hud.pausePanel.transform,-610);hud.quit=Button("QUIT",hud.pausePanel.transform,-675);
            hud.controlsPanel=PanelUi("Controls panel",canvas.transform,new Vector2(0,.5f),new Vector2(25,0),new Vector2(600,920),1f);hud.controls=TextUi("Actual controls",hud.controlsPanel.transform,new Vector2(0,1),new Vector2(20,-20),new Vector2(560,830),19,Palette.Paper);var close=Button("CLOSE CONTROLS",hud.controlsPanel.transform,-850);UnityEditor.Events.UnityEventTools.AddPersistentListener(close.onClick,hud.CloseControls);hud.controls.enableAutoSizing=true;hud.controls.fontSizeMin=14;hud.controls.fontSizeMax=19;hud.controlsPanel.SetActive(false);
            var events=new GameObject("UI EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));var module=events.GetComponent<InputSystemUIInputModule>();module.actionsAsset=actions;
            module.point=InputActionReference.Create(actions.FindAction("UI/Point"));module.leftClick=InputActionReference.Create(actions.FindAction("UI/Click"));module.scrollWheel=InputActionReference.Create(actions.FindAction("UI/ScrollWheel"));module.move=InputActionReference.Create(actions.FindAction("UI/Navigate"));module.submit=InputActionReference.Create(actions.FindAction("UI/Submit"));module.cancel=InputActionReference.Create(actions.FindAction("UI/Cancel"));
        }
        static AudioClip MakeLatch()
        {
            string path=Root+"/Generated/MechanicalLatch.wav";if(!File.Exists(path))
            {
                const int rate=22050,count=4410;using(var writer=new BinaryWriter(File.Create(path))){writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);for(int i=0;i<count;i++){double t=i/(double)rate;double signal=Math.Sin(2*Math.PI*820*t)*Math.Exp(-t*38)+.45*Math.Sin(2*Math.PI*1470*t)*Math.Exp(-Math.Abs(t-.055)*85);writer.Write((short)(signal*12000));}}
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        [MenuItem("ARCANA/Build Windows x64")]
        public static void BuildWindows()
        {
            PlaygroundBootstrap.Ensure();string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Builds/Windows/ARCANA.exe"));var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]=="-buildOutput")path=args[i+1];Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildWindowsTo(path);
        }
        public static void BuildWindowsTo(string path)
        {
            PlaygroundBootstrap.Ensure();path=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{PlaygroundBootstrap.ScenePath,ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            string reportPath=Path.Combine(Path.GetDirectoryName(path),"build-summary.json");File.WriteAllText(reportPath,JsonUtility.ToJson(new BuildReceipt{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize,seconds=report.summary.totalTime.TotalSeconds,editor=Application.unityVersion},true));
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed: "+report.summary.result);Debug.Log("ARCANA Windows player: "+path);
        }
        [Serializable] sealed class BuildReceipt{public string result,editor;public int errors,warnings;public ulong bytes;public double seconds;}
    }
}



