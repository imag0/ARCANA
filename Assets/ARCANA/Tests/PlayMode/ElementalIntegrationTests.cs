using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Arcana.Elemental;
namespace Arcana.Tests
{
    public sealed class ElementalIntegrationTests
    {
        ElementalWorld world;GlyphNotebook notebook;
        [UnitySetUp] public IEnumerator Setup(){Time.timeScale=1;yield return SceneManager.LoadSceneAsync("ElementalPlayground");yield return null;world=UnityEngine.Object.FindAnyObjectByType<ElementalWorld>();notebook=UnityEngine.Object.FindAnyObjectByType<GlyphNotebook>();world.input.SetPaused(false);yield return null;}
        [UnityTearDown] public IEnumerator Cleanup(){world.Cancel();world.input.SetPaused(true);yield return null;Time.timeScale=1;}
        static void Seal(Construction c){c.seal.Clear();for(int i=0;i<=96;i++){float a=i*Mathf.PI/48;c.seal.Add(new Vector2(Mathf.Cos(a)*.97f,Mathf.Sin(a)*.97f));}}
        [UnityTest] public IEnumerator RealReservoirCastAndReleaseConserveMaterial()
        {
            notebook.Example("Water route");var c=notebook.draft;Seal(c);var before=world.MaterialTotals();Assert.That(world.CloseSeal(c),Is.True,world.Message);yield return new WaitForSeconds(1.1f);Assert.That(world.bodies.Any(b=>b.owner==c.id),Is.True);world.Cancel(c.id);var after=world.MaterialTotals();foreach(var total in before)Assert.That(after[total.Key],Is.EqualTo(total.Value).Within(Math.Max(1e-5,total.Value*.01)));Assert.That(world.ledger.Residual,Is.Zero.Within(1e-5));
        }
        [UnityTest] public IEnumerator InvalidAndRepeatedClosuresDoNotDebit()
        {
            notebook.Example("Water route");var c=notebook.draft;Seal(c);c.nodes[0].sourceId="missing";double initial=world.ledger.remaining;Assert.That(world.CloseSeal(c),Is.False);Assert.That(world.ledger.remaining,Is.EqualTo(initial));c.nodes[0].sourceId="courtyard-water";Assert.That(world.CloseSeal(c),Is.True);double paid=world.ledger.remaining;Assert.That(world.CloseSeal(c),Is.False);Assert.That(world.ledger.remaining,Is.EqualTo(paid));yield return null;
        }
        [UnityTest] public IEnumerator WholeRequestAndActiveReservationsAreValidatedBeforeDebit()
        {
            notebook.Example("Water route");var first=notebook.draft;Seal(first);Assert.That(world.CloseSeal(first),Is.True);double remaining=world.ledger.remaining;
            notebook.NewDraft();notebook.Example("Water route");var second=notebook.draft;second.nodes.First(n=>n.rune==Rune.Gather).width=249;Seal(second);Assert.That(world.CloseSeal(second),Is.False,"250 kg tank already reserves 8 kg for the first construction");Assert.That(world.ledger.remaining,Is.EqualTo(remaining));Assert.That(world.Message,Does.Contain("unreserved"));world.Cancel(first.id);Assert.That(world.CloseSeal(second),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator ActiveSnapshotRestoresVelocitiesAndWholeLedger()
        {
            notebook.Example("Water route");Seal(notebook.draft);Assert.That(world.CloseSeal(notebook.draft),Is.True);yield return new WaitForSeconds(.8f);string json=JsonUtility.ToJson(world.Capture(notebook.draft));var before=world.bodies.ToDictionary(b=>b.id,b=>b.Body.linearVelocity);double charge=world.ledger.remaining;world.Cancel();world.RestoreWorld(json);Assert.That(world.ledger.remaining,Is.EqualTo(charge));foreach(var b in world.bodies)Assert.That(Vector3.Distance(b.Body.linearVelocity,before[b.id]),Is.LessThan(1e-5));Assert.That(world.input.Paused,Is.True);Assert.Throws<ArgumentException>(()=>world.RestoreWorld("{}"));yield return null;
        }
        [UnityTest] public IEnumerator ReleasedStoneUsesOrdinaryGravity()
        {
            var b=world.Spawn(new MaterialLot{origin="courtyard-stone",kind=MatterKind.Stone,kg=2},new Vector3(0,10,-20));b.Body.linearDamping=0;b.Body.linearVelocity=new Vector3(2,3,0);Vector3 p=b.Body.position,v=b.Body.linearVelocity;float elapsed=0;while(elapsed<.4f){yield return new WaitForFixedUpdate();elapsed+=Time.fixedDeltaTime;}var expected=p+v*elapsed+Physics.gravity*(.5f*elapsed*elapsed);Assert.That(Vector3.Distance(b.Body.position,expected),Is.LessThan(.07));Assert.That(b.Body.linearVelocity.y,Is.LessThan(v.y));
        }
        [UnityTest] public IEnumerator ThermalPhaseUsesLatentHeatAndPersistsOnRelease()
        {
            var b=world.Spawn(new MaterialLot{origin="courtyard-water",kind=MatterKind.Water,kg=1,enthalpy=84000},new Vector3(-20,3,0));b.AddHeat(-100000);Assert.That(b.phase,Is.EqualTo(MatterPhase.Liquid));b.AddHeat(-320000);Assert.That(b.phase,Is.EqualTo(MatterPhase.Ice));world.Cancel();Assert.That(b.phase,Is.EqualTo(MatterPhase.Ice));b.AddHeat(400000);Assert.That(b.phase,Is.EqualTo(MatterPhase.Liquid));b.AddHeat(3000000);Assert.That(b.phase,Is.EqualTo(MatterPhase.Steam));yield return null;
        }
    }
}
