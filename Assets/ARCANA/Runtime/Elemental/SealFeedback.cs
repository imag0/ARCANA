using TMPro;
using UnityEngine;

namespace Arcana.Elemental
{
    public sealed partial class GlyphNotebook
    {
        TMP_Text sealFeedback;UnityEngine.UI.Image sealFeedbackBackground;float nextFeedback;
        public string SealStatus{get;private set;}="";public bool SealBlocked{get;private set;}
        void BuildSealFeedback()
        {
            var title=notebook.transform.Find("Title").GetComponent<TMP_Text>();title.text="ARCANA\nGLYPH NOTEBOOK";title.fontSize=24;title.rectTransform.sizeDelta=new Vector2(300,65);
            var panel=Panel("Seal readiness",notebook.transform,new Vector2(0,1),new Vector2(330,-15),new Vector2(900,65),Palette.Ink);sealFeedbackBackground=panel.GetComponent<UnityEngine.UI.Image>();
            sealFeedback=Text("Seal reason",panel.transform,new Vector2(10,-5),new Vector2(880,58),20);sealFeedback.enableAutoSizing=true;sealFeedback.fontSizeMin=17;sealFeedback.fontSizeMax=20;
            Button("SMALLER",notebook.transform,1260,-30,140,()=>ResizeSpell(1/1.25f));Button("BIGGER",notebook.transform,1410,-30,135,()=>ResizeSpell(1.25f));
            Button("CHECK / CAST SEAL",notebook.transform,24,-885,265,TryCastFromNotebook);
        }
        public void ResizeSpell(float factor)
        {
            if(draft.committed){LastInterpretation="Reopen the seal to resize the next spell.";return;}
            Remember();draft.spellSize=Mathf.Clamp(draft.spellSize*factor,.1f,4);nextFeedback=0;board.SetVerticesDirty();
            LastInterpretation="Spell size "+draft.spellSize.ToString("F2")+"×. Geometry, material amount and force budgets scale together; bigger spells require more source material.";
        }
        public void TryCastFromNotebook()
        {
            bool cast=world.CloseSeal(draft);LastInterpretation=world.Message;nextFeedback=0;RefreshSealFeedback(true);if(cast)Show(false);
        }
        void RefreshSealFeedback(bool force=false)
        {
            if(!force&&Time.unscaledTime<nextFeedback)return;nextFeedback=Time.unscaledTime+.2f;
            bool closed=DrawnGeometry.ClosedSeal(draft.seal,draft.nodes,out var boundaryReason);
            bool ready=world.Validate(draft,out _,out _,out var graphReason);
            bool nested=DrawnGeometry.NestedSealsValid(draft,out var nestedReason);
            SealBlocked=!ready||!closed||!nested;
            if(draft.committed){SealStatus="CAST COMMITTED · Reopen to edit or resize. X releases control.";SealBlocked=false;}
            else if(!ready)SealStatus="CANNOT CAST: "+graphReason+" Fix it, then CHECK / CAST SEAL.";
            else if(!closed)SealStatus="SEAL OPEN: "+boundaryReason+" Use 5 OUTER SEAL.";
            else if(!nested)SealStatus="CANNOT CAST: "+nestedReason;
            else SealStatus="READY: outer and inner seals are closed. Press CHECK / CAST SEAL.";
            if(sealFeedback!=null){sealFeedback.text="SIZE "+draft.spellSize.ToString("F2")+"× · "+SealStatus;sealFeedbackBackground.color=SealBlocked?Color.Lerp(Palette.Ink,Palette.Red,.38f):Color.Lerp(Palette.Ink,Palette.Teal,.25f);}
        }
    }
}
