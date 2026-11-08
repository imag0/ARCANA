from pathlib import Path
p=Path('Assets/ARCANA/Runtime/Elemental/ElementalWorld.cs');s=p.read_text().replace('body.Body.linearVelocity=Vector3.up*.2f;channel.emitted', 'FluidVolumeContacts(body,cast,ci);body.Body.linearVelocity=Vector3.up*.2f;channel.emitted');p.write_text(s)
p=Path('Assets/ARCANA/Runtime/Elemental/ElementalPersistence.cs');s=p.read_text().replace('guided=snapshot.guided;motes.Clear();', 'guided=snapshot.guided;foreach(var g in guided){var b=bodies.Find(v=>v.id==g.bodyId);var c=casts.Find(v=>v.id==g.castId);FluidVolumeContacts(b,c,g.channel);}motes.Clear();');p.write_text(s)
