using UnityEngine;
namespace Arcana.Elemental
{
    public sealed class PlaygroundFixture : MonoBehaviour
    {
        public string area,example;public bool reset,portable;public string help;
        public static PlaygroundFixture Carried;FirstPersonMotor carrier;MatterBody body;
        void Start()
        {
            if(!portable)return;var rb=gameObject.AddComponent<Rigidbody>();rb.mass=3;body=gameObject.AddComponent<MatterBody>();body.id="portable-board-"+transform.GetSiblingIndex();body.origin="fixture:"+body.id;body.kind=MatterKind.Fuel;body.kg=3;body.fuelKg=2.1;body.enthalpy=48000;body.radius=1.2f;rb.isKinematic=true;
            FindAnyObjectByType<ElementalWorld>().RegisterFixture(body,"court");
        }
        public void Carry(FirstPersonMotor player){if(!portable||body==null)return;if(Carried!=null)Carried.Release();Carried=this;carrier=player;body.Body.isKinematic=false;body.Refresh();}
        public void Release(){carrier=null;if(Carried==this)Carried=null;}
        void OnDestroy(){if(Carried==this)Carried=null;}
        void FixedUpdate(){if(carrier==null||body==null)return;var target=carrier.view.transform.position+carrier.view.transform.forward*2;var force=body.Body.mass*((target-body.Body.position)*40-body.Body.linearVelocity*12-Physics.gravity);body.Body.AddForce(Vector3.ClampMagnitude(force,300));}
        public void Use(GlyphNotebook notebook)
        {
            if(reset){notebook.world.ResetArea(area);return;}
            if(portable){notebook.draft.origin=transform.position;notebook.draft.forward=Vector3.Dot(notebook.world.motor.transform.position-transform.position,transform.forward)>=0?transform.forward:-transform.forward;notebook.draft.up=transform.up;notebook.draft.vertical=true;notebook.draft.height=0;notebook.draft.scale=1;notebook.draft.drawingSurfaceId=body.id;notebook.world.Say("Drawing frame placed on portable board. F carries/releases the board; E writes.");}
            if(!string.IsNullOrEmpty(example)){notebook.Example(example);notebook.Show(true);}else{notebook.world.Say(help);notebook.Show(true);}
        }
    }
}
