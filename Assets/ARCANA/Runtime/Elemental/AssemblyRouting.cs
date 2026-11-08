using System.Linq;
using UnityEngine;
namespace Arcana.Elemental
{
    // Local obstacle detours, with an explicit magical escape for trapped matter.
    public sealed class AssemblyRouting : MonoBehaviour
    {
        MatterBody matter;Vector3 sample,waypoint;float clock,stalled,detourTime,bestError;bool initialized;
        public bool Phasing{get;private set;} public static int escapeCount;
        void OnEnable(){initialized=false;clock=stalled=detourTime=0;Phasing=false;}
        void FixedUpdate(){if(matter!=null&&!matter.Controlled)Restore();}
        void OnDisable(){Restore();}
        public void Restore(){if(matter!=null&&Phasing)matter.Body.detectCollisions=true;Phasing=false;stalled=0;}
        public Vector3 Route(MatterBody body,Vector3 target,Vector3 destination,float dt)
        {
            matter=body;var p=body.Body.position;
            if(!initialized){sample=p;bestError=Vector3.Distance(p,destination);initialized=true;}
            if(Phasing)
            {
                if(Vector3.Distance(p,destination)<.02f){body.Body.MovePosition(destination);body.Body.linearVelocity=Vector3.zero;body.Body.angularVelocity=Vector3.zero;return destination;}
                body.Body.MovePosition(Vector3.MoveTowards(p,destination,6*dt));body.Body.linearVelocity=Vector3.zero;body.Body.angularVelocity=Vector3.zero;return destination;
            }
            clock+=dt;
            if(clock>=.5f)
            {
                float error=Vector3.Distance(p,destination);bool progress=error<bestError-.08f;
                bool unsettled=error>.12f||body.Body.linearVelocity.magnitude>.3f;stalled=unsettled&&!progress?stalled+clock:0;if(progress)bestError=error;
                clock=0;sample=p;
            }
            if(stalled>=4)
            {
                Phasing=true;escapeCount++;body.world.RemoveIceBonds(body);body.Body.detectCollisions=false;
                body.world.Say("A trapped material particle is phasing through obstacles to finish assembling. Collisions return when launched or released.");return destination;
            }
            // Liquid cells share one volume; avoid expensive individual detour
            // queries, but recover them if external geometry blocks assembly.
            if(body.kind==MatterKind.Water)return target;
            if(detourTime>0){detourTime-=dt;if(Vector3.Distance(p,waypoint)>.18f)return waypoint;detourTime=0;}
            var delta=target-p;if(delta.magnitude<.25f)return target;
            float radius=Mathf.Max(.06f,body.radius);
            var hits=Physics.SphereCastAll(p,radius,delta.normalized,Mathf.Min(delta.magnitude,2),~(1<<2),QueryTriggerInteraction.Ignore)
                .Where(h=>h.rigidbody!=body.Body&&h.collider.GetComponentInParent<FirstPersonMotor>()==null).OrderBy(h=>h.distance).ToArray();
            if(hits.Length==0)return target;
            var hit=hits[0];var bounds=hit.collider.bounds;float best=float.PositiveInfinity;
            foreach(var side in new[]{Vector3.up,Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
            {
                var candidate=bounds.center+Vector3.Scale(side,bounds.extents+Vector3.one*(radius+.35f));
                var leg=candidate-p;if(leg.sqrMagnitude<.01f)continue;
                if(Physics.SphereCastAll(p,radius,leg.normalized,leg.magnitude,~(1<<2),QueryTriggerInteraction.Ignore).Any(h=>h.rigidbody!=body.Body&&h.collider.GetComponentInParent<FirstPersonMotor>()==null))continue;
                float score=leg.magnitude+Vector3.Distance(candidate,target);if(score<best){best=score;waypoint=candidate;}
            }
            if(float.IsInfinity(best))return target;detourTime=1;return waypoint;
        }
    }
}
