using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Arcana.Elemental;

namespace Arcana.Tests
{
    public sealed class CompositionTests
    {
        static IEnumerable<TestCaseData> Pairs(){for(int a=1;a<=7;a++)for(int b=a;b<=7;b++)yield return new TestCaseData((Rune)a,(Rune)b);}
        [TestCaseSource(nameof(Pairs))] public void EveryElementPairCompilesWithoutRecipeLookup(Rune a,Rune b)
        {
            var c=new Construction();var first=c.Add(a,new Vector2(-.5f,.4f));var second=c.Add(b,new Vector2(-.5f,-.4f));var merge=c.Add(Rune.Combine,Vector2.zero);var form=c.Add(Rune.Form,new Vector2(.3f,0));form.sketch=new List<Vector2>{new Vector2(-.8f,.1f),new Vector2(-.2f,.6f),new Vector2(.7f,-.3f)};var hold=c.Add(Rune.Hold,new Vector2(.6f,0));c.Connect(first.id,merge.id);c.Connect(second.id,merge.id);c.Connect(merge.id,form.id);c.Connect(form.id,hold.id);
            Assert.That(GlyphPlan.Compile(c).valid,Is.True,GlyphPlan.Compile(c).reason);
        }
        [Test] public void IndependentSketchesAndDepthSurviveSerialization()
        {
            var c=new Construction{vertical=true,scale=10};var a=c.Add(Rune.Form,Vector2.zero);a.sketch=new List<Vector2>{new Vector2(-1,0),new Vector2(0,.5f),new Vector2(1,0)};a.depthProfile=new List<Vector2>{new Vector2(-1,-.5f),new Vector2(0,.8f),new Vector2(1,-.2f)};a.geometryEuler=new Vector3(27,-38,61);a.geometryOffset=new Vector3(3,6,9);
            var b=c.Add(Rune.Direct,Vector2.one);b.sketch=new List<Vector2>{Vector2.zero,Vector2.up};var before=c.Geometry(a);var copy=c.Copy();Assert.That(copy.Geometry(copy.nodes[0]),Is.EqualTo(before));Assert.That(copy.Geometry(copy.nodes[1]).Count,Is.LessThan(before.Count));
            var normal=c.Frame(a)*Vector3.forward;var depth=before.Select(p=>Vector3.Dot(p-c.Map(Vector2.zero,true)-a.geometryOffset,normal));Assert.That(depth.Max()-depth.Min(),Is.GreaterThan(4));
        }
        [Test] public void ZeroSpinAxisAndNonfiniteSettingsAreRejected()
        {
            var c=new Construction();var air=c.Add(Rune.Air,Vector2.zero);var spin=c.Add(Rune.Spin,Vector2.one);c.Connect(air.id,spin.id);spin.axis=Vector3.zero;Assert.That(GlyphPlan.Compile(c).valid,Is.False);spin.axis=new Vector3(1,2,3);air.powerW=float.NaN;Assert.That(GlyphPlan.Compile(c).valid,Is.False);
        }
    }
}
