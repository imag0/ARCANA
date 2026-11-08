using UnityEngine;

namespace Arcana
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        public LabInput input;public Transform cameraPivot;public Camera view;
        public float sensitivity=.12f,walk=4,sprint=6.5f,crouchSpeed=2,jumpHeight=1,standingHeight=1.8f,crouchHeight=1.15f;
        public bool invertY;public Vector3 spawn=new Vector3(0,.1f,-3.6f);
        public float Pitch {get;private set;}public float VerticalSpeed {get;private set;}
        public bool Grounded=>controller!=null&&controller.isGrounded;
        public bool Crouched=>controller!=null&&controller.height<standingHeight-.1f;
        CharacterController controller;
        void Awake(){controller=GetComponent<CharacterController>();sensitivity=PlayerPrefs.GetFloat("lookSensitivity",.12f);invertY=PlayerPrefs.GetInt("invertY",0)!=0;view.fieldOfView=PlayerPrefs.GetFloat("fieldOfView",75);}
        void Update()
        {
            if(input.Paused)return;
            Look(input.Read2("Look"));Tick(input.Read2("Move"),input.Held("Sprint"),input.Held("Crouch"),input.Pressed("Jump"),Time.deltaTime);
        }
        public void Look(Vector2 delta)
        {
            transform.Rotate(0,delta.x*sensitivity,0,Space.World);
            Pitch=Mathf.Clamp(Pitch+delta.y*sensitivity*(invertY?1:-1),-85,85);cameraPivot.localRotation=Quaternion.Euler(Pitch,0,0);
        }
        public bool CanStand()
        {
            // Only newly occupied upper capsule; floor contact must not prevent standing.
            var hits=Physics.OverlapCapsule(transform.position+Vector3.up*(crouchHeight-controller.radius+.03f),transform.position+Vector3.up*(standingHeight-controller.radius),controller.radius*.94f,~(1<<2),QueryTriggerInteraction.Ignore);
            foreach(var hit in hits)if(hit!=controller&&!hit.transform.IsChildOf(transform))return false;return true;
        }
        public void Tick(Vector2 move,bool fast,bool crouch,bool jump,float dt)
        {
            if(controller==null)controller=GetComponent<CharacterController>();dt=Mathf.Clamp(dt,0,.05f);
            bool lower=crouch||(Crouched&&!CanStand());float height=lower?crouchHeight:standingHeight;
            controller.height=height;controller.center=new Vector3(0,height/2,0);cameraPivot.localPosition=new Vector3(0,height-.16f,0);
            if(controller.isGrounded&&VerticalSpeed<0)VerticalSpeed=-2;
            if(jump&&controller.isGrounded&&!lower)VerticalSpeed=Mathf.Sqrt(2*20*jumpHeight);
            VerticalSpeed-=20*dt;move=Vector2.ClampMagnitude(move,1);float speed=lower?crouchSpeed:(fast?sprint:walk);
            Vector3 velocity=(transform.right*move.x+transform.forward*move.y)*speed+Vector3.up*VerticalSpeed;
            var flags=controller.Move(velocity*dt);if((flags&CollisionFlags.Above)!=0&&VerticalSpeed>0)VerticalSpeed=0;
            if(transform.position.y<-8)ResetPosition();
        }
        public void ResetPosition(){controller.enabled=false;transform.SetPositionAndRotation(spawn,Quaternion.identity);controller.enabled=true;VerticalSpeed=0;Pitch=0;cameraPivot.localRotation=Quaternion.identity;}
        public void Settings(float look,bool invert,float fov){sensitivity=look;invertY=invert;view.fieldOfView=fov;PlayerPrefs.SetFloat("lookSensitivity",look);PlayerPrefs.SetInt("invertY",invert?1:0);PlayerPrefs.SetFloat("fieldOfView",fov);PlayerPrefs.Save();}
    }
}
