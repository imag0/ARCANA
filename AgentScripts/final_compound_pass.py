from pathlib import Path
p=Path('Assets/ARCANA/Runtime/Elemental/MaterialAppearance.cs');s=p.read_text().replace('Face(Vector3.up,ring[(i+1)%4],ring[i],1);Face(Vector3.down,ring[i],ring[(i+1)%4],1);', 'Face(Vector3.up,ring[i],ring[(i+1)%4],1);Face(Vector3.down,ring[(i+1)%4],ring[i],1);');p.write_text(s)
p=Path('Assets/ARCANA/Runtime/Elemental/CompoundExecution.cs');s=p.read_text().replace('const int n=4;', 'const int n=5;');p.write_text(s)
p=Path('Assets/ARCANA/Runtime/Elemental/FluidSurface.cs');s=p.read_text().replace('if(q<18)density+=Mathf.Exp(-q*.6f);', 'if(q<25)density+=Mathf.Exp(-q*.35f);').replace('field[index]=.8f-density;', 'field[index]=1.5f-density;');p.write_text(s)
p=Path('Assets/ARCANA/Runtime/Elemental/CompoundAcceptance.cs');s=p.read_text().replace('mass.width=8000;', 'mass.width=9500;').replace('world.input.SetPaused(true);var focus=', 'var focus=').replace('yield return Capture("compound-closeup");world.input.SetPaused(false);', 'yield return Capture("compound-closeup");');p.write_text(s)
p=Path('Assets/ARCANA/Runtime/Elemental/GlyphNotebook.cs');s=p.read_text().replace('Button("FREEHAND MODE",', 'Button("COMPOUND STARTER",notebook.transform,24,-755,265,LoadCompoundStudy);\n            Button("FREEHAND MODE",');p.write_text(s)
p=Path('Assets/ARCANA/Runtime/Elemental/NestedNotebook.cs');s=p.read_text().replace('        public void NewInnerSeal()', '''        void LoadCompoundStudy()
        {
            var asset=Resources.Load<TextAsset>("CompoundStudy");if(asset==null){LastInterpretation="Compound study data is missing.";return;}
            Remember();draft=JsonUtility.FromJson<Construction>(asset.text);draft.id=System.Guid.NewGuid().ToString("N");draft.committed=false;activeSeal=0;selected=null;glyphInk.Clear();layer=InkLayer.Seal;board.ResetView();
            LastInterpretation="Editable compound study: two closed inner regions, one open outer seal. Join the outer gap, then aim while it assembles. All sockets and sketches are editable.";
        }
        public void NewInnerSeal()''');p.write_text(s)
