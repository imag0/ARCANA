using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Arcana.Elemental;
namespace Arcana.Tests
{
    public sealed class NestedSealTests
    {
        static List<Vector2> Circle(Vector2 center,float radius){var p=new List<Vector2>();for(int i=0;i<=64;i++)p.Add(center+new Vector2(Mathf.Cos(i*Mathf.PI/32),Mathf.Sin(i*Mathf.PI/32))*radius);return p;}
        static Construction Drawing(){var c=new Construction();var e=c.Add(Rune.Water,Vector2.zero);var a=c.Add(Rune.Assemble,new Vector2(.2f,0));var l=c.Add(Rune.Launch,new Vector2(.4f,0));c.Connect(e.id,a.id);c.Connect(a.id,l.id);c.seal=Circle(Vector2.zero,1);c.innerSeals.Add(new SealRegion{id=1,closed=true,points=Circle(Vector2.zero,.6f)});return c;}
        [Test] public void NestedContoursAcceptMultipleDepths(){var c=Drawing();c.innerSeals.Add(new SealRegion{id=2,closed=true,points=Circle(Vector2.zero,.1f)});Assert.That(DrawnGeometry.NestedSealsValid(c,out var why),Is.True,why);Assert.That(GlyphPlan.Compile(c).valid,Is.True);}
        [Test] public void CrossingChildBoundariesAreRejected(){var c=Drawing();c.innerSeals.Add(new SealRegion{id=2,closed=true,points=Circle(new Vector2(.5f,0),.3f)});Assert.That(DrawnGeometry.NestedSealsValid(c,out _),Is.False);}
        [Test] public void EscapingChildIsRejected(){var c=Drawing();c.innerSeals[0].points=Circle(new Vector2(.8f,0),.6f);Assert.That(DrawnGeometry.NestedSealsValid(c,out _),Is.False);}
        [Test] public void EmptyOrOpenChildCannotCommit(){var c=Drawing();c.innerSeals[0].closed=false;Assert.That(DrawnGeometry.NestedSealsValid(c,out _),Is.False);c.innerSeals[0].closed=true;c.innerSeals[0].points=Circle(new Vector2(-.7f,0),.1f);Assert.That(DrawnGeometry.NestedSealsValid(c,out _),Is.False);}
        [Test] public void NestedRegionsAndViewportSurviveCopy(){var c=Drawing();c.canvasZoom=3.1f;c.canvasPan=new Vector2(2,-4);var copy=c.Copy();Assert.That(copy.canvasZoom,Is.EqualTo(c.canvasZoom));Assert.That(copy.canvasPan,Is.EqualTo(c.canvasPan));Assert.That(copy.innerSeals[0].points,Is.EqualTo(c.innerSeals[0].points));Assert.That(DrawnGeometry.NestedSealsValid(copy,out _),Is.True);}
    }
}
