using System;
using System.Collections.Generic;
using UnityEngine;
using Arcana.Core;
using Arcana.Mechanics;

namespace Arcana
{
    public sealed class DrawingTool : MonoBehaviour
    {
        public LabInput input;public Camera view;public ExperimentState experiment;public ProbeInstrument probe;public ExperimentPersistence persistence;
        public int Tool {get;private set;}=1;public bool bonded=true;public double radius=.02;
        public string Selected {get;private set;}="";public string Hint {get;private set;}="";
        public Stroke Preview {get;private set;}
        public event Action PreviewChanged;
        DrawingSurface activeSurface;bool wasHeld;float lastSampleTime;
        void OnEnable(){input.PauseChanged+=Pause;}
        void OnDisable(){input.PauseChanged-=Pause;Finish();}
        void Pause(bool paused){if(paused){Finish();wasHeld=false;}}
        public bool Contact(out RaycastHit hit,out DrawingSurface surface)
        {
            surface=null;if(!Physics.Raycast(view.ViewportPointToRay(new Vector3(.5f,.5f,0)),out hit,5,~(1<<2),QueryTriggerInteraction.Ignore))return false;
            surface=hit.collider.GetComponentInParent<DrawingSurface>();return surface!=null;
        }
        void Update()
        {
            if(input.Paused)return;
            for(int i=1;i<=4;i++)if(input.Pressed("Tool"+i)){Finish();Tool=i;}
            if(input.Pressed("Bond")){Finish();bonded=!bonded;experiment.Message(bonded?"Bond mode: same-plane contacts join":"Insulated mode: crossings do not bond");}
            if(input.Pressed("Undo")){Finish();experiment.Undo();}if(input.Pressed("Redo")){Finish();experiment.Redo();}
            if(input.Pressed("Save")){Finish();persistence.Save();}if(input.Pressed("Load")){Finish();persistence.Load();}
            if(input.Pressed("Delete")&&!string.IsNullOrEmpty(Selected)){Finish();experiment.Delete(Selected);Selected="";}
            if(input.Pressed("Join")){Finish();experiment.Join(Selected);}
            if(input.Pressed("ReleaseSupports")){Finish();experiment.ReleaseSupports(Selected);}
            bool contact=Contact(out var hit,out var surface);
            if(contact)Hint=surface.materialLabel+"  C="+surface.coefficient+"  |  "+(surface.Supported?(bonded?"BONDED":"INSULATED"):"UNSUPPORTED SCALE / SURFACE");
            else Hint="Aim at a labelled drawing panel";
            if(hit.collider!=null)
            {
                var station=hit.collider.GetComponentInParent<LabStation>();
                if(station!=null){Hint="E  "+station.kind;if(input.Pressed("Interact")){Finish();Interact(station.kind);return;}}
            }
            if(input.Pressed("Interact")&&contact){if(Pick(surface,surface.Local(hit.point),out var id,out _,out _)){Selected=id;if(Tool==3)experiment.RepositionSupport(surface.stableId,surface.Local(hit.point));else experiment.Message("Stroke selected: Delete removes, J joins endpoints, U releases supports");}}
            if(input.Pressed("Cancel")){Finish();wasHeld=false;return;}
            float wheel=input.Read2("Scroll").y;
            if(Math.Abs(wheel)>.01)
            {
                float direction=Mathf.Sign(wheel);
                if(Tool==4)probe.ChangeDistance(direction*.025f);else if(Tool==3)experiment.AdjustPreload(direction*.001);else radius=Numbers.Clamp(radius+direction*.002,.004,.03);
            }
            if(Tool==4){if(input.Pressed("RotateLeft"))probe.Rotate(-15);if(input.Pressed("RotateRight"))probe.Rotate(15);}
            if(Tool==3&&(input.Pressed("RotateLeft")||input.Pressed("RotateRight")))experiment.AdjustPreload(input.Pressed("RotateLeft")?-.002:.002);
            bool held=input.Held("Fire");
            if(Tool==1)
            {
                if(held)
                {
                    if(!contact||!surface.Supported||Vector3.Dot(hit.normal,-surface.transform.forward)<.98f){Finish();if(contact&&!surface.Supported)experiment.Message("Drawing rejected: use a rigid planar panel with unit scale");}
                    else Sample(surface,surface.Local(hit.point));
                }
                else if(wasHeld)Finish();
            }
            else if(input.Pressed("Fire")&&contact)
            {
                var local=surface.Local(hit.point);
                if(Tool==4)probe.Place(surface,local);
                else if(Pick(surface,local,out var id,out var segment,out var picked)){Selected=id;if(Tool==2)experiment.Cut(id,segment,picked);else experiment.Prepare(id);}
                else experiment.Message("Aim at a visible stroke");
            }
            wasHeld=held;
        }
        public void Sample(DrawingSurface surface,D3 point)
        {
            if(Preview!=null&&(activeSurface!=surface||D3.Distance(Preview.points[Preview.points.Count-1],point)>experiment.config.maxContactJump||Time.unscaledTime-lastSampleTime>.25f))Finish();
            if(Preview==null){activeSurface=surface;Preview=surface.NewStroke(bonded,radius);Preview.points.Add(point);lastSampleTime=Time.unscaledTime;PreviewChanged?.Invoke();return;}
            if(D3.Distance(Preview.points[Preview.points.Count-1],point)<experiment.config.samplingDistance){lastSampleTime=Time.unscaledTime;return;}
            Preview.points.Add(point);lastSampleTime=Time.unscaledTime;PreviewChanged?.Invoke();if(Preview.points.Count>=256)Finish();
        }
        public void Finish()
        {
            if(Preview!=null){var completed=Preview;Preview=null;activeSurface=null;if(completed.points.Count>=2){completed.points=Simplify(completed.points,experiment.config.simplificationTolerance);experiment.AddStroke(completed);}PreviewChanged?.Invoke();}
        }
        public static List<D3> Simplify(List<D3> points,double tolerance)
        {
            if(points.Count<=2)return new List<D3>(points);var keep=new bool[points.Count];keep[0]=keep[points.Count-1]=true;
            void Range(int a,int b){if(b<=a+1)return;var ab=points[b]-points[a];double best=tolerance;int index=-1;for(int i=a+1;i<b;i++){double t=ab.LengthSquared<1e-20?0:Numbers.Clamp(D3.Dot(points[i]-points[a],ab)/ab.LengthSquared,0,1);double d=D3.Distance(points[i],points[a]+ab*t);if(d>best){best=d;index=i;}}if(index>=0){keep[index]=true;Range(a,index);Range(index,b);}}
            Range(0,points.Count-1);var result=new List<D3>();for(int i=0;i<points.Count;i++)if(keep[i])result.Add(points[i]);return result;
        }
        public bool Pick(DrawingSurface surface,D3 point,out string id,out int segment,out D3 picked)
        {
            id=null;segment=-1;picked=default;double best=.065;
            foreach(var stroke in experiment.Geometry.strokes)if(stroke.surface==surface.stableId)for(int i=0;i<stroke.points.Count-1;i++)
            {var a=stroke.points[i];var ab=stroke.points[i+1]-a;double t=ab.LengthSquared<1e-20?0:Numbers.Clamp(D3.Dot(point-a,ab)/ab.LengthSquared,0,1);var closest=a+ab*t;double d=D3.Distance(point,closest);if(d<best){best=d;id=stroke.id;segment=i;picked=closest;}}
            return id!=null;
        }
        public void Interact(LabStation.Kind kind)
        {
            switch(kind){case LabStation.Kind.Winder:experiment.Wind();break;case LabStation.Kind.Guided:experiment.Demo(false);probe.distance=.5f;probe.angle=0;probe.Place(experiment.Surface("paper"),default);break;case LabStation.Kind.WBenchmark:experiment.Demo(true);probe.distance=.5f;probe.angle=0;probe.Place(experiment.Surface("stone"),default);break;case LabStation.Kind.Crossings:experiment.CrossingDemo();probe.Place(experiment.Surface("metal"),default);break;}
        }
    }
}
