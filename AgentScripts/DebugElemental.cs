using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Arcana.Elemental;
public static class DebugElemental
{
    public static async Task<string> Heat()
    {
        var world=UnityEngine.Object.FindAnyObjectByType<ElementalWorld>();world.ResetArea("all");world.input.SetPaused(false);
        // Reproduce reuse after snapshot/reset, retaining physical fixture shapes.
        var saved=System.IO.File.ReadAllText("E:/_Dev/ARCANA/captures/elemental/player-run-2/bridge-world.json");world.RestoreWorld(saved);world.ResetArea("all");world.input.SetPaused(false);
        var b=world.Spawn(world.Source("thermal-water").Withdraw(.5)[0],new Vector3(15,2,-20));b.Body.constraints=RigidbodyConstraints.FreezePosition;
        var c=new Construction{origin=new Vector3(15,0,-22),target=b.Body.position};var fire=c.Add(Rune.Fire,new Vector2(-.5f,0));fire.intensity=3;fire.duration=3;var hold=c.Add(Rune.Hold,new Vector2(.5f,0));hold.duration=3;c.Connect(fire.id,hold.id);for(int i=0;i<=96;i++){float a=i*Mathf.PI/48;c.seal.Add(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.95f);}world.CloseSeal(c);
        var log="";for(int i=0;i<10;i++){await Task.Delay(1000);log+=$"{i} {b.id} active {b.gameObject.activeSelf} member {world.bodies.Contains(b)} kg {b.kg} phase {b.phase} H {b.enthalpy} p {b.Body.position} sheet {b.sheetSize} bounds {b.GetComponent<Collider>().bounds}\n";}
        log+="Pools: "+string.Join("; ",world.sources.Where(s=>s.collector).Select(s=>s.Position+" "+s.Available));world.input.SetPaused(true);return log;
    }
}
