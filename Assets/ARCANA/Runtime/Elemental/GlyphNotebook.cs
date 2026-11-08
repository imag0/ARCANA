using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace Arcana.Elemental
{
    public sealed partial class GlyphNotebook : MonoBehaviour
    {
        public ElementalWorld world;public LabInput input;public FirstPersonMotor motor;public TMP_FontAsset font;public GameObject laboratoryCanvas;public DrawingTool laboratoryTool;public LabHud laboratoryHud;
        public Construction draft=new Construction();public GlyphNode selected;public InkLayer layer;public bool Open{get;private set;}public bool AnnexMode{get;private set;}
        public GlyphCanvas board;public TMP_Text status,details,hudText;GameObject notebook,pause,diagnostics;TMP_Text diagnosticsText;Transform canvas;LineRenderer preview;readonly List<InkStroke> glyphInk=new List<InkStroke>();readonly Stack<string> history=new Stack<string>();
        int activeSeal;bool placing,recognizing;Rune stamp;int fromNode=-1,sourceIndex;string targetId="";Vector3 targetPoint;LineRenderer forwardPreview,spinPreview,sourcePreview;readonly List<LineRenderer> branchPreviews=new List<LineRenderer>();readonly Dictionary<string,UnityEngine.UI.Button> controls=new Dictionary<string,UnityEngine.UI.Button>();public string LastInterpretation="Draw a glyph in a notched socket; release to see its interpretation.";
        void Start()
        {
            BuildUI();SetAnnex(false);NewDraft();input.PauseChanged+=PauseChanged;PauseChanged(input.Paused);
            preview=new GameObject("Drawn route preview").AddComponent<LineRenderer>();preview.sharedMaterial=world.trailMaterial;preview.widthMultiplier=.05f;preview.startColor=preview.endColor=Palette.Teal;
            forwardPreview=PreviewLine("Frame forward arrow",Palette.Brass);spinPreview=PreviewLine("Signed spin axis arrow",Palette.Teal);sourcePreview=PreviewLine("Finite source marker",Palette.Brass);
        }
        void OnDestroy(){if(input!=null)input.PauseChanged-=PauseChanged;}
        void PauseChanged(bool value){if(pause!=null)pause.SetActive(value&&!Open&&!AnnexMode);}
        public void SetAnnex(bool enabled)
        {
            AnnexMode=enabled;laboratoryCanvas.SetActive(enabled);laboratoryTool.enabled=enabled;laboratoryHud.enabled=enabled;
            if(canvas!=null)canvas.gameObject.SetActive(!enabled);if(!enabled)world.Say("Elemental mode · G notebook · T target · X release · M mechanical annex mode");
        }
        void Update()
        {
            var keyboard=Keyboard.current;if(keyboard==null)return;if(ComposerInputField.LastFocusedFrame>=Time.frameCount-1)return;if(UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.GetComponent<TMP_InputField>()?.isFocused==true)return;
            if(keyboard.mKey.wasPressedThisFrame&&!Open){SetAnnex(!AnnexMode);return;}if(AnnexMode)return;
            if(keyboard.gKey.wasPressedThisFrame||keyboard.f1Key.wasPressedThisFrame)Show(!Open);
            if(keyboard.tabKey.wasPressedThisFrame&&!Open)diagnostics.SetActive(!diagnostics.activeSelf);
            if(!Open&&!AnnexMode&&!input.Paused){if(Mouse.current?.leftButton.wasPressedThisFrame==true)world.RequestLaunch();if(keyboard.leftBracketKey.wasPressedThisFrame)world.AdjustSpin(.8f);if(keyboard.rightBracketKey.wasPressedThisFrame)world.AdjustSpin(1.25f);}
            if(keyboard.xKey.wasPressedThisFrame&&!Open)world.Cancel();
            if(keyboard.tKey.wasPressedThisFrame&&!Open&&!input.Paused)MarkTarget();
            if(keyboard.f5Key.wasPressedThisFrame)Save();if(keyboard.f9Key.wasPressedThisFrame)Load();
            if(Open&&keyboard.enterKey.wasPressedThisFrame)Recognize();
            if(Open&&keyboard.deleteKey.wasPressedThisFrame)DeleteSelected();
            if(Open&&keyboard.leftCtrlKey.isPressed&&keyboard.zKey.wasPressedThisFrame)Undo();
            if(!Open&&!input.Paused&&keyboard.fKey.wasPressedThisFrame){if(PlaygroundFixture.Carried!=null)PlaygroundFixture.Carried.Release();else if(Physics.Raycast(motor.view.ViewportPointToRay(new Vector3(.5f,.5f)),out var grab,5))grab.collider.GetComponentInParent<PlaygroundFixture>()?.Carry(motor);}
            if(!Open&&keyboard.eKey.wasPressedThisFrame&&!input.Paused)
            {
                if(Physics.Raycast(motor.view.ViewportPointToRay(new Vector3(.5f,.5f)),out var hit,5))
                {var fixture=hit.collider.GetComponentInParent<PlaygroundFixture>();if(fixture!=null)fixture.Use(this);}
            }
            if(Open&&keyboard.escapeKey.wasPressedThisFrame)Show(false);
            var hudNode=selected??draft.nodes.FirstOrDefault(n=>GlyphPlan.Element(n.rune));var hudSource=world.Source(hudNode?.sourceId)??world.Source(draft.nodes.FirstOrDefault(n=>GlyphPlan.Element(n.rune))?.sourceId);
            hudText.text=$"ARCANA   ·   {world.ledger.remaining:F1} charge   ·   {world.casts.Count(c=>c.active)} active seals   ·   {hudNode?.rune.ToString().ToUpperInvariant()??"NO GLYPH SELECTED"}\n{(hudSource!=null?hudSource.label+" · "+hudSource.Available.ToString("F1")+" kg available":"G opens the alphabet and editable examples")}   ·   {(world.IsControlling(draft.id)?"CONTROLLING":draft.committed?"RELEASED":"DRAFT")}\nLMB LAUNCH   [ / ] SPIN   G NOTEBOOK   T TARGET   X RELEASE   E USE   F CARRY   M ANNEX   ESC PAUSE";
            if(Open){RefreshText();RefreshComposer();RefreshSealFeedback();}else{var activeCast=world.casts.LastOrDefault(c=>c.active&&c.channels.Any(ch=>ch.launchGroup>=0));status.text=activeCast!=null&&activeCast.launches.Any(l=>!l.launched&&!l.impacted)?activeCast.status:world.Message;}
            if(diagnostics.activeSelf)diagnosticsText.text=$"ELEMENTAL DIAGNOSTICS\n100 Hz physics · {world.bodies.Count}/{ElementalWorld.BodyLimit} material bodies\nStep {world.lastStepMs:F2} ms · steps {world.simulationSteps}\nCharge balance {world.ledger.Residual:E3} J\nDrive {world.ledger.drive:F0} J · heat {world.ledger.heat:F0} J\nCooling work {world.ledger.cooling:F0} J · loss {world.ledger.loss:F0} J\nSustain {world.ledger.overhead:F0} J · discharge {world.ledger.discharge:F0} J\n1 charge = 10000 J; separate from dimensionless Theta ledger.\n{Arcana.Core.ReferenceVerification.Summary}\nSee SCIENTIFIC_REPORT.md; elemental gameplay is independent.\n"+string.Join("\n",world.MaterialTotals().Select(k=>$"{k.Key}: {k.Value:F3} kg"));
            DrawPreview();
        }
        public void Show(bool value){Open=value;notebook.SetActive(value);input.SetPaused(value);pause.SetActive(false);if(value)RefreshText();}
        void Remember(){if(history.Count>50)history.Clear();history.Push(JsonUtility.ToJson(draft));}
        public void Undo(){if(draft.committed){world.Say("Committed casts cannot be undone into free resources. Reopen or load a whole-world snapshot.");return;}if(history.Count>0){draft=JsonUtility.FromJson<Construction>(history.Pop());selected=null;board.SetVerticesDirty();}}
        public void NewDraft()
        {
            activeSeal=0;draft=new Construction{origin=motor.transform.position,forward=Vector3.ProjectOnPlane(motor.transform.forward,Vector3.up).normalized,target=motor.transform.position+motor.transform.forward*6+Vector3.up*2,vertical=true};targetPoint=draft.target;selected=null;glyphInk.Clear();history.Clear();if(board!=null)board.SetVerticesDirty();
        }
        public void Reopen(){world.Cancel(draft.id);draft=draft.Copy();draft.id=Guid.NewGuid().ToString("N");draft.committed=false;draft.sealClosed=false;if(draft.seal.Count>2)draft.seal.RemoveRange(Math.Max(1,draft.seal.Count-5),Math.Min(5,draft.seal.Count-1));board.SetVerticesDirty();}
        public void SelectLayer(InkLayer value){layer=value;if(value==InkLayer.Seal)activeSeal=0;placing=false;glyphInk.Clear();LastInterpretation=value==InkLayer.Seal?"Seal layer only: trace one outer contour, leave a gap, then join it.":value==InkLayer.Glyphs?"Freehand: draw in a socket; ENTER finishes a multi-stroke glyph.":value==InkLayer.Connectors?"Draw an arrow FROM a socket TO another. Crossings alone are insulated.":value==InkLayer.Form?"Select FORM first to edit its own shape; otherwise edit the shared shape.":value==InkLayer.Depth?"Select FORM or DIRECT. Draw depth versus progress: left=start, right=end; up=positive depth.":"Select DIRECT first to edit its own route; otherwise edit the shared path.";}
        public void BeginStroke(Vector2 point)
        {
            if(draft.committed){world.Say("Reopen this seal before editing it.");return;}
            if(layer==InkLayer.Seal&&activeSeal==0)
            {
                bool Near(System.Collections.Generic.List<Vector2> p)=>p.Count>0&&Mathf.Min(Vector2.Distance(p[0],point),Vector2.Distance(p[p.Count-1],point))<.12f;
                if(!Near(draft.seal)){var inner=draft.innerSeals.FirstOrDefault(s=>!s.closed&&Near(s.points));if(inner!=null)activeSeal=inner.id;}
            }
            if(layer==InkLayer.Glyphs)
            {
                if(placing){Remember();selected=draft.Add(stamp,point);if(stamp==Rune.Form)selected.fillMode=2;AutoSource(selected);placing=false;return;}
                var nearest=draft.nodes.OrderBy(n=>Vector2.Distance(point,n.position)).FirstOrDefault();
                if(nearest==null||Vector2.Distance(nearest.position,point)>.18f){Remember();selected=draft.Add(Rune.Unknown,point);glyphInk.Clear();}
                else if(selected!=nearest){selected=nearest;glyphInk.Clear();}
            }
            if(layer==InkLayer.Connectors){var node=draft.nodes.OrderBy(n=>Vector2.Distance(point,n.position)).FirstOrDefault();fromNode=node!=null&&Vector2.Distance(node.position,point)<.18f?node.id:-1;}
        }
        public void CommitStroke(List<Vector2> points)
        {
            if(draft.committed||points.Count<2)return;Remember();
            switch(layer)
            {
                case InkLayer.Glyphs:
                    if(selected==null)return;var ink=new InkStroke{layer=layer,points=points};glyphInk.Add(ink);draft.ink.Add(ink);Recognize();break;
                case InkLayer.Connectors:
                    var end=draft.nodes.OrderBy(n=>Vector2.Distance(points[points.Count-1],n.position)).FirstOrDefault();
                    if(fromNode<0||end==null||Vector2.Distance(end.position,points[points.Count-1])>.18f||end.id==fromNode){LastInterpretation="Connector needs two different sockets.";break;}
                    draft.edges.RemoveAll(e=>e.from==fromNode&&e.to==end.id);draft.edges.Add(new GlyphEdge{from=fromNode,to=end.id,points=points});LastInterpretation="Arrow connected. Crossing arrows remain separate.";break;
                case InkLayer.Form:if(selected?.rune==Rune.Form)selected.sketch=DrawnGeometry.Resample(points);else draft.form=DrawnGeometry.Resample(points);break;
                case InkLayer.Trajectory:if(selected?.rune==Rune.Direct)selected.sketch=DrawnGeometry.Resample(points);else draft.trajectory=DrawnGeometry.Resample(points);break;
                case InkLayer.Depth:if(selected!=null&&(selected.rune==Rune.Form||selected.rune==Rune.Direct))selected.depthProfile=DrawnGeometry.Resample(points);else LastInterpretation="Select a FORM or DIRECT socket before drawing depth.";break;
                case InkLayer.Seal:CommitSealStroke(points);break;
            }
            board.SetVerticesDirty();
        }
        public void Recognize()
        {
            if(selected==null||glyphInk.Count==0)return;var result=GlyphAlphabet.Recognize(glyphInk);selected.rune=result.rune;selected.confidence=result.confidence;selected.spin=result.spin;LastInterpretation=result.message;
            if(string.IsNullOrEmpty(selected.sourceId)&&world.sources.Count>0)selected.sourceId=world.sources[sourceIndex%world.sources.Count].stableId;board.SetVerticesDirty();
            if(selected.rune==Rune.Form)selected.fillMode=2;if(GlyphPlan.Element(selected.rune))AutoSource(selected);
        }
        void AutoSource(GlyphNode node)
        {
            if(node.rune!=Rune.Water&&node.rune!=Rune.Stone)return;var kind=node.rune==Rune.Water?MatterKind.Water:MatterKind.Stone;
            var current=world.Source(node.sourceId);if(current!=null&&current.kind==kind&&current.Available>0)return;
            var source=world.sources.Where(s=>s.kind==kind&&s.Available>0).OrderBy(s=>Vector3.Distance(s.Position,motor.transform.position)).FirstOrDefault();if(source==null)return;node.sourceId=source.stableId;sourceIndex=world.sources.IndexOf(source);
        }
        public void Stamp(Rune rune){layer=InkLayer.Glyphs;stamp=rune;placing=true;LastInterpretation="Click the page to place "+rune+". Or choose Freehand to draw it yourself.";}
        public void Correct(Rune rune){if(selected==null){Stamp(rune);return;}Remember();selected.rune=rune;selected.confidence=1;glyphInk.Clear();LastInterpretation="Selected socket corrected to "+rune;board.SetVerticesDirty();}
        public void ClearLayer()
        {
            if(draft.committed)return;Remember();if(layer==InkLayer.Form){if(selected?.rune==Rune.Form)selected.sketch.Clear();else draft.form.Clear();}else if(layer==InkLayer.Trajectory){if(selected?.rune==Rune.Direct)selected.sketch.Clear();else draft.trajectory.Clear();}else if(layer==InkLayer.Depth){selected?.depthProfile.Clear();}else if(layer==InkLayer.Seal){if(activeSeal!=0){var region=draft.innerSeals.Find(s=>s.id==activeSeal);if(region!=null)draft.innerSeals.Remove(region);activeSeal=0;LastInterpretation="Inner seal removed. Choose OUTER SEAL or NEW INNER SEAL.";}else{draft.seal.Clear();draft.sealClosed=false;}}else if(layer==InkLayer.Connectors)draft.edges.Clear();else DeleteSelected();board.SetVerticesDirty();
        }
        public void DeleteSelected(){if(selected==null||draft.committed)return;Remember();int id=selected.id;draft.nodes.Remove(selected);draft.edges.RemoveAll(e=>e.from==id||e.to==id);selected=null;glyphInk.Clear();board.SetVerticesDirty();}
        public void CycleSource(){if(world.sources.Count==0)return;sourceIndex=(sourceIndex+1)%world.sources.Count;if(selected!=null)selected.sourceId=world.sources[sourceIndex].stableId;}
        public void MarkTarget()
        {
            if(Physics.Raycast(motor.view.ViewportPointToRay(new Vector3(.5f,.5f)),out var hit,55))
            {targetPoint=hit.point;var b=hit.collider.GetComponentInParent<MatterBody>();var p=hit.collider.GetComponentInParent<ConductivePost>();targetId=b!=null?b.id:p!=null?p.stableId:"";draft.target=targetPoint;if(selected!=null)selected.targetId=targetId;world.Say("Target marked. G opens notebook; TARGET locks this point unless tracking is enabled.");}
        }
        public void Example(string name){if(draft.committed)world.Cancel(draft.id);draft=GlyphExamples.Create(name,motor.transform.position,motor.transform.forward,world.sources);selected=draft.nodes[0];glyphInk.Clear();board.SetVerticesDirty();LastInterpretation="Editable example. Change the route/shape or operators, then close the brass gap.";}
        void DrawPreview()
        {
            if(preview==null)return;foreach(var line in branchPreviews)line.positionCount=0;forwardPreview.positionCount=spinPreview.positionCount=sourcePreview.positionCount=0;if(draft.committed){preview.positionCount=0;return;}var plan=GlyphPlan.Compile(draft);if(!plan.valid){preview.positionCount=0;return;}List<Channel> channels;try{channels=world.Evaluate(draft,plan);}catch(InvalidOperationException){preview.positionCount=0;return;}var geometries=channels.SelectMany(c=>new[]{c.shape,c.direct?c.path:new List<Vector3>()}).Where(p=>p.Count>1).ToList();for(int index=1;index<geometries.Count;index++){while(branchPreviews.Count<index)branchPreviews.Add(PreviewLine("Independent branch preview",Palette.Teal));branchPreviews[index-1].positionCount=geometries[index].Count;branchPreviews[index-1].SetPositions(geometries[index].ToArray());}var path=geometries.FirstOrDefault();
            PreviewArrow(forwardPreview,draft.origin+draft.up*draft.height+draft.forward.normalized*2,draft.forward.normalized*2);
            var spinning=channels.FirstOrDefault(c=>Mathf.Abs(c.spin)>0);if(spinning!=null)PreviewArrow(spinPreview,spinning.anchor,spinning.axis.normalized*Mathf.Sign(spinning.spin)*2);
            var source=channels.Select(c=>world.Source(c.sourceId)).FirstOrDefault(s=>s!=null);if(source!=null){var p=source.Position;sourcePreview.positionCount=6;sourcePreview.SetPositions(new[]{p-Vector3.right*.25f,p+Vector3.right*.25f,p,p-Vector3.forward*.25f,p+Vector3.forward*.25f,p});}
            if(path==null){preview.positionCount=0;return;}preview.positionCount=path.Count;preview.SetPositions(path.ToArray());bool blocked=false;for(int i=1;i<path.Count;i++)if(Physics.Linecast(path[i-1],path[i],~(1<<2),QueryTriggerInteraction.Ignore)){blocked=true;break;}preview.startColor=preview.endColor=blocked?Palette.Red:Palette.Teal;
        }
        LineRenderer PreviewLine(string label,Color color){var line=new GameObject(label).AddComponent<LineRenderer>();line.sharedMaterial=world.trailMaterial;line.widthMultiplier=.045f;line.startColor=line.endColor=color;line.positionCount=0;return line;}
        static void PreviewArrow(LineRenderer line,Vector3 start,Vector3 vector){var tip=start+vector;var side=Vector3.Cross(vector.normalized,Mathf.Abs(vector.normalized.y)>.9f?Vector3.right:Vector3.up).normalized*.2f;line.positionCount=5;line.SetPositions(new[]{start,tip,tip-vector.normalized*.35f+side,tip,tip-vector.normalized*.35f-side});}
        void RefreshText()
        {
            RefreshSealFeedback();status.text=LastInterpretation+"\n"+SealStatus;
            bool element=selected!=null&&GlyphPlan.Element(selected.rune);void Enable(string label,bool value){if(controls.TryGetValue(label,out var button))button.interactable=value&&!draft.committed;}
            Enable("NEXT SOURCE",selected?.rune==Rune.Water||selected?.rune==Rune.Stone);Enable("DOTS +",element);Enable("BARS +",element||selected?.rune==Rune.Hold);Enable("QUANTITY ×2",selected?.rune==Rune.Gather||selected?.rune==Rune.Spread);Enable("REVERSE SPIN",selected?.rune==Rune.Spin);Enable("AXIS X/Y/Z",selected?.rune==Rune.Spin);Enable("ANCHOR WORLD/CASTER",selected?.rune==Rune.Hold||selected?.rune==Rune.Spin);Enable("TRACK TARGET ON/OFF",selected?.rune==Rune.Target||selected?.rune==Rune.Hold);Enable("CORRECT TO STAMP",selected!=null);Enable("ERASE SOCKET",selected!=null);
            if(selected==null){details.text="CREATE ANY CONSTRUCTION\n1. Stamp or draw elements and operators.\n2. Connect their sockets with arrow strokes.\n3. Select each FORM / DIRECT and draw its sketch.\n4. Draw an OUTER SEAL around the graph and close it.\n\nWRITE YOUR OWN\nClick an alphabet entry and then the page to stamp,\nor use FREEHAND MODE and copy its symbol.\nENTER finishes a glyph drawn in several strokes.\n\nConnect element to operators with arrow strokes.\nFORM defines arrangement; DIRECT defines movement.\nSETTINGS accepts sizes, forces and arbitrary axes.\nDEPTH draws a third coordinate along your sketch.\nExamples below are optional starting drawings.\nOnly the OUTER SEAL layer activates magic.\n\nSelect a socket to read its meaning and properties.\nT marks a world target. E opens a nearby fixture.\nF carries or releases a portable board.\n\nG returns to the world. F5 saves; F9 restores.\nThe complete glyph reference is ELEMENTAL_README.md.";return;}
            var source=world.Source(selected.sourceId);
            details.text=$"{layer.ToString().ToUpperInvariant()} LAYER\n{(selected==null?"START HERE\n1. Click WATER ROUTE below the page.\n2. Select DIRECT PATH and draw a new curve.\n3. Select OUTER SEAL. Join the brass gap at the right.\n4. Watch the finite tank feed your curve. X releases.":GlyphAlphabet.ElementDescription(selected.rune))}\n\nSOURCE: {source?.label??"select a material element"}\nAvailable: {source?.Available:F1} kg\n\nIntensity dots: {selected?.intensity??1}  (250/2500/10000 N)\nDuration bars: {selected?.duration??2}  (4/12/45 s)\nQuantity / width: {selected?.width??8:F1}\nSpin: {(selected?.spin>=0?"positive axis":"negative axis")}\nAxis: {selected?.axis}\n\nScale {draft.scale:F1} m · height {draft.height:F1} m\nSpeed {draft.speed:F1} m/s · {(draft.vertical?"vertical drawing plane":"top-down drawing plane")}\nTarget: {draft.target:F1}\n\nElements → operators → shape/route → outer seal.\nSETTINGS: continuous values and 3D orientation.\nSelect FORM / DIRECT before drawing its own sketch.\nFILL switches a FORM between outline and solid area.\nDEPTH adds a third coordinate along the stroke.";
        }
        public void Save(){world.SaveWorld(draft);}
        public void Load(){var loaded=world.LoadWorld();if(loaded!=null){draft=loaded;selected=null;board.SetVerticesDirty();Show(true);}}
        RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=rt.pivot=anchor;rt.anchoredPosition=position;rt.sizeDelta=size;return rt;}
        GameObject Panel(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,Color color)
        {var r=Rect(name,parent,anchor,position,size);r.gameObject.AddComponent<UnityEngine.UI.Image>().color=color;return r.gameObject;}
        TMP_Text Text(string name,Transform parent,Vector2 position,Vector2 size,int fontsize,string value="")
        {var r=Rect(name,parent,new Vector2(0,1),position,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=fontsize;t.color=Palette.Paper;t.text=value;t.raycastTarget=false;return t;}
        void Button(string label,Transform parent,float x,float y,float width,Action action)
        {var g=Panel(label,parent,new Vector2(0,1),new Vector2(x,y),new Vector2(width,34),Palette.Brass);var button=g.AddComponent<UnityEngine.UI.Button>();controls[label]=button;button.targetGraphic=g.GetComponent<UnityEngine.UI.Image>();button.onClick.AddListener(()=>{world.feedback?.Click();action();});var text=Text(label,g.transform,new Vector2(5,-5),new Vector2(width-10,27),17,label);text.color=Palette.Ink;text.alignment=TextAlignmentOptions.Center;}
        void GainSlider(string label,Transform parent,float y,float initial,Action<float> changed)
        {
            Text(label,parent,new Vector2(60,y),new Vector2(190,30),18,label);
            var track=Panel(label+" level",parent,new Vector2(0,1),new Vector2(260,y),new Vector2(330,22),Palette.Teal);
            var handle=Panel("Handle",track.transform,new Vector2(0,.5f),Vector2.zero,new Vector2(16,30),Palette.Brass).GetComponent<RectTransform>();
            var slider=track.AddComponent<UnityEngine.UI.Slider>();slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();slider.minValue=0;slider.maxValue=1;slider.SetValueWithoutNotify(initial);slider.onValueChanged.AddListener(value=>changed(value));
        }
        void BuildUI()
        {
            canvas=new GameObject("Elemental interface",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster)).transform;canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var top=Panel("Charge and controls",canvas,new Vector2(0,1),new Vector2(24,-20),new Vector2(1130,112),Palette.Ink);hudText=Text("HUD",top.transform,new Vector2(18,-12),new Vector2(1090,92),22);
            status=Text("Status",canvas,new Vector2(28,-960),new Vector2(1840,100),23);status.color=Palette.Paper;
            Text("Reticle",canvas,new Vector2(948,-525),new Vector2(30,35),25,"+");
            notebook=Panel("Glyph notebook",canvas,new Vector2(0,1),Vector2.zero,new Vector2(1920,930),Palette.Ink);
            Text("Title",notebook.transform,new Vector2(24,-20),new Vector2(1500,65),30,"ARCANA / GLYPH NOTEBOOK    ·    world paused while writing");
            string[] layers={"1 GLYPHS","2 CONNECTORS","3 FORM SHAPE","4 DIRECT PATH","5 OUTER SEAL"};for(int i=0;i<5;i++){int index=i;Button(layers[i],notebook.transform,330+i*180,-88,170,()=>SelectLayer((InkLayer)index));}
            Text("Alphabet label",notebook.transform,new Vector2(24,-85),new Vector2(280,35),22,"ALPHABET / click to stamp");int row=0;
            foreach(Rune rune in Enum.GetValues(typeof(Rune))){if(rune==Rune.Unknown)continue;var captured=rune;Button(rune.ToString().ToUpperInvariant(),notebook.transform,24,-128-row*31,265,()=>Stamp(captured));var iconRect=Rect("Glyph "+rune,notebook.transform,new Vector2(0,1),new Vector2(32,-128-row*31),new Vector2(30,30));var icon=iconRect.gameObject.AddComponent<GlyphIcon>();icon.rune=rune;icon.color=Palette.Ink;icon.raycastTarget=false;row++;}
            Button("COMPOUND STARTER",notebook.transform,24,-755,265,LoadCompoundStudy);
            Button("FREEHAND MODE",notebook.transform,24,-800,265,()=>{placing=false;layer=InkLayer.Glyphs;glyphInk.Clear();LastInterpretation="Draw in a socket. ENTER completes a multi-stroke glyph.";});
            Button("CORRECT TO STAMP",notebook.transform,24,-840,265,()=>Correct(stamp));
            var page=Panel("Ink page",notebook.transform,new Vector2(0,1),new Vector2(330,-138),new Vector2(900,650),new Color(.07f,.12f,.15f));page.AddComponent<UnityEngine.UI.RectMask2D>();var r=Rect("Drawing input",page.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(900,650));board=r.gameObject.AddComponent<GlyphCanvas>();board.notebook=this;board.raycastTarget=true;
            Button("RECOGNIZE / ENTER",notebook.transform,330,-805,210,Recognize);Button("UNDO",notebook.transform,550,-805,110,Undo);Button("ERASE SOCKET",notebook.transform,670,-805,180,DeleteSelected);Button("CLEAR LAYER",notebook.transform,860,-805,170,ClearLayer);Button("REOPEN SEAL",notebook.transform,1040,-805,190,Reopen);
            Button("WATER ROUTE",notebook.transform,330,-850,170,()=>Example("Water route"));Button("STONE ORBIT",notebook.transform,510,-850,170,()=>Example("Stone orbit"));Button("ICE BRIDGE",notebook.transform,690,-850,170,()=>Example("Ice bridge"));Button("FLAME VORTEX",notebook.transform,870,-850,170,()=>Example("Flame vortex"));Button("NEW DRAFT",notebook.transform,1050,-850,180,NewDraft);
            details=Text("Selected socket",notebook.transform,new Vector2(1260,-130),new Vector2(620,510),20);details.enableAutoSizing=true;details.fontSizeMin=16;details.fontSizeMax=20;
            Button("NEXT SOURCE",notebook.transform,1260,-655,190,CycleSource);Button("DOTS +",notebook.transform,1460,-655,140,()=>{if(selected!=null)selected.intensity=selected.intensity%3+1;});Button("BARS +",notebook.transform,1610,-655,140,()=>{if(selected!=null)selected.duration=selected.duration%3+1;});
            Button("QUANTITY ×2",notebook.transform,1260,-695,190,()=>{if(selected!=null)selected.width=selected.width>=256?8:selected.width*2;});Button("REVERSE SPIN",notebook.transform,1460,-695,190,()=>{if(selected!=null)selected.spin*=-1;});Button("AXIS X/Y/Z",notebook.transform,1660,-695,190,()=>{if(selected!=null)selected.axis=selected.axis==Vector3.up?Vector3.forward:selected.axis==Vector3.forward?Vector3.right:Vector3.up;});
            Button("SCALE +",notebook.transform,1260,-735,140,()=>draft.scale=draft.scale>=40?4:draft.scale+2);Button("HEIGHT +",notebook.transform,1410,-735,140,()=>draft.height=draft.height>=10?0:draft.height+1);Button("PLANE",notebook.transform,1560,-735,140,()=>draft.vertical=!draft.vertical);Button("SPEED +",notebook.transform,1710,-735,140,()=>draft.speed=draft.speed>=10?1:draft.speed+1);
            Button("ANCHOR WORLD/CASTER",notebook.transform,1260,-775,285,()=>{if(selected!=null)selected.attached=!selected.attached;});Button("TRACK TARGET ON/OFF",notebook.transform,1555,-775,285,()=>{if(selected!=null)selected.tracking=!selected.tracking;});
            Button("ROTATE 45",notebook.transform,1260,-815,190,()=>draft.forward=Quaternion.AngleAxis(45,Vector3.up)*draft.forward);Button("MOVE +2 m",notebook.transform,1460,-815,190,()=>draft.origin+=draft.forward*2);Button("MOVE -2 m",notebook.transform,1660,-815,190,()=>draft.origin-=draft.forward*2);
            Button("RETURN TO WORLD [G]",notebook.transform,1260,-850,285,()=>Show(false));Button("SAVE WORLD",notebook.transform,1555,-850,140,Save);Button("LOAD WORLD",notebook.transform,1705,-850,140,Load);BuildComposerUI();BuildSealFeedback();notebook.SetActive(false);
            pause=Panel("Playground pause",canvas,new Vector2(.5f,.5f),Vector2.zero,new Vector2(650,760),Palette.Ink);Text("Welcome",pause.transform,new Vector2(40,-35),new Vector2(570,130),30,"ARCANA / ELEMENTAL PLAYGROUND\nChoose elements. Write instructions. Aim. Close the seal.");
            Button("ENTER / RESUME",pause.transform,60,-195,530,()=>input.SetPaused(false));Button("GLYPH NOTEBOOK [G]",pause.transform,60,-240,530,()=>Show(true));Button("SAVE WORLD [F5]",pause.transform,60,-285,530,Save);Button("LOAD WORLD [F9]",pause.transform,60,-330,530,Load);Button("RESET NEAREST AREA",pause.transform,60,-375,530,()=>world.ResetArea(null));Button("RESET WHOLE PLAYGROUND",pause.transform,60,-420,530,()=>world.ResetArea("all"));Button("QUIT",pause.transform,60,-465,530,()=>Application.Quit());Text("Help",pause.transform,new Vector2(60,-520),new Vector2(530,115),20,"WASD move · mouse look · Shift sprint\nSpace jump · Ctrl crouch · T mark target\nX release · Tab diagnostics · M annex controls\nG opens editable examples and the alphabet.");
            GainSlider("AMBIENT VOLUME",pause.transform,-655,PlayerPrefs.GetFloat("ArcanaAmbientGain",1),v=>world.feedback?.SetMusicGain(v));GainSlider("EFFECTS VOLUME",pause.transform,-700,PlayerPrefs.GetFloat("ArcanaSfxGain",1),v=>world.feedback?.SetSfxGain(v));
            diagnostics=Panel("Elemental diagnostics",canvas,new Vector2(1,.5f),new Vector2(-25,0),new Vector2(700,770),Palette.Ink);diagnosticsText=Text("Details",diagnostics.transform,new Vector2(20,-20),new Vector2(660,730),18);diagnostics.SetActive(false);
        }
    }
}
