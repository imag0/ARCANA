using System.Collections.Generic;
using UnityEngine;
using Arcana.Mechanics;

namespace Arcana
{
    public sealed class StrokeView : MonoBehaviour
    {
        public ExperimentState experiment;public DrawingTool tool;public Material strokeMaterial,supportMaterial;
        readonly List<GameObject> objects=new List<GameObject>();LineRenderer preview;
        void OnEnable(){experiment.Changed+=Rebuild;tool.PreviewChanged+=Preview;}
        void OnDisable(){experiment.Changed-=Rebuild;tool.PreviewChanged-=Preview;}
        void Start(){Rebuild();}
        void Clear(){foreach(var item in objects)if(item!=null)Destroy(item);objects.Clear();}
        LineRenderer Line(Stroke stroke,string name)
        {
            var surface=experiment.Surface(stroke.surface);var go=new GameObject(name);go.layer=2;go.transform.SetParent(transform);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=strokeMaterial;line.useWorldSpace=true;line.positionCount=stroke.points.Count;line.widthMultiplier=(float)stroke.radius*2;line.numCapVertices=4;line.numCornerVertices=3;
            for(int i=0;i<stroke.points.Count;i++)line.SetPosition(i,surface.World(stroke.points[i])-surface.transform.forward*.009f);
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return line;
        }
        public void Rebuild()
        {
            Clear();var graph=experiment.Mechanics?.graph;
            foreach(var stroke in experiment.Geometry.strokes){var line=Line(stroke,"Stroke "+stroke.id);bool active=graph!=null&&graph.edges.Exists(e=>e.stroke.id==stroke.id&&System.Math.Abs(e.strain)>1e-7);line.startColor=line.endColor=active?Palette.Teal:(stroke.bonded?Palette.Ink:Palette.Red);objects.Add(line.gameObject);}
            if(graph==null)return;
            foreach(var s in experiment.Geometry.supports){int n=graph.FindSupport(s);if(n<0)continue;var surface=experiment.Surface(s.surface);var position=surface.World(s.position)-surface.transform.forward*.035f;Marker("Installed panel support",position,new Vector3(.075f,.075f,.08f),supportMaterial,PrimitiveType.Cube);}
            for(int i=0;i<graph.nodes.Count;i++){int degree=0;foreach(var edge in graph.edges)if(edge.a==i||edge.b==i)degree++;if(degree<3)continue;var node=graph.nodes[i];var surface=experiment.Surface(node.surface);Marker("Bonded junction",surface.World(node.position)-surface.transform.forward*.02f,Vector3.one*.05f,supportMaterial,PrimitiveType.Sphere);}
        }
        void Marker(string name,Vector3 position,Vector3 scale,Material material,PrimitiveType primitive){var go=GameObject.CreatePrimitive(primitive);go.name=name;go.layer=2;go.transform.SetParent(transform);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;Destroy(go.GetComponent<Collider>());objects.Add(go);}
        void Preview(){if(preview!=null)Destroy(preview.gameObject);if(tool.Preview==null)return;preview=Line(tool.Preview,"Uncommitted pen contact");preview.startColor=preview.endColor=Palette.Brass;}
    }
}
