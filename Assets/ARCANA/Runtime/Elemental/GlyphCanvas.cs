using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Arcana.Elemental
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GlyphCanvas : UnityEngine.UI.MaskableGraphic,IPointerDownHandler,IDragHandler,IPointerUpHandler,IScrollHandler
    {
        public GlyphNotebook notebook;bool panning;Vector2 lastPointer;public readonly List<Vector2> stroke=new List<Vector2>();
        InputActionAsset navigationActions;InputActionReference middleReference,rightReference;
        protected override void Start()
        {
            base.Start();var module=FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if(module==null)return;navigationActions=ScriptableObject.CreateInstance<InputActionAsset>();var map=navigationActions.AddActionMap("Notebook navigation");
            var middle=map.AddAction("MiddleClick",InputActionType.PassThrough,"<Mouse>/middleButton");var right=map.AddAction("RightClick",InputActionType.PassThrough,"<Mouse>/rightButton");
            if(module.middleClick==null){middleReference=InputActionReference.Create(middle);module.middleClick=middleReference;}
            if(module.rightClick==null){rightReference=InputActionReference.Create(right);module.rightClick=rightReference;}
            navigationActions.Enable();
        }
        protected override void OnDestroy(){if(navigationActions!=null){navigationActions.Disable();Destroy(navigationActions);}if(middleReference!=null)Destroy(middleReference);if(rightReference!=null)Destroy(rightReference);base.OnDestroy();}
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();var rect=rectTransform.rect;Color grid=new Color(.2f,.31f,.35f);
            var min=ViewToPage(-Vector2.one);var max=ViewToPage(Vector2.one);
            for(float t=Mathf.Floor(min.x*5)/5;t<=max.x;t+=.2f)Line(mesh,new Vector2(t,min.y),new Vector2(t,max.y),grid,1);
            for(float t=Mathf.Floor(min.y*5)/5;t<=max.y;t+=.2f)Line(mesh,new Vector2(min.x,t),new Vector2(max.x,t),grid,1);
            if(notebook==null||notebook.draft==null)return;var c=notebook.draft;
            foreach(var edge in c.edges)
            {
                var from=c.nodes.Find(n=>n.id==edge.from);var to=c.nodes.Find(n=>n.id==edge.to);if(from==null||to==null)continue;Line(mesh,from.position,to.position,Palette.Brass,2);Vector2 d=(to.position-from.position).normalized,p=to.position-d*.12f,n=new Vector2(-d.y,d.x);Line(mesh,to.position-d*.08f,p+n*.03f,Palette.Brass,2);Line(mesh,to.position-d*.08f,p-n*.03f,Palette.Brass,2);
            }
            foreach(var node in c.nodes)
            {
                Color color=node==notebook.selected?Palette.Teal:node.confidence>=.58f?Palette.Paper:Palette.Red;
                Vector2 p=node.position;float s=.085f;Line(mesh,p+new Vector2(-s,s),p+new Vector2(s,s),color,1);Line(mesh,p+new Vector2(0,s),p+new Vector2(0,s+.035f),Palette.Brass,3);
                if(node.rune==Rune.Unknown)continue;foreach(var path in GlyphAlphabet.Get(node.rune).strokes)for(int i=1;i<path.Length;i++)Line(mesh,p+path[i-1]*s,p+path[i]*s,color,2.5f);
            }
            if(notebook.layer==InkLayer.Depth)Poly(mesh,notebook.selected?.depthProfile??new List<Vector2>(),Palette.Teal,3);else{Poly(mesh,notebook.selected?.rune==Rune.Form?c.Sketch(notebook.selected):c.form,new Color(.45f,.7f,1),3);Poly(mesh,notebook.selected?.rune==Rune.Direct?c.Sketch(notebook.selected):c.trajectory,Palette.Teal,3);}foreach(var region in c.innerSeals)Poly(mesh,region.points,region.closed?new Color(.38f,.76f,.69f):Palette.Brass,2);Poly(mesh,c.seal,c.committed?Palette.Teal:Palette.Brass,3);Poly(mesh,stroke,Palette.Paper,2.5f);
            if(!c.committed&&c.seal.Count>1&&Vector2.Distance(c.seal[0],c.seal[c.seal.Count-1])>.07f)foreach(var point in new[]{c.seal[0],c.seal[c.seal.Count-1]}){Line(mesh,point-Vector2.right*.025f,point+Vector2.right*.025f,Palette.Red,4);Line(mesh,point-Vector2.up*.025f,point+Vector2.up*.025f,Palette.Red,4);}
            foreach(var ink in c.ink)if(ink.layer==InkLayer.Glyphs)Poly(mesh,ink.points,new Color(.5f,.6f,.65f,.6f),1);
        }
        void Poly(UnityEngine.UI.VertexHelper mesh,IReadOnlyList<Vector2> points,Color color,float thickness){for(int i=1;i<points.Count;i++)Line(mesh,points[i-1],points[i],color,thickness);}
        void Line(UnityEngine.UI.VertexHelper mesh,Vector2 a,Vector2 b,Color color,float thickness)
        {
            Vector2 size=rectTransform.rect.size*.5f;a=Vector2.Scale(PageToView(a),size);b=Vector2.Scale(PageToView(b),size);var delta=b-a;if(delta.sqrMagnitude<1e-8)return;Vector2 normal=new Vector2(-delta.y,delta.x).normalized*thickness*.5f;int index=mesh.currentVertCount;
            mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);mesh.AddTriangle(index,index+1,index+2);mesh.AddTriangle(index,index+2,index+3);
        }
        float Zoom=>notebook?.draft==null?1:Mathf.Clamp(notebook.draft.canvasZoom,.35f,8);
        Vector2 Pan=>notebook?.draft==null?Vector2.zero:notebook.draft.canvasPan;
        public Vector2 PageToView(Vector2 p)=>p*Zoom+Pan;
        public Vector2 ViewToPage(Vector2 p)=>(p-Pan)/Zoom;
        public Vector2 PageToScreen(Vector2 p)=>RectTransformUtility.WorldToScreenPoint(null,rectTransform.TransformPoint(Vector2.Scale(PageToView(p),rectTransform.rect.size*.5f)));
        Vector2 View(PointerEventData e){RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,e.position,e.pressEventCamera,out var p);var half=rectTransform.rect.size*.5f;return new Vector2(p.x/half.x,p.y/half.y);}
        Vector2 Local(PointerEventData e)=>ViewToPage(View(e));
        public void ResetView(){notebook.draft.canvasZoom=1;notebook.draft.canvasPan=Vector2.zero;SetVerticesDirty();}
        public void OnScroll(PointerEventData e)
        {
            if(stroke.Count>0)return;var view=View(e);var page=ViewToPage(view);notebook.draft.canvasZoom=Mathf.Clamp(Zoom*Mathf.Pow(1.2f,Mathf.Clamp(e.scrollDelta.y,-3,3)),.35f,8);notebook.draft.canvasPan=view-page*Zoom;SetVerticesDirty();e.Use();
        }
        public void OnPointerDown(PointerEventData e)
        {
            panning=e.button==PointerEventData.InputButton.Middle||e.button==PointerEventData.InputButton.Right||Keyboard.current?.spaceKey.isPressed==true;lastPointer=e.position;if(panning)return;
            if(e.button!=PointerEventData.InputButton.Left)return;stroke.Clear();stroke.Add(Local(e));notebook.BeginStroke(stroke[0]);SetVerticesDirty();
        }
        public void OnDrag(PointerEventData e)
        {
            if(panning){var half=rectTransform.rect.size*.5f;var delta=e.position-lastPointer;notebook.draft.canvasPan+=new Vector2(delta.x/half.x,delta.y/half.y)/canvas.scaleFactor;lastPointer=e.position;SetVerticesDirty();return;}
            if(stroke.Count==0)return;var point=Local(e);if(Vector2.Distance(point,stroke[stroke.Count-1])>.004f/Zoom)stroke.Add(point);SetVerticesDirty();
        }
        public void OnPointerUp(PointerEventData e){if(panning){panning=false;return;}if(stroke.Count==0)return;notebook.CommitStroke(new List<Vector2>(stroke));stroke.Clear();SetVerticesDirty();}
    }
}
