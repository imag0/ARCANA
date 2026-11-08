using UnityEngine;
using Arcana.Core;
using Arcana.Mechanics;

namespace Arcana
{
    public sealed class DrawingSurface : MonoBehaviour
    {
        public string stableId="paper",materialLabel="PAPER / BOARD";
        public double coefficient=50,density=50;public bool planar=true;
        public Vector2 halfSize=new Vector2(1.4f,.8f);
        public bool Supported=>planar&&Vector3.Distance(transform.lossyScale,Vector3.one)<.0001f;
        public D3 Local(Vector3 world){var p=transform.InverseTransformPoint(world);p.z=0;return Coordinates.Data(p);}
        public Vector3 World(D3 local)=>transform.TransformPoint(Coordinates.Vector(local));
        public bool Contains(D3 point)=>System.Math.Abs(point.x)<=halfSize.x+.03&&System.Math.Abs(point.y)<=halfSize.y+.03&&System.Math.Abs(point.z)<.01;
        public Stroke NewStroke(bool bonded,double radius)=>new Stroke{surface=stableId,material=materialLabel,C=coefficient,rho=density,radius=radius,bonded=bonded,supported=Supported,normal=new D3(0,0,-1)};
    }
}
