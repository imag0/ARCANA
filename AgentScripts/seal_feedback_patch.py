from pathlib import Path
b=Path('Unity/ARCANA/Assets/ARCANA/Runtime/Elemental')
p=b/'NestedNotebook.cs';s=p.read_text();a=s.index('        void CommitSealStroke(');s=s[:a]+'''        void CommitSealStroke(List<Vector2> points)
        {
            var contour=activeSeal==0?draft.seal:draft.innerSeals.Find(s=>s.id==activeSeal)?.points;
            if(contour==null){LastInterpretation="This inner seal was removed. Choose NEW INNER SEAL or 5 OUTER SEAL.";return;}
            if(!DrawnGeometry.JoinSeal(contour,points))
            {
                if(activeSeal==0&&draft.seal.Count>=3&&DrawnGeometry.Contains(draft.seal,points[0])){NewInnerSeal();contour=draft.innerSeals.Last().points;DrawnGeometry.JoinSeal(contour,points);}
                else{LastInterpretation="Stroke did not touch either highlighted endpoint. Join an endpoint, or CLEAR LAYER to redraw the outer seal.";SealStatus=LastInterpretation;nextFeedback=0;return;}
            }
            DrawnGeometry.TrimClosureTail(contour);
            if(activeSeal!=0)
            {
                var region=draft.innerSeals.Find(s=>s.id==activeSeal);region.closed=DrawnGeometry.ClosedContour(contour,System.Array.Empty<GlyphNode>(),.005f,out var reason);
                LastInterpretation=region.closed?"Inner seal #"+activeSeal+" closed and dormant. Choose 5 OUTER SEAL to cast the compound.":"Inner seal #"+activeSeal+": "+reason;RefreshSealFeedback(true);return;
            }
            if(DrawnGeometry.ClosedSeal(draft.seal,draft.nodes,out var outerReason))TryCastFromNotebook();else{LastInterpretation=outerReason;RefreshSealFeedback(true);}
        }
    }
}
''';p.write_text(s)
p=b/'GlyphModel.cs';s=p.read_text();s=s.replace('reason="Leave a gap in one outer contour, then join its ends.";if(points.Count<8||Vector2.Distance(points[0],points[points.Count-1])>.07f)return false;', 'reason="Draw a boundary around the sockets, then join its two highlighted endpoints.";if(points.Count<4||Vector2.Distance(points[0],points[points.Count-1])>.07f)return false;');s=s.replace('reason="All instruction sockets must lie inside the outer seal.";', 'reason=node.rune+" socket #"+node.id+" lies outside the outer seal. Enlarge or redraw the seal around every socket.";');s=s.replace('if(!inner.closed||!ClosedContour(inner.points,Array.Empty<GlyphNode>(),.005f,out reason)){reason="Close every inner seal before closing the outer seal. "+reason;return false;}', 'if(!inner.closed){reason="Inner seal #"+inner.id+" is open. Finish its boundary, or undo/remove that inner seal.";return false;}\n                if(!ClosedContour(inner.points,Array.Empty<GlyphNode>(),.005f,out reason)){reason="Inner seal #"+inner.id+": "+reason;return false;}');idx=s.index('        public static bool ClosedSeal(');s=s[:idx]+'''        public static bool JoinSeal(List<Vector2> contour,IReadOnlyList<Vector2> stroke)
        {
            if(stroke.Count<2)return false;if(contour.Count==0){contour.AddRange(stroke);return true;}
            float[] gaps={Vector2.Distance(contour[contour.Count-1],stroke[0]),Vector2.Distance(contour[contour.Count-1],stroke[stroke.Count-1]),Vector2.Distance(contour[0],stroke[stroke.Count-1]),Vector2.Distance(contour[0],stroke[0])};
            int best=0;for(int i=1;i<4;i++)if(gaps[i]<gaps[best])best=i;if(gaps[best]>.12f)return false;
            var ink=stroke.ToList();if(best==1||best==3)ink.Reverse();if(best<2)contour.AddRange(ink.Skip(1));else contour.InsertRange(0,ink.Take(ink.Count-1));return true;
        }
        public static void TrimClosureTail(List<Vector2> points)
        {
            // Permit a small overshoot after joining the start, while preserving
            // all earlier geometry for the self-intersection and enclosure checks.
            if(points.Count<4)return;float tail=0;int nearest=points.Count-1;float gap=Vector2.Distance(points[0],points[nearest]);
            for(int i=points.Count-2;i>=3;i--){tail+=Vector2.Distance(points[i],points[i+1]);if(tail>.18f)break;float d=Vector2.Distance(points[0],points[i]);if(d<gap){nearest=i;gap=d;}}
            if(gap<=.035f){if(nearest<points.Count-1)points.RemoveRange(nearest+1,points.Count-nearest-1);points[points.Count-1]=points[0];}
        }
'''+s[idx:];p.write_text(s)
p=b/'GlyphNotebook.cs';s=p.read_text().replace('if(Open){RefreshText();RefreshComposer();}', 'if(Open){RefreshText();RefreshComposer();RefreshSealFeedback();}');s=s.replace('else status.text=world.Message;', 'else{var activeCast=world.casts.LastOrDefault(c=>c.active&&c.channels.Any(ch=>ch.launchGroup>=0));status.text=activeCast!=null&&activeCast.launches.Any(l=>!l.launched&&!l.impacted)?activeCast.status:world.Message;}');s=s.replace('BuildComposerUI();notebook.SetActive(false);', 'BuildComposerUI();BuildSealFeedback();notebook.SetActive(false);');s=s.replace('region?.points.Clear();if(region!=null)region.closed=false;', 'if(region!=null)draft.innerSeals.Remove(region);activeSeal=0;LastInterpretation="Inner seal removed. Choose OUTER SEAL or NEW INNER SEAL.";');s=s.replace('var plan=GlyphPlan.Compile(draft);if(!plan.valid||draft.committed)', 'if(draft.committed){preview.positionCount=0;return;}var plan=GlyphPlan.Compile(draft);if(!plan.valid)');p.write_text(s)
p=b/'GlyphCanvas.cs';s=p.read_text().replace('foreach(var ink in c.ink)', '''if(!c.committed&&c.seal.Count>1&&Vector2.Distance(c.seal[0],c.seal[c.seal.Count-1])>.07f)foreach(var point in new[]{c.seal[0],c.seal[c.seal.Count-1]}){Line(mesh,point-Vector2.right*.025f,point+Vector2.right*.025f,Palette.Red,4);Line(mesh,point-Vector2.up*.025f,point+Vector2.up*.025f,Palette.Red,4);}
            foreach(var ink in c.ink)''');p.write_text(s)
p=Path('Unity/ARCANA/Assets/ARCANA/Resources/CompoundStudy.json');import json;c=json.loads(p.read_text());c['spellSize']=.5;p.write_text(json.dumps(c,indent=2));Path('COMPOUND_EXAMPLE.json').write_text(json.dumps(c,indent=2))
