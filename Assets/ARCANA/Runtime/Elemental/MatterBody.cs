using System;
using UnityEngine;

namespace Arcana.Elemental
{
    [Serializable] public sealed class BodyState
    {
        public string id,origin,owner;public MatterKind kind;public MatterPhase phase;public double kg,enthalpy,wetness,fuelKg,electricalJ;
        public Vector3 position,velocity,angularVelocity,scale,sheetSize;public Quaternion rotation;public float radius;public bool burning,bridge,fractured,kinematic;
    }
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MatterBody : MonoBehaviour
    {
        public string id,origin,owner;public MatterKind kind;public MatterPhase phase;public double kg=1,enthalpy,wetness,fuelKg,electricalJ;
        public float radius=.1f;public bool burning,bridge,fractured;public ElementalWorld world;public Rigidbody Body{get;private set;}
        Renderer mesh;MaterialPropertyBlock tint;TrailRenderer flowTrail;public bool Controlled=>!string.IsNullOrEmpty(owner)&&world!=null&&world.IsControlling(owner);
        public Vector3 sheetSize;static Mesh sphereMesh,cubeMesh;
        public void SetSheet(Vector3 size)
        {
            var sphere=GetComponent<SphereCollider>();if(sphere==null)return;
            if(sphereMesh==null)sphereMesh=GetComponent<MeshFilter>().sharedMesh;
            if(cubeMesh==null&&size.sqrMagnitude>0){var temporary=GameObject.CreatePrimitive(PrimitiveType.Cube);temporary.SetActive(false);cubeMesh=temporary.GetComponent<MeshFilter>().sharedMesh;Destroy(temporary);}
            var box=GetComponent<BoxCollider>();bool shaped=size.sqrMagnitude>0;if(shaped&&box==null)box=gameObject.AddComponent<BoxCollider>();if(box!=null){if(box.enabled!=shaped)box.enabled=shaped;if(box.size!=Vector3.one)box.size=Vector3.one;}if(sphere.enabled==shaped)sphere.enabled=!shaped;var filter=GetComponent<MeshFilter>();var wanted=shaped?cubeMesh:kind==MatterKind.Stone||kind==MatterKind.Soil?MaterialAppearance.Rock():sphereMesh;if(filter.sharedMesh!=wanted)filter.sharedMesh=wanted;
            var scale=shaped?size:Vector3.one*radius*2;if(transform.localScale!=scale)transform.localScale=scale;
        }
        public double Temperature=>kind==MatterKind.Water?(enthalpy<0?Math.Min(0,(enthalpy/kg+334000)/2100):enthalpy/kg<420000?enthalpy/kg/4200:100+Math.Max(0,enthalpy/kg-2680000)/2000):enthalpy/(Math.Max(.001,kg)*800);
        void Awake(){Body=GetComponent<Rigidbody>();mesh=GetComponent<Renderer>();tint=new MaterialPropertyBlock();}
        void LateUpdate()
        {
            bool liquid=kind==MatterKind.Water&&phase==MatterPhase.Liquid&&sheetSize.sqrMagnitude==0&&world!=null&&Body!=null;
            if(liquid&&flowTrail==null)
            {
                flowTrail=gameObject.AddComponent<TrailRenderer>();flowTrail.sharedMaterial=world.effectMaterial??world.trailMaterial;flowTrail.time=.22f;flowTrail.minVertexDistance=.025f;flowTrail.widthMultiplier=radius*1.2f;flowTrail.startColor=new Color(.12f,.62f,.84f,.7f);flowTrail.endColor=new Color(.12f,.62f,.84f,0);flowTrail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;flowTrail.receiveShadows=false;flowTrail.Clear();
            }
            if(flowTrail!=null){flowTrail.emitting=liquid&&Body.linearVelocity.sqrMagnitude>.25f;flowTrail.widthMultiplier=radius*1.2f;}
        }
        void OnDisable(){if(flowTrail!=null)flowTrail.Clear();}
        public void Refresh()
        {
            if(Body==null)Body=GetComponent<Rigidbody>();if(mesh==null)mesh=GetComponent<Renderer>();if(tint==null)tint=new MaterialPropertyBlock();
            if(Math.Abs(Body.mass-kg)>.00001)Body.mass=(float)Math.Max(.01,kg);var detection=Body.isKinematic?CollisionDetectionMode.ContinuousSpeculative:CollisionDetectionMode.ContinuousDynamic;if(Body.collisionDetectionMode!=detection)Body.collisionDetectionMode=detection;if(Body.interpolation!=RigidbodyInterpolation.Interpolate)Body.interpolation=RigidbodyInterpolation.Interpolate;Body.useGravity=true;
            var previousPhase=phase;if(kind==MatterKind.Water)
            {
                phase=enthalpy<=-334000*kg?MatterPhase.Ice:enthalpy>=2680000*kg?MatterPhase.Steam:MatterPhase.Liquid;
                Body.linearDamping=phase==MatterPhase.Steam?.6f:.05f;Body.angularDamping=.1f;
            }
            else phase=MatterPhase.Solid;
            if(phase==MatterPhase.Ice&&previousPhase!=phase)fractured=false;
            if(bridge&&phase!=MatterPhase.Ice&&kind!=MatterKind.Stone){if(world!=null)world.RemoveIceBonds(this);else foreach(var joint in GetComponents<FixedJoint>())Destroy(joint);bridge=false;}
            if(sheetSize.sqrMagnitude>0)SetSheet(kind==MatterKind.Stone||kind==MatterKind.Soil||phase==MatterPhase.Ice||Controlled?sheetSize:Vector3.zero);
            Color color=kind==MatterKind.Water?(phase==MatterPhase.Ice?new Color(.65f,.88f,1):phase==MatterPhase.Steam?new Color(.75f,.82f,.85f):new Color(.12f,.55f,.8f)):kind==MatterKind.Soil?new Color(.37f,.25f,.13f):kind==MatterKind.Fuel?Palette.Wood:new Color(.48f,.46f,.4f);
            if(Temperature>120)color=Color.Lerp(color,new Color(1,.2f,.04f),(float)Math.Min(.8,(Temperature-120)/600));if(wetness>0)color*=.7f;
            if(world!=null)mesh.sharedMaterial=kind==MatterKind.Water&&phase==MatterPhase.Liquid?MaterialAppearance.Water(world.bodyMaterial):world.bodyMaterial;
            mesh.GetPropertyBlock(tint);tint.SetFloat("_Smoothness",kind==MatterKind.Water&&phase==MatterPhase.Liquid?.94f:.18f);tint.SetColor("_BaseColor",color);mesh.SetPropertyBlock(tint);
        }
        public double AddHeat(double joules){double before=enthalpy;enthalpy+=joules;if(enthalpy!=before)Refresh();return joules;}
        public BodyState Capture()=>new BodyState{id=id,origin=origin,owner=owner,kind=kind,phase=phase,kg=kg,enthalpy=enthalpy,wetness=wetness,fuelKg=fuelKg,electricalJ=electricalJ,position=Body.position,rotation=Body.rotation,velocity=Body.linearVelocity,angularVelocity=Body.angularVelocity,radius=radius,burning=burning,bridge=bridge,fractured=fractured,kinematic=Body.isKinematic,scale=transform.localScale,sheetSize=sheetSize};
        void OnJointBreak(float force){fractured=true;if(world!=null)world.Say(kind==MatterKind.Stone?"Stone form fractured: load exceeded bond strength.":"Ice fracture: physical support exceeded bond strength. Thaw and refreeze to repair.");}
        void OnCollisionEnter(Collision collision){if(world!=null&&collision.contactCount>0)world.Contact(this,collision.GetContact(0).point,collision.relativeVelocity.magnitude);if(world!=null&&collision.contactCount>0)world.ElectricalContact(this,collision.collider,collision.GetContact(0).point);if(world!=null&&collision.contactCount>0)world.LaunchContact(this,collision.collider,collision.GetContact(0).point,collision.GetContact(0).normal);}
        void OnCollisionStay(Collision collision){if(world!=null&&Controlled&&collision.contactCount>0)world.LaunchContact(this,collision.collider,collision.GetContact(0).point,collision.GetContact(0).normal);}
    }
}
