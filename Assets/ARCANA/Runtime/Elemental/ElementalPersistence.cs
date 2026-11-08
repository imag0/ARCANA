using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Arcana.Elemental
{
    [Serializable] public sealed class PostState {public string id;public double energy;}
    [Serializable] public sealed class BondState {public string body,other;public Vector3 anchor,connectedAnchor;public float force,torque;}
    [Serializable] public sealed class PlaygroundSnapshot
    {
        public int version=2;public string carriedBoard;public ChargeLedger ledger;public long[] chargeBits;public Construction draft;public List<CastState> casts;public List<GuidedState> guided;
        public List<BodyState> bodies;public List<SourceState> sources;public List<PostState> receivers;public List<BondState> bonds;public List<ElementalWorld.Mote> motes;public Vector3 player;public double ambientHeat,fuelReleased;
    }
    public sealed partial class ElementalWorld
    {
        [NonSerialized] public string saveOverride;
        public string SavePath=>string.IsNullOrEmpty(saveOverride)?Path.Combine(Application.persistentDataPath,"elemental-world-v1.json"):saveOverride;
        public PlaygroundSnapshot Capture(Construction draft)=>new PlaygroundSnapshot
        {
            ledger=ledger,chargeBits=ledger.Accounts().Select(BitConverter.DoubleToInt64Bits).ToArray(),draft=draft,casts=casts,guided=guided,carriedBoard=PlaygroundFixture.Carried!=null?PlaygroundFixture.Carried.GetComponent<MatterBody>().id:null,bodies=bodies.Select(b=>b.Capture()).ToList(),sources=sources.Select(s=>new SourceState{id=s.stableId,lots=s.lots.Select(l=>l.Copy()).ToList(),position=s.transform.position,area=s.area,label=s.label,kind=s.kind,collector=s.collector}).ToList(),
            receivers=posts.Select(p=>new PostState{id=p.stableId,energy=p.receivedJ}).ToList(),bonds=bodies.SelectMany(b=>b.GetComponents<FixedJoint>().Select(j=>new BondState{body=b.id,other=j.connectedBody!=null?j.connectedBody.GetComponent<MatterBody>()?.id:null,anchor=j.anchor,connectedAnchor=j.connectedAnchor,force=j.breakForce,torque=j.breakTorque})).ToList(),motes=motes.ToList(),player=motor.transform.position,ambientHeat=ambientHeatJ,fuelReleased=fuelReleasedJ
        };
        public void SaveWorld(Construction draft)
        {
            try{string json=JsonUtility.ToJson(Capture(draft),true);ValidateSnapshot(JsonUtility.FromJson<PlaygroundSnapshot>(json));Directory.CreateDirectory(Path.GetDirectoryName(SavePath));File.WriteAllText(SavePath+".tmp",json);if(File.Exists(SavePath))File.Replace(SavePath+".tmp",SavePath,SavePath+".bak");else File.Move(SavePath+".tmp",SavePath);Say("Whole world saved: all phases, source lots, charge, active seals and velocities.");}
            catch(Exception e){Say("Save failed: "+e.Message);}
        }
        public Construction LoadWorld()
        {
            try{var result=RestoreWorld(File.ReadAllText(SavePath));Say("World restored under a controlled pause. G resumes; existing velocities are restored.");return result;}
            catch(Exception e){Say("Load rejected; current world preserved: "+e.Message);return null;}
        }
        void ValidateSnapshot(PlaygroundSnapshot snapshot)
        {
            bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);bool V(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
            if(snapshot==null||(snapshot.version!=1&&snapshot.version!=2)||snapshot.ledger==null||snapshot.sources==null||snapshot.bodies==null||snapshot.casts==null||snapshot.guided==null||snapshot.draft==null||snapshot.receivers==null||snapshot.bonds==null||snapshot.motes==null)throw new ArgumentException("Missing fields or incompatible world version");
            if(snapshot.version==1)foreach(var source in sources.Where(s=>s.introducedVersion==2&&!snapshot.sources.Any(v=>v.id==s.stableId)))snapshot.sources.Add(new SourceState{id=source.stableId,area=source.area,label=source.label,kind=source.kind,position=source.transform.position,collector=source.collector,lots=new List<MaterialLot>{new MaterialLot{origin=source.stableId,kind=source.kind,kg=source.initialKg,enthalpy=source.kind==MatterKind.Water?source.initialKg*84000:0}}});
            // JsonUtility's decimal double parser can move by one ULP. Preserve the
            // original IEEE bits too, while checking the readable ledger agrees.
            snapshot.ledger.RestoreExact(snapshot.chargeBits);
            if(snapshot.bodies.Count>BodyLimit||snapshot.casts.Count(c=>c.active)>CastLimit)throw new ArgumentException("Snapshot exceeds simulation capacity");
            var accounts=new[]{snapshot.ledger.initial,snapshot.ledger.external,snapshot.ledger.remaining,snapshot.ledger.drive,snapshot.ledger.heat,snapshot.ledger.cooling,snapshot.ledger.overhead,snapshot.ledger.loss,snapshot.ledger.discharge,snapshot.ledger.light};
            if(accounts.Any(x=>!Finite(x)||x<0)||!Finite(snapshot.ledger.Residual)||Math.Abs(snapshot.ledger.Residual)>1e-3)throw new ArgumentException("Invalid complete charge ledger");
            var ids=new HashSet<string>();foreach(var b in snapshot.bodies)if(string.IsNullOrEmpty(b.id)||!ids.Add(b.id)||b.kg<=0||!Finite(b.kg)||!Finite(b.enthalpy)||!Finite(b.electricalJ)||b.electricalJ<0||!Finite(b.wetness)||!Finite(b.fuelKg)||!Finite(b.radius)||b.radius<=0||!V(b.position)||!V(b.velocity)||!V(b.angularVelocity)||!V(b.scale)||!V(b.sheetSize)||!Finite(b.rotation.x)||!Finite(b.rotation.y)||!Finite(b.rotation.z)||!Finite(b.rotation.w))throw new ArgumentException("Invalid or duplicate material body");
            if(fixtures.Keys.Any(id=>!ids.Contains(id)))throw new ArgumentException("Snapshot omits an original physical fixture");
            ids.Clear();foreach(var source in snapshot.sources)
            {if(string.IsNullOrEmpty(source.id)||!ids.Add(source.id)||source.lots==null||!V(source.position)||Source(source.id)==null&&!source.id.StartsWith("pool-"))throw new ArgumentException("Unknown or duplicate material source");foreach(var lot in source.lots)if(string.IsNullOrEmpty(lot.origin)||lot.kg<0||!Finite(lot.kg)||!Finite(lot.enthalpy))throw new ArgumentException("Invalid material lot");}
            if(sources.Any(s=>s.initialKg>0&&!ids.Contains(s.stableId)))throw new ArgumentException("Snapshot omits an original finite source");
            ids.Clear();foreach(var cast in snapshot.casts){if(!ids.Add(cast.id)||cast.channels==null||cast.drawing==null||!Finite(cast.elapsed)||cast.elapsed<0)throw new ArgumentException("Invalid cast state");if(cast.launches==null||cast.launches.Any(l=>!Finite(l.elapsed)||l.elapsed<0||!Finite(l.distance)||l.distance<0||!Finite(l.speed)||l.speed<0||!Finite(l.stable)||!V(l.direction)||!V(l.center)||!V(l.offset)||!V(l.referenceCenter)||!V(l.impactPoint)))throw new ArgumentException("Invalid assembly or launch state");}
            foreach(var guide in snapshot.guided)if(!snapshot.bodies.Any(b=>b.id==guide.bodyId)||!snapshot.casts.Any(c=>c.id==guide.castId&&guide.channel>=0&&guide.channel<c.channels.Count)||!Finite(guide.distance))throw new ArgumentException("Orphaned material controller");
            foreach(var bond in snapshot.bonds)if(!snapshot.bodies.Any(b=>b.id==bond.body)||!string.IsNullOrEmpty(bond.other)&&!snapshot.bodies.Any(b=>b.id==bond.other))throw new ArgumentException("Orphaned ice bond");
            foreach(var mote in snapshot.motes)if(!V(mote.p)||!V(mote.v)||!Finite(mote.life)||!Finite(mote.size)||!Finite(mote.heat)||mote.heat<0)throw new ArgumentException("Invalid hot-gas packet");
            foreach(var post in snapshot.receivers)if(!posts.Any(p=>p.stableId==post.id)||!Finite(post.energy)||post.energy<0)throw new ArgumentException("Invalid receiver state");
            if(!V(snapshot.player)||!Finite(snapshot.ambientHeat)||!Finite(snapshot.fuelReleased))throw new ArgumentException("Invalid world state");
        }
        public Construction RestoreWorld(string json)
        {
            if(string.IsNullOrWhiteSpace(json)||!json.Contains("\"version\"")||!json.Contains("\"sources\""))throw new ArgumentException("Malformed snapshot");var snapshot=JsonUtility.FromJson<PlaygroundSnapshot>(json);RestoreLegacyLaunchFrames(snapshot);ValidateSnapshot(snapshot);
            // Validate first. Then replace, never merge, the entire authoritative world under pause.
            input.SetPaused(true);if(PlaygroundFixture.Carried!=null)PlaygroundFixture.Carried.Release();foreach(var body in bodies.ToArray())Retire(body);
            foreach(var source in sources)source.lots.Clear();
            foreach(var saved in snapshot.sources)
            {
                var source=Source(saved.id);if(source==null){var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Restored collection pool";go.transform.position=saved.position;go.transform.localScale=new Vector3(.5f,.015f,.5f);go.GetComponent<Collider>().isTrigger=true;go.GetComponent<Renderer>().sharedMaterial=bodyMaterial;source=go.AddComponent<MaterialSource>();source.stableId=saved.id;source.label=saved.label;source.kind=saved.kind;source.collector=saved.collector;source.initialKg=0;source.area=saved.area;sources.Add(source);}source.lots=saved.lots;
            }
            ledger=snapshot.ledger;casts=snapshot.casts;guided=new List<GuidedState>();ambientHeatJ=snapshot.ambientHeat;fuelReleasedJ=snapshot.fuelReleased;
            foreach(var state in snapshot.bodies)
            {
                RestoreBody(state);
            }
            guided=snapshot.guided;foreach(var g in guided){var b=bodies.Find(v=>v.id==g.bodyId);var c=casts.Find(v=>v.id==g.castId);FluidVolumeContacts(b,c,g.channel);}motes.Clear();motes.AddRange(snapshot.motes);foreach(var bond in snapshot.bonds){var body=bodies.Find(b=>b.id==bond.body);var joint=body.gameObject.AddComponent<FixedJoint>();joint.autoConfigureConnectedAnchor=false;joint.connectedBody=string.IsNullOrEmpty(bond.other)?null:bodies.Find(b=>b.id==bond.other).Body;joint.anchor=bond.anchor;joint.connectedAnchor=bond.connectedAnchor;joint.breakForce=bond.force;joint.breakTorque=bond.torque;body.bridge=true;}foreach(var saved in snapshot.receivers){var post=posts.Find(p=>p.stableId==saved.id);if(post!=null)post.receivedJ=saved.energy;}
            var cc=motor.GetComponent<CharacterController>();cc.enabled=false;motor.transform.position=snapshot.player;cc.enabled=true;if(!string.IsNullOrEmpty(snapshot.carriedBoard))bodies.Find(b=>b.id==snapshot.carriedBoard)?.GetComponent<PlaygroundFixture>()?.Carry(motor);Physics.SyncTransforms();return snapshot.draft;
        }
        public void ResetArea(string area)
        {
            if(PlaygroundFixture.Carried!=null)PlaygroundFixture.Carried.Release();
            if(string.IsNullOrEmpty(area))area=sources.OrderBy(s=>Vector3.Distance(s.Position,motor.transform.position)).FirstOrDefault()?.area;
            var originals=sources.Where(s=>s.initialKg>0&&(area=="all"||s.area==area)).ToArray();var origins=new HashSet<string>(originals.Select(s=>s.stableId));
            foreach(var cast in casts.Where(c=>c.active&&(area=="all"||c.channels.Any(ch=>origins.Contains(ch.sourceId)||Source(ch.sourceId)?.lots.Any(l=>origins.Contains(l.origin))==true||sources.Where(s=>s.initialKg>0).OrderBy(s=>Vector3.Distance(s.Position,ch.anchor)).FirstOrDefault()?.area==area))).ToArray())Cancel(cast.id);
            foreach(var body in bodies.Where(b=>origins.Contains(b.origin)).ToArray())Retire(body);
            foreach(var source in sources)source.lots.RemoveAll(l=>origins.Contains(l.origin));
            foreach(var source in originals)source.lots.Add(new MaterialLot{origin=source.stableId,kind=source.kind,kg=source.initialKg,enthalpy=source.kind==MatterKind.Water?source.initialKg*84000:0});
            foreach(var initial in fixtureStates.Values.Where(s=>area=="all"||fixtureAreas[s.id]==area)){var fixture=fixtures[initial.id];foreach(var joint in fixture.GetComponents<FixedJoint>())Destroy(joint);RestoreBody(initial);}
            foreach(var post in posts.Where(p=>area=="all"||sources.Where(s=>s.initialKg>0).OrderBy(s=>Vector3.Distance(s.Position,p.transform.position)).FirstOrDefault()?.area==area))post.receivedJ=0;
            // A local reset removes local gas, while accounting for its remaining
            // heat. Unrelated ongoing experiments retain their hot packets.
            foreach(var mote in motes.Where(m=>area=="all"||sources.Where(s=>s.initialKg>0).OrderBy(s=>Vector3.Distance(s.Position,m.p)).FirstOrDefault()?.area==area).ToArray()){ambientHeatJ+=mote.heat;motes.Remove(mote);}settled.Clear();
            if(area=="all")
            {
                Cancel();foreach(var body in bodies.Where(b=>!fixtures.ContainsKey(b.id)).ToArray())Retire(body);
                ledger=new ChargeLedger();casts.Clear();guided.Clear();foreach(var post in posts)post.receivedJ=0;ambientHeatJ=0;fuelReleasedJ=0;
            }
            Say("Reset "+area+": reclaimed its material from every reservoir and released body; no duplicate resource refill.");
        }
    }
}
