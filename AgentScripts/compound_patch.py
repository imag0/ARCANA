from pathlib import Path
p=Path('Assets/ARCANA/Runtime/Elemental/ElementalWorld.cs')
s=p.read_text()
changes=[
('explicitPower;public int formNode;public int combine=-1;', 'explicitPower,assemble,volume;public int formNode;public int combine=-1,launchGroup=-1;public float launchRange=40,requiredMass;'),
('public float elapsed,emitClock;public bool active=true;', 'public List<LaunchState> launches=new List<LaunchState>();public float elapsed,emitClock;public bool active=true;'),
('case Rune.End:channel.end=true;break;', 'case Rune.End:channel.end=true;break;\n                        case Rune.Assemble:channel.assemble=true;break;\n                        case Rune.Launch:channel.assemble=true;channel.launchGroup=node.id;channel.launchRange=node.width;break;'),
('var outline=drawing.Sketch(node);channel.cells.Clear();', 'var outline=drawing.Sketch(node);channel.cells.Clear();\n            if(node.fillMode==3){BuildVolumeCells(drawing,node,channel,outline);return;}'),
('var source=Source(channel.sourceId);if(source==null){reason=', 'if(channel.requiredMass>channel.amount+.01f){reason="Volume needs "+channel.requiredMass.ToString("F1")+" kg. Increase GATHER mass or reduce the drawn profile / scale.";return false;}\n                    var source=Source(channel.sourceId);if(source==null){reason='),
('drawing.sealClosed=true;if(!Validate', 'if(!DrawnGeometry.NestedSealsValid(drawing,out reason)){Say("Dormant: "+reason);return false;}\n            drawing.sealClosed=true;if(!Validate'),
('if(!cast.active)continue;cast.elapsed+=dt;', 'if(!cast.active)continue;cast.elapsed+=dt;\n                UpdateLaunches(cast,dt);'),
('if(c.form&&!c.direct&&c.spin==0&&guide.formStage<2)', 'bool waiting=WaitingForAssembly(cast,c);float motionTime=MotionTime(cast,c);\n            if(c.form&&!c.direct&&(c.spin==0||waiting)&&guide.formStage<2)'),
('            if(c.direct)\n', '            if(c.direct&&!waiting)\n'),
('            if(c.spin!=0)\n', '            if(c.spin!=0&&!waiting)\n'),
('Quaternion.AngleAxis(cast.elapsed*c.spin', 'Quaternion.AngleAxis(motionTime*c.spin'),
('            if(!c.direct&&!c.hold', '            if(c.spin==0||waiting||c.bodySpin)target+=LaunchOffset(cast,c);\n            velocity+=LaunchVelocity(cast,c);\n            if(!c.direct&&!c.hold'),
('var desiredRotation=c.spin!=0&&!c.bodySpin?', 'var desiredRotation=c.spin!=0&&!c.bodySpin&&!waiting?')]
for a,b in changes:
 assert a in s,a
 s=s.replace(a,b)
p.write_text(s)
p=Path('Assets/ARCANA/Runtime/Elemental/ElementalEffects.cs');s=p.read_text().replace('return p;','return p+LaunchOffset(cast,c);',1).replace('            return offset;','            return offset+LaunchOffset(cast,c);',1);p.write_text(s)
p=Path('Assets/ARCANA/Runtime/Elemental/GlyphModel.cs');s=p.read_text().replace('node.width=40;node.flowSpeed=12;', 'node.width=40;');p.write_text(s)
