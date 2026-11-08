using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arcana.Elemental
{
    public sealed partial class ElementalWorld
    {
        // Earlier version-2 saves have no player-relative launch frame. Derive
        // it from their authored geometry while preserving bodies and flight.
        void RestoreLegacyLaunchFrames(PlaygroundSnapshot snapshot)
        {
            if(snapshot?.casts==null)return;
            foreach(var cast in snapshot.casts)
            {
                if(cast?.channels==null||cast.drawing==null)continue;
                foreach(var group in cast.channels.Where(c=>c.launchGroup>=0).GroupBy(c=>c.launchGroup))
                {
                    if(group.Any(c=>c.formationRadius>0))continue;
                    var points=group.SelectMany(c=>c.form?c.shape:c.path).ToArray();if(points.Length==0)points=group.Select(c=>c.anchor).ToArray();
                    var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
                    float padding=group.Max(c=>c.cells.Count>0?c.cellSize.magnitude*.5f:.25f);bounds.Expand(padding*2);
                    var f=motor.view.transform.forward;float depth=Vector3.Dot(bounds.extents,new Vector3(Mathf.Abs(f.x),Mathf.Abs(f.y),Mathf.Abs(f.z)));
                    foreach(var c in group)
                    {
                        c.assemblyCenter=bounds.center;c.frontDepth=depth;c.frontDown=bounds.extents.y;c.formationRadius=bounds.extents.magnitude;
                        var form=cast.drawing.nodes.Find(n=>n.id==c.formNode);if(c.cells.Count==0&&form!=null&&Quaternion.Angle(c.cellRotation,Quaternion.identity)<.001f)c.cellRotation=cast.drawing.Frame(form)*Quaternion.Euler(-90,0,0);
                    }
                    var state=cast.launches?.Find(l=>l.node==group.Key);if(state!=null){state.center=state.referenceCenter=bounds.center;state.offset=Vector3.zero;}
                }
            }
        }
        void SizeChannels(Construction drawing,List<Channel> channels)
        {
            float size=drawing.spellSize;if(Mathf.Abs(size-1)<1e-6f)return;float volume=size*size*size;
            var pivot=drawing.Map(Vector2.zero,true);
            foreach(var c in channels)
            {
                c.anchor=pivot+(c.anchor-pivot)*size;
                for(int i=0;i<c.shape.Count;i++)c.shape[i]=pivot+(c.shape[i]-pivot)*size;
                for(int i=0;i<c.path.Count;i++)c.path[i]=pivot+(c.path[i]-pivot)*size;
                for(int i=0;i<c.cells.Count;i++)c.cells[i]=pivot+(c.cells[i]-pivot)*size;
                if(c.direct&&!c.worldPath&&c.launchGroup<0&&c.path.Count>1&&Source(c.sourceId)!=null)
                {var offset=Source(c.sourceId).Position-c.path[0];for(int i=0;i<c.path.Count;i++)c.path[i]+=offset;}
                c.cellSize*=size;c.width*=size;c.length=DrawnGeometry.Length(c.path);
                c.amount*=volume;c.rate*=volume;c.requiredMass*=volume;c.force=Mathf.Min(1000000,c.force*volume);c.power=Mathf.Min(100000000,c.power*volume);
            }
        }
        // Camera-local placement is shared by preview, validation and casting.
        // Fixed world constructions without LAUNCH retain their authored locations.
        void PlaceLaunchChannels(Construction drawing,List<Channel> channels)
        {
            if(motor==null)return;
            var turn=Quaternion.LookRotation(motor.view.transform.forward,motor.view.transform.up)*Quaternion.Inverse(Quaternion.LookRotation(drawing.forward,drawing.up));
            foreach(var group in channels.Where(c=>c.launchGroup>=0).GroupBy(c=>c.launchGroup))
            {
                var all=group.SelectMany(c=>c.form?c.shape:c.path).ToArray();if(all.Length==0)all=group.Select(c=>c.anchor).ToArray();
                var bounds=new Bounds(all[0],Vector3.zero);foreach(var p in all)bounds.Encapsulate(p);var pivot=bounds.center;
                float radius=0,depth=0,down=0;
                foreach(var c in group)
                {
                    var points=c.cells.Count>0?c.cells:c.form?c.shape:c.path;float padding=c.cells.Count>0?c.cellSize.magnitude*.5f:c.element==Rune.Stone||c.element==Rune.Water?Mathf.Max(.06f,Mathf.Pow(c.amount/48/(c.element==Rune.Water?1000:2400)*3/(4*Mathf.PI),1f/3)):.2f;
                    foreach(var p in points){var v=turn*(p-pivot);radius=Mathf.Max(radius,v.magnitude+padding);depth=Mathf.Max(depth,Mathf.Abs(Vector3.Dot(v,motor.view.transform.forward))+padding);down=Mathf.Max(down,-v.y+padding);}
                }
                var center=FrontCenter(depth,down);
                foreach(var c in group)
                {
                    c.assemblyCenter=center;c.formationRadius=radius;c.frontDepth=depth;c.frontDown=down;
                    c.anchor=center+turn*(c.anchor-pivot);c.axis=turn*c.axis;c.cellRotation=turn*c.cellRotation;
                    for(int i=0;i<c.shape.Count;i++)c.shape[i]=center+turn*(c.shape[i]-pivot);
                    for(int i=0;i<c.cells.Count;i++)c.cells[i]=center+turn*(c.cells[i]-pivot);
                    for(int i=0;i<c.path.Count;i++)c.path[i]=center+turn*(c.path[i]-pivot);
                }
            }
        }
        Vector3 FrontCenter(float depth,float down)
        {
            var center=motor.view.transform.position+motor.view.transform.forward*Mathf.Max(1.5f,depth+1);
            // Keep oversized rings above nearby terrain rather than burying them.
            if(Physics.Raycast(motor.transform.position+Vector3.up*.2f,Vector3.down,out var floor,4,~(1<<2),QueryTriggerInteraction.Ignore))center.y=Mathf.Max(center.y,floor.point.y+down+.18f);
            return center;
        }
        bool LaunchSpaceClear(List<Channel> channels,out string reason)
        {
            foreach(var c in channels.Where(c=>c.launchGroup>=0&&c.element!=Rune.Lightning))
            {
                var points=c.cells.Count>0?c.cells:c.form?c.shape:c.path;int stride=Mathf.Max(1,points.Count/16);
                for(int i=0;i<points.Count;i+=stride)
                {
                    float radius=c.cells.Count>0?c.cellSize.minComponent()*.42f:.08f;
                    foreach(var hit in Physics.OverlapSphere(points[i],radius,~(1<<2),QueryTriggerInteraction.Ignore))
                    {
                        if(hit.GetComponentInParent<FirstPersonMotor>()!=null)continue;
                        reason="Launch space is blocked by "+hit.gameObject.name+". Aim at open space, step back, or use SMALLER.";return false;
                    }
                }
            }
            reason="";return true;
        }
    }
}
