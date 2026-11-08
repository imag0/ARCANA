using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Arcana.Elemental;
namespace Arcana.Tests
{
    public sealed class ElementalTests
    {
        [TestCase(Rune.Fire)][TestCase(Rune.Water)][TestCase(Rune.Air)][TestCase(Rune.Stone)][TestCase(Rune.Frost)][TestCase(Rune.Lightning)][TestCase(Rune.Light)]
        public void HandDrawnElementRemainsIdentifiable(Rune rune)
        {
            var template=GlyphAlphabet.Get(rune);var strokes=template.strokes.Select(path=>new InkStroke{points=DrawnGeometry.Resample(path.Select(p=>new Vector2(p.x*.083f+.24f,p.y*.083f-.15f)).ToArray(),.007f)}).ToList();
            var recognized=GlyphAlphabet.Recognize(strokes);Assert.That(recognized.rune,Is.EqualTo(rune));Assert.That(recognized.confidence,Is.GreaterThanOrEqualTo(.58f));
        }
        [Test] public void SamplingDensityDoesNotChangeRouteLength()
        {
            var corners=new[]{new Vector2(-.7f,-.2f),new Vector2(-.2f,.3f),new Vector2(.25f,.25f),new Vector2(.7f,-.3f)};
            var sparse=DrawnGeometry.Resample(corners,.02f);var dense=DrawnGeometry.Resample(DrawnGeometry.Resample(corners,.001f),.02f);
            Assert.That(Math.Abs(Length(sparse)-Length(dense))/Length(sparse),Is.LessThan(.02));Assert.That(Vector2.Distance(sparse[0],dense[0]),Is.Zero.Within(1e-5));
        }
        static float Length(List<Vector2> p){float sum=0;for(int i=1;i<p.Count;i++)sum+=Vector2.Distance(p[i-1],p[i]);return sum;}
        [Test] public void CycleAndImplicitSplitAreRejected()
        {
            var c=new Construction();var water=c.Add(Rune.Water,new Vector2(-.5f,0));var a=c.Add(Rune.Hold,Vector2.zero);var b=c.Add(Rune.Target,new Vector2(.5f,0));c.Connect(water.id,a.id);c.Connect(a.id,b.id);c.Connect(b.id,a.id);Assert.That(GlyphPlan.Compile(c).reason,Does.Contain("cycle"));c.edges.RemoveAt(2);c.Connect(water.id,b.id);Assert.That(GlyphPlan.Compile(c).valid,Is.False);
        }
        [Test] public void InternalGlyphLoopCannotBecomeOuterSeal()
        {
            var c=new Construction();c.Add(Rune.Fire,new Vector2(.6f,0));var small=DrawnGeometry.Resample(GlyphAlphabet.Get(Rune.Fire).strokes[0].Select(p=>p*.1f).ToList());Assert.That(DrawnGeometry.ClosedSeal(small,c.nodes,out _),Is.False);
        }
        [Test] public void ChargeCannotGoNegativeOrRecoverThroughWork()
        {
            var ledger=new ChargeLedger{initial=1,remaining=1};Assert.That(ledger.Pay(20000,"drive"),Is.EqualTo(10000));Assert.That(ledger.Pay(10,"heat"),Is.Zero);Assert.That(ledger.remaining,Is.Zero);Assert.That(ledger.Residual,Is.Zero.Within(1e-9));
        }
    }
}
