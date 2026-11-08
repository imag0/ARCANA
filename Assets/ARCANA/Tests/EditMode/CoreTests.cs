using System;
using System.Linq;
using NUnit.Framework;
using Arcana.Core;
using Arcana.Mechanics;
using Arcana.Field;
using UnityEngine;

namespace Arcana.Tests
{
    public sealed class CoreTests
    {
        static Stroke Stroke(params D3[] points)=>new Stroke{radius=.03,points=points.ToList()};
        static Geometry Rod(double displacement=.008)
        {var s=Stroke(new D3(-.2,0,0),new D3(.2,0,0));var g=new Geometry();g.strokes.Add(s);g.supports.Add(new Support{surface=s.surface,strokeId=s.id,position=s.points[0]});g.supports.Add(new Support{surface=s.surface,strokeId=s.id,position=s.points[1],displacement=displacement});return g;}
        static double Determinant(double[,] m)=>m[0,0]*(m[1,1]*m[2,2]-m[1,2]*m[2,1])-m[0,1]*(m[1,0]*m[2,2]-m[1,2]*m[2,0])+m[0,2]*(m[1,0]*m[2,1]-m[1,1]*m[2,0]);
        [Test] public void TensorTraceAndFrobenius(){var t=new Theta(.2,-.1,.3,.07,-.11);var m=t.Matrix();double sum=0;for(int i=0;i<3;i++)for(int j=0;j<3;j++){Assert.That(m[i,j],Is.EqualTo(m[j,i]));sum+=m[i,j]*m[i,j];}Assert.That(t.xx+t.yy+t.zz,Is.EqualTo(0).Within(1e-15));Assert.That(Theta.Dot(t,t),Is.EqualTo(sum).Within(1e-14));}
        [Test] public void ExactMetricPositiveUnitDeterminantInverse(){var t=new Theta(.7,-.4,.3,-.2,.15);var h=t.Metric(1);var inverse=t.Metric(1,true);Assert.That(h[0,0],Is.GreaterThan(0));Assert.That(h[0,0]*h[1,1]-h[0,1]*h[1,0],Is.GreaterThan(0));Assert.That(Determinant(h),Is.EqualTo(1).Within(1e-12));for(int i=0;i<3;i++)for(int j=0;j<3;j++){double v=0;for(int k=0;k<3;k++)v+=h[i,k]*inverse[k,j];Assert.That(v,Is.EqualTo(i==j?1:0).Within(1e-12));}}
        [Test] public void ExponentialHasKnownPrincipalDirections(){var q=Theta.Q(new D3(1,0,0))*.3;double factor=2/Math.Sqrt(1+q.Norm*q.Norm);var h=q.Metric(1);Assert.That(h[0,0],Is.EqualTo(Math.Exp(factor*.2)).Within(1e-13));Assert.That(h[1,1],Is.EqualTo(Math.Exp(-factor*.1)).Within(1e-13));}
        [TestCase(0,2d/3)] [TestCase(90,-1d/3)] [TestCase(180,2d/3)] [TestCase(54.7356103,0)]
        public void QContractions(double angle,double expected){double r=angle*Math.PI/180;Assert.That(Theta.Dot(Theta.Q(new D3(1,0,0)),Theta.Q(new D3(Math.Cos(r),Math.Sin(r),0))),Is.EqualTo(expected).Within(1e-9));}
        [Test] public void ParametersRejectWrongPotentialWindow(){Assert.Throws<ArgumentException>(()=>new Model{b=4}.Validate());Assert.Throws<ArgumentException>(()=>new Model{K=0}.Validate());Assert.DoesNotThrow(()=>new Model().Validate());}
        [Test] public void ZeroSourceAndConstantDisplacement(){Assert.That(Theta.Source(default,1,50).Norm,Is.Zero);var g=Rod();foreach(var support in g.supports)support.displacement=.02;var r=Solver.Solve(g);Assert.That(r.energy,Is.Zero.Within(1e-24));Assert.That(r.maxStrain,Is.Zero.Within(1e-12));Assert.That(WFixture.Evaluate(0).shift,Is.Zero);}
        [Test] public void SignSymmetryAndSquareAmplitude(){double a=WFixture.Evaluate(.01).shift,b=WFixture.Evaluate(.02).shift,c=WFixture.Evaluate(-.02).shift;Assert.That(b/a,Is.EqualTo(4).Within(1e-12));Assert.That(c,Is.EqualTo(b).Within(1e-15));}
        [Test] public void WFiniteCylinderAndModeWeight()
        {var source=WFixture.Source();var cache=new SourceCache(new[]{source},new Model());var center=cache.At(new D3(0,.5,0),allowLine:false);var reading=WFixture.Evaluate();Assert.That(source.Energy,Is.EqualTo(1.1309733552923255e-5).Within(1e-17));Assert.That(center.value.xx*1.5,Is.EqualTo(2.1036306522215816e-6).Within(1e-13));Assert.That(reading.shift,Is.EqualTo(-1.401262439e-6).Within(1e-9));Assert.That(reading.error,Is.LessThan(1e-9));TestContext.WriteLine($"W energy={source.Energy:R}; q={center.value.xx*1.5:R}; shift={reading.shift:R}; quadrature estimate={reading.error:R}");}
        [TestCase(.3,-2.723294849e-6)] [TestCase(.8,-6.617189927e-7)] [TestCase(1,-4.356934530e-7)]
        public void DistanceBenchmark(double distance,double expected){var cache=new SourceCache(new[]{WFixture.Source()},new Model());Assert.That(cache.Measure(new D3(0,distance,0),new D3(1,0,0)).shift,Is.EqualTo(expected).Within(1e-9));}
        [Test] public void ProbeRotationChangesSign(){var cache=new SourceCache(new[]{WFixture.Source()},new Model());double x=cache.Measure(new D3(0,.5,0),new D3(1,0,0)).shift,z=cache.Measure(new D3(0,.5,0),new D3(0,0,1)).shift;Assert.That(x,Is.LessThan(0));Assert.That(z,Is.GreaterThan(0));}
        [Test] public void SingularCylinderCenterUsesIntegratedKernel()
        {var c=WFixture.Source();double low=SourceCache.RadialIntegral(c,default,new Model(),64),high=SourceCache.RadialIntegral(c,default,new Model(),128);Assert.That(Numbers.Finite(high),Is.True);Assert.That(high,Is.GreaterThan(0));Assert.That(Math.Abs(low-high)/high,Is.LessThan(.008));var inside=new SourceCache(new[]{c},new Model()).At(default);Assert.That(Numbers.Finite(inside.value.Norm),Is.True);}
        [Test] public void CoincidentProbeIsUnavailable(){var cache=new SourceCache(new[]{WFixture.Source()},new Model());Assert.That(cache.Measure(default,new D3(1,0,0)).available,Is.False);}
        [Test] public void FiniteVolumeFieldEnergyNoOmittedSelfCells(){var result=new SourceCache(new[]{WFixture.Source()},new Model()).FieldEnergy();Assert.That(Numbers.Finite(result.field),Is.True);Assert.That(result.field,Is.GreaterThan(0));Assert.That(result.field,Is.LessThan(WFixture.Source().Energy));TestContext.WriteLine($"Finite-volume Etheta={result.field:R}; error estimate={result.error:R}; resolved={result.available}");}
        [Test] public void UnanchoredRodIsPassiveWithExplicitGauge(){var g=Rod();g.supports.Clear();var r=Solver.Solve(g);Assert.That(r.valid,Is.True);Assert.That(r.energy,Is.Zero);}
        [Test] public void ScalarGraphForceBalance(){var g=Rod();g.strokes[0].points.Insert(1,new D3(0,0,0));var r=Solver.Solve(g);Assert.That(r.valid,Is.True);Assert.That(r.maxStrain,Is.EqualTo(.02).Within(1e-12));Assert.That(r.forceResidual,Is.LessThan(1e-12));Assert.That(r.energy,Is.EqualTo(WFixture.Source().Energy).Within(1e-15));}
        [Test] public void ClosureAcrossSeparateStrokesAndPeriodicity()
        {var g=new Geometry();var corners=new[]{new D3(0,0,0),new D3(1,0,0),new D3(1,1,0),new D3(0,1,0),new D3(0,0,0)};for(int i=0;i<4;i++)g.strokes.Add(Stroke(corners[i],corners[i+1]));var graph=Graph.Build(g);Assert.That(graph.components.Count,Is.EqualTo(1));Assert.That(graph.components[0].Closed,Is.True);Solver.InstallFixture(g,graph,graph.components[0],.01);var r=Solver.Solve(g);double sum=0;foreach(var e in r.graph.edges)sum+=e.strain*e.length;Assert.That(sum,Is.Zero.Within(1e-14));Assert.That(r.energy,Is.GreaterThan(0));Assert.That(r.forceResidual,Is.LessThan(1e-12));}
        [Test] public void BondedVersusInsulatedCrossing()
        {var g=new Geometry();g.strokes.Add(Stroke(new D3(-.2,0,0),new D3(.2,0,0)));g.strokes.Add(Stroke(new D3(0,-.2,0),new D3(0,.2,0)));var bonded=Graph.Build(g);Assert.That(bonded.nodes.Count,Is.EqualTo(5));Assert.That(bonded.components.Count,Is.EqualTo(1));g.strokes[1].bonded=false;var insulated=Graph.Build(g);Assert.That(insulated.nodes.Count,Is.EqualTo(4));Assert.That(insulated.components.Count,Is.EqualTo(2));}
        [Test] public void ContactRequiresSamePlaneAndPolarization()
        {var g=new Geometry();g.strokes.Add(Stroke(new D3(-.2,0,0),new D3(.2,0,0)));g.strokes.Add(Stroke(new D3(0,-.2,0),new D3(0,.2,0)));g.strokes[1].surface="another-panel";Assert.That(Graph.Build(g).components.Count,Is.EqualTo(2));g.strokes[1].surface=g.strokes[0].surface;g.strokes[1].polarization=1;Assert.That(Graph.Build(g).components.Count,Is.EqualTo(2));}
        [Test] public void CrossJunctionReferenceDisplacement()
        {var g=new Geometry();var x=Stroke(new D3(-.2,0,0),new D3(.2,0,0));var y=Stroke(new D3(0,-.2,0),new D3(0,.2,0));g.strokes.Add(x);g.strokes.Add(y);foreach(var s in g.strokes)for(int i=0;i<2;i++)g.supports.Add(new Support{surface=s.surface,strokeId=s.id,position=s.points[i],displacement=s==x&&i==1?.004:0});var r=Solver.Solve(g);var middle=r.graph.nodes.OrderBy(n=>n.position.Length).First();Assert.That(middle.u,Is.EqualTo(.001).Within(1e-12));Assert.That(r.forceResidual,Is.LessThan(1e-12));double bonded=r.energy;y.bonded=false;var insulated=Solver.Solve(g);Assert.That(bonded/insulated.energy,Is.EqualTo(1.5).Within(1e-12));}
        [Test] public void UnresolvedSurfaceExplicitlyUnsupported(){var g=Rod();g.strokes[0].supported=false;Assert.That(Solver.Solve(g).valid,Is.False);}
        [Test] public void ConflictingSupportsRejected(){var g=Rod();g.supports.Add(g.supports[0].Copy());g.supports[2].displacement=.1;Assert.That(Solver.Solve(g).valid,Is.False);}
        [Test] public void AffordablePreloadAndStrainLimit()
        {var g=Rod(.08);var ledger=new Ledger{initial=1e-6,reserve=1e-6};var r=Solver.Commit(g,ledger,out var adjustment);Assert.That(r.valid,Is.True);Assert.That(r.maxStrain,Is.LessThanOrEqualTo(.0500000001));Assert.That(r.energy,Is.LessThanOrEqualTo(1.0000001e-6));Assert.That(ledger.Residual,Is.Zero.Within(1e-15));Assert.That(adjustment,Is.Not.Empty);}
        [Test] public void EmptyReserveCannotActivate(){var g=Rod();var ledger=new Ledger{initial=0,reserve=0};var r=Solver.Commit(g,ledger,out _);Assert.That(r.energy,Is.Zero);Assert.That(ledger.reserve,Is.Zero);}
        [Test] public void NoDoubleDebitAndNoAutomaticRefund()
        {var g=Rod();var ledger=new Ledger();var r=Solver.Commit(g,ledger,out _);double reserve=ledger.reserve;Solver.Commit(g,ledger,out _);Assert.That(ledger.reserve,Is.EqualTo(reserve));g.supports.Clear();Solver.Commit(g,ledger,out _);Assert.That(ledger.reserve,Is.EqualTo(reserve));Assert.That(ledger.released,Is.EqualTo(r.energy).Within(1e-15));ledger.Wind(.001);Assert.That(ledger.Residual,Is.Zero.Within(1e-15));}
        [Test] public void CutWithRetainedSupportsRetainsStrain()
        {var g=Rod();var extra=Stroke(new D3(0,0,0),new D3(0,.3,0));g.strokes.Add(extra);double before=Solver.Solve(g).energy;g.strokes.Remove(extra);Assert.That(Solver.Solve(g).energy,Is.EqualTo(before).Within(1e-12));Assert.That(before,Is.GreaterThan(0));}
        [Test] public void SnapshotRoundTripAndValidation()
        {var g=Rod();var l=new Ledger();Solver.Commit(g,l,out _);var snapshot=new Snapshot{geometry=g,ledger=l};string json=JsonUtility.ToJson(snapshot);var round=JsonUtility.FromJson<Snapshot>(json);round.Validate();Assert.That(round.geometry.supports[1].displacement,Is.EqualTo(.008));Assert.That(round.ledger.reserve,Is.EqualTo(l.reserve));Assert.That(Solver.Solve(round.geometry).energy,Is.EqualTo(l.stored).Within(1e-15));round.ledger.reserve+=.01;Assert.Throws<ArgumentException>(()=>round.Validate());}
        [Test] public void IncompatibleSnapshotRejected(){Assert.Throws<ArgumentException>(()=>new Snapshot{version=9}.Validate());Assert.Throws<ArgumentException>(()=>new Snapshot{probeAxis=default}.Validate());}
        [Test] public void CoordinateRotationPreservesDotProductsAndHandedness()
        {var x=Coordinates.ToUnity(new D3(1,0,0));var y=Coordinates.ToUnity(new D3(0,1,0));var z=Coordinates.ToUnity(new D3(0,0,1));Assert.That(Vector3.Dot(x,y),Is.Zero);Assert.That(Vector3.Distance(Vector3.Cross(x,y),z),Is.LessThan(1e-6));var v=new D3(.2,.3,.4);Assert.That(D3.Distance(Coordinates.ToResearch(Coordinates.ToUnity(v)),v),Is.LessThan(1e-7));}
        [Test] public void ResamplingKeepsRealCorners(){var points=new[]{new D3(0,0,0),new D3(.5,0,0),new D3(1,0,0),new D3(1,1,0)};var result=DrawingTool.Simplify(points.ToList(),.008);Assert.That(result.Count,Is.EqualTo(3));Assert.That(D3.Distance(result[1],points[2]),Is.Zero);}
    }
}
