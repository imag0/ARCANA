using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arcana.Elemental
{
    public sealed partial class GlyphNotebook
    {
        void LoadCompoundStudy()
        {
            var asset=Resources.Load<TextAsset>("CompoundStudy");if(asset==null){LastInterpretation="Compound study data is missing.";return;}
            Remember();draft=JsonUtility.FromJson<Construction>(asset.text);draft.id=System.Guid.NewGuid().ToString("N");draft.committed=false;activeSeal=0;selected=null;glyphInk.Clear();layer=InkLayer.Seal;board.ResetView();
            LastInterpretation="Editable compound study: two closed inner regions, one open outer seal. Join the outer gap, then aim while it assembles. All sockets and sketches are editable.";
        }
        public void NewInnerSeal()
        {
            if(draft.committed)return;Remember();SelectLayer(InkLayer.Seal);
            activeSeal=draft.innerSeals.Count==0?1:draft.innerSeals.Max(s=>s.id)+1;
            draft.innerSeals.Add(new SealRegion{id=activeSeal});LastInterpretation="INNER SEAL #"+activeSeal+": enclose a branch. Closing this region cannot activate the spell.";
        }
        void CommitSealStroke(List<Vector2> points)
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
