using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Arcana.Elemental;
namespace Arcana.Tests
{
    public sealed class SealJoiningTests
    {
        [TestCase(false,false)][TestCase(false,true)][TestCase(true,false)][TestCase(true,true)]
        public void ClosureWorksFromEitherEndInEitherDirection(bool reverseContour,bool reverseStroke)
        {
            var contour=new List<Vector2>{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)};var stroke=new List<Vector2>{new Vector2(-1,1),new Vector2(-1,0),new Vector2(-1,-1)};
            if(reverseContour)contour.Reverse();if(reverseStroke)stroke.Reverse();Assert.That(DrawnGeometry.JoinSeal(contour,stroke),Is.True);DrawnGeometry.TrimClosureTail(contour);Assert.That(DrawnGeometry.ClosedSeal(contour,new[]{new GlyphNode{position=Vector2.zero}},out var why),Is.True,why);
        }
        [Test] public void TinyClosureOvershootIsTrimmedWithoutRemovingEarlierCrossings()
        {
            var contour=new List<Vector2>{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1),new Vector2(-1,-1),new Vector2(-.9f,-1)};DrawnGeometry.TrimClosureTail(contour);Assert.That(contour.Last(),Is.EqualTo(contour.First()));
            var crossed=new List<Vector2>{new Vector2(-1,-1),new Vector2(1,1),new Vector2(-1,1),new Vector2(1,-1),new Vector2(-1,-1),new Vector2(-.9f,-1)};DrawnGeometry.TrimClosureTail(crossed);Assert.That(DrawnGeometry.ClosedSeal(crossed,new[]{new GlyphNode{position=Vector2.zero}},out _),Is.False);
        }
        [Test] public void MissingInnerSealMessageIdentifiesTheRegion()
        {var c=new Construction();c.innerSeals.Add(new SealRegion{id=7});Assert.That(DrawnGeometry.NestedSealsValid(c,out var reason),Is.False);Assert.That(reason,Does.Contain("#7").And.Contain("open"));}
    }
}
