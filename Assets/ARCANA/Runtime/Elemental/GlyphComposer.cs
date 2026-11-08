using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Arcana.Elemental
{
    public sealed partial class GlyphNotebook
    {
        GameObject composer;TMP_Text composerTitle;int composerNode=-1;string composerDraft;
        readonly Dictionary<string,TMP_InputField> fields=new Dictionary<string,TMP_InputField>();
        static string Number(float v)=>v.ToString("0.###",CultureInfo.InvariantCulture);
        static string Vector(Vector3 v)=>Number(v.x)+", "+Number(v.y)+", "+Number(v.z);
        void BuildComposerUI()
        {
            Button("SETTINGS / HELP",notebook.transform,1555,-30,285,()=>{composer.SetActive(!composer.activeSelf);details.gameObject.SetActive(!composer.activeSelf);PopulateComposer();});
            Button("DEPTH PROFILE",notebook.transform,1260,-88,190,()=>SelectLayer(InkLayer.Depth));
            Button("FILL / OUTLINE",notebook.transform,1460,-88,190,()=>{if(selected?.rune!=Rune.Form||draft.committed)return;Remember();selected.fillMode=selected.fillMode==2?3:selected.fillMode==3?1:2;LastInterpretation=selected.fillMode==3?"VOLUME: revolve a closed profile around its vertical axis. Draw a circle for a ball; material mass must cover the volume.":selected.fillMode==2?"Filled polygon: close the shape. Mass and area determine thickness.":"Outline: material follows the full drawn stroke, including its depth profile.";});
            Button("SEAL GUIDE",notebook.transform,1660,-88,190,()=>{if(draft.committed)return;Remember();activeSeal=0;draft.seal.Clear();for(int i=0;i<61;i++){float a=(20+i*5.3f)*Mathf.Deg2Rad;draft.seal.Add(new Vector2(Mathf.Cos(a)*.93f,Mathf.Sin(a)*.88f));}SelectLayer(InkLayer.Seal);board.SetVerticesDirty();});
            Button("NEW INNER SEAL",notebook.transform,1260,-890,285,NewInnerSeal);
            Button("RESET PAGE VIEW",notebook.transform,1555,-890,285,()=>board.ResetView());
            Text("Canvas navigation",notebook.transform,new Vector2(330,-892),new Vector2(900,30),18,"Wheel: zoom to pointer · Middle/right drag or Space+drag: pan · Inner seals stay dormant");
            composer=Panel("Continuous construction settings",notebook.transform,new Vector2(0,1),new Vector2(1260,-130),new Vector2(620,510),Palette.Ink);
            composerTitle=Text("Settings title",composer.transform,Vector2.zero,new Vector2(620,45),20,"Select a socket. Zero force / power / time inherits dots / bars.");
            string[] labels={"Amount kg / radius m","Force N (0=auto)","Power kW (0=auto)","Time s (0=auto)","Spin rad/s","Axis x, y, z","Sketch scale ×","Sketch rotation ° x,y,z","Sketch offset m x,y,z","Depth profile m","World scale m","World height m","World speed m/s","World origin x,y,z","Launch speed m/s"};
            for(int i=0;i<labels.Length;i++)
            {
                string label=labels[i];float x=(i%2)*310,y=-50-(i/2)*47;
                Text(label,composer.transform,new Vector2(x,y),new Vector2(300,23),16,label);
                var box=Panel(label+" input",composer.transform,new Vector2(0,1),new Vector2(x,y-24),new Vector2(295,27),new Color(.13f,.23f,.27f));
                var value=Text("Value",box.transform,new Vector2(6,-2),new Vector2(280,24),17,"");
                var inputField=box.AddComponent<ComposerInputField>();inputField.textViewport=box.GetComponent<RectTransform>();inputField.textComponent=value;inputField.targetGraphic=box.GetComponent<UnityEngine.UI.Image>();inputField.lineType=TMP_InputField.LineType.SingleLine;inputField.characterLimit=60;fields.Add(label,inputField);
            }
            Button("APPLY SETTINGS",composer.transform,0,-435,300,()=>ApplyComposer());
            Button("PATH SOURCE / WORLD",composer.transform,310,-435,300,()=>{if(selected?.rune==Rune.Direct&&!draft.committed){Remember();selected.worldPath=!selected.worldPath;LastInterpretation=selected.worldPath?"Path uses the world drawing frame.":"Path starts at the material source.";}});
            Text("Geometry hint",composer.transform,new Vector2(0,-477),new Vector2(610,32),16,"FORM cycles outline / area / volume. LAUNCH uses Launch speed m/s & its range.");composer.SetActive(false);
        }
        void RefreshComposer()
        {
            if(composer==null||!composer.activeSelf)return;
            if(composerNode!=(selected?.id??0)||composerDraft!=draft.id)PopulateComposer();
        }
        void PopulateComposer()
        {
            if(composer==null)return;composerNode=selected?.id??0;composerDraft=draft.id;
            var n=selected??new GlyphNode();composerTitle.text=(selected==null?"WORLD SETTINGS":selected.rune+" #"+selected.id+" · "+(selected.fillMode==2?"FILLED":selected.fillMode==3?"REVOLVED VOLUME":"OUTLINE / LEGACY"))+"\n0 = inherited force/power/time. LAUNCH amount = range.";
            string[] values={Number(n.width),Number(n.forceN),Number(n.powerW/1000),Number(n.seconds),Number(n.spin*2),Vector(n.axis),Number(n.geometryScale),Vector(n.geometryEuler),Vector(n.geometryOffset),Number(n.profileDepth),Number(draft.scale),Number(draft.height),Number(draft.speed),Vector(draft.origin),Number(draft.launchSpeed)};
            int i=0;foreach(var field in fields.Values){field.SetTextWithoutNotify(values[i++]);field.interactable=!draft.committed;}
        }
        public bool ApplyComposer()
        {
            if(draft.committed)return false;
            try
            {
                float F(string key){if(!float.TryParse(fields[key].text,NumberStyles.Float,CultureInfo.InvariantCulture,out float v)||!float.IsFinite(v))throw new ArgumentException(key+": enter a finite number.");return v;}
                Vector3 V(string key){var parts=fields[key].text.Split(',');if(parts.Length!=3)throw new ArgumentException(key+": enter x, y, z separated by commas.");var v=parts.Select(p=>float.Parse(p.Trim(),CultureInfo.InvariantCulture)).ToArray();if(v.Any(f=>!float.IsFinite(f)))throw new ArgumentException(key+": enter finite coordinates.");return new Vector3(v[0],v[1],v[2]);}
                var candidate=draft.Copy();var n=selected==null?new GlyphNode():candidate.nodes.Find(v=>v.id==selected.id);
                n.width=F("Amount kg / radius m");n.forceN=F("Force N (0=auto)");n.powerW=F("Power kW (0=auto)")*1000;n.seconds=F("Time s (0=auto)");n.spin=F("Spin rad/s")/2;n.axis=V("Axis x, y, z");n.geometryScale=F("Sketch scale ×");n.geometryEuler=V("Sketch rotation ° x,y,z");n.geometryOffset=V("Sketch offset m x,y,z");n.profileDepth=F("Depth profile m");
                candidate.launchSpeed=F("Launch speed m/s");if(candidate.launchSpeed<1||candidate.launchSpeed>60)throw new ArgumentException("Launch speed must be 1–60 m/s.");
                candidate.scale=F("World scale m");candidate.height=F("World height m");candidate.speed=F("World speed m/s");candidate.origin=V("World origin x,y,z");
                if(n.width<=0||n.geometryScale<=0||n.forceN<0||n.forceN>1000000||n.powerW<0||n.powerW>100000000||n.seconds<0||n.seconds>3600||Mathf.Abs(n.spin)>30||candidate.scale<.1f||candidate.scale>100||candidate.speed<.1f||candidate.speed>60)throw new ArgumentException("Outside simulation ranges: size 0.1–100 m, speed 0.1–60 m/s, force 0–1 MN, power 0–100 MW, time 0–3600 s, spin ±60 rad/s.");
                if(n.rune==Rune.Spin&&n.axis.sqrMagnitude<1e-6)throw new ArgumentException("Spin axis cannot be zero.");
                Remember();draft=candidate;if(selected!=null)selected=n;board.SetVerticesDirty();LastInterpretation="Settings applied. Geometry and force budgets update from these values.";PopulateComposer();return true;
            }
            catch(Exception e){LastInterpretation="Settings unchanged: "+e.Message;return false;}
        }
    }
}
