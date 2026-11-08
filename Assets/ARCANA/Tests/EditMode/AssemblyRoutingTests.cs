using NUnit.Framework;
using UnityEngine;
using Arcana.Elemental;
namespace Arcana.Tests
{
    public class AssemblyRoutingTests
    {
        [TestCase(false)][TestCase(true)] public void TrappedParticlePhasesAfterFourSecondsAndRestoresOnRelease(bool movingNearSlot)
        {
            var worldObject=new GameObject("routing test world");var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                var world=worldObject.AddComponent<ElementalWorld>();go.AddComponent<Rigidbody>();var body=go.AddComponent<MatterBody>();body.Refresh();body.world=world;
                var router=go.AddComponent<AssemblyRouting>();var destination=movingNearSlot?new Vector3(.05f,0,0):new Vector3(5,2,0);if(movingNearSlot)body.Body.linearVelocity=Vector3.right*2;
                for(int i=0;i<39;i++)router.Route(body,destination,destination,.1f);
                Assert.That(router.Phasing,Is.False);
                for(int i=0;i<7;i++)router.Route(body,destination,destination,.1f);
                Assert.That(router.Phasing,Is.True);Assert.That(body.Body.detectCollisions,Is.False);
                body.Body.position=destination;body.Body.linearVelocity=Vector3.zero;router.Route(body,destination,destination,.1f);
                Assert.That(router.Phasing,Is.True);router.Restore();Assert.That(body.Body.detectCollisions,Is.True);
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(worldObject);}
        }
    }
}
