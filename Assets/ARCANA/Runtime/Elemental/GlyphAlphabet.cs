using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arcana.Elemental
{
    public sealed class GlyphTemplate
    {
        public Rune rune; public string name,meaning; public float spin=1;public List<Vector2[]> strokes=new List<Vector2[]>();
        public GlyphTemplate(Rune r,string title,string description,params Vector2[][] paths){rune=r;name=title;meaning=description;strokes.AddRange(paths);}
    }
    public struct Recognition { public Rune rune;public float confidence,spin;public string message; }
    public static class GlyphAlphabet
    {
        static Vector2[] P(params float[] xy){var p=new Vector2[xy.Length/2];for(int i=0;i<p.Length;i++)p[i]=new Vector2(xy[2*i],xy[2*i+1]);return p;}
        static Vector2[] Circle(float radius=1,float start=0,float sweep=360){var p=new Vector2[33];for(int i=0;i<p.Length;i++){float a=(start+sweep*i/32)*Mathf.Deg2Rad;p[i]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;}return p;}
        static Vector2[] Wave(float y){var p=new Vector2[25];for(int i=0;i<p.Length;i++){float x=-1+2f*i/24;p[i]=new Vector2(x,y+.15f*Mathf.Sin(x*Mathf.PI*2));}return p;}
        public static readonly List<GlyphTemplate> Templates=Create();
        static List<GlyphTemplate> Create()
        {
            var list=new List<GlyphTemplate>{
                new GlyphTemplate(Rune.Fire,"FIRE / upright triangle","Adds heat from charge. Eligible fuel can keep burning.",P(0,1,-1,-1,1,-1,0,1)),
                new GlyphTemplate(Rune.Water,"WATER / inverted triangle","Moves finite water from a selected source.",P(-1,1,1,1,0,-1,-1,1)),
                new GlyphTemplate(Rune.Air,"AIR / three waves","Force and drag move props and carry hot gas.",Wave(-.6f),Wave(0),Wave(.6f)),
                new GlyphTemplate(Rune.Stone,"STONE / square and dot","Moves existing rock or loose soil; mass matters.",P(-1,-1,1,-1,1,1,-1,1,-1,-1),Circle(.09f)),
                new GlyphTemplate(Rune.Frost,"FROST / six-point asterisk","Removes heat over time. Available water becomes ice.",P(-1,0,1,0),P(-.5f,-.87f,.5f,.87f),P(-.5f,.87f,.5f,-.87f)),
                new GlyphTemplate(Rune.Lightning,"LIGHTNING / zigzag","Finite discharge follows conductive, uninsulated paths.",P(.5f,1,-.4f,.15f,.4f,.15f,-.5f,-1)),
                new GlyphTemplate(Rune.Light,"LIGHT / sun","Illumination and beams; walls block light.",Circle(.48f),P(-1,0,-.67f,0),P(.67f,0,1,0),P(0,.67f,0,1),P(0,-1,0,-.67f)),
                new GlyphTemplate(Rune.Gather,"GATHER / open cup","Transfers a bounded amount from the chosen source.",P(.7f,.8f,-.3f,.8f,-.8f,.3f,-.8f,-.3f,-.3f,-.8f,.7f,-.8f)),
                new GlyphTemplate(Rune.Form,"FORM / diamond","Shape layer defines where material or emission is arranged.",P(0,1,1,0,0,-1,-1,0,0,1)),
                new GlyphTemplate(Rune.Direct,"DIRECT / arrow","Trajectory layer guides motion with a bounded force.",P(-1,0,1,0,.45f,.55f),P(1,0,.45f,-.55f)),
                new GlyphTemplate(Rune.Spin,"SPIN / curved arrow","Orbit about the shown axis. Toggle direction to reverse.",Circle(.8f,140,-290),P(-.7f,-.5f,-.3f,-.8f,-.3f,-.25f)),
                new GlyphTemplate(Rune.Hold,"HOLD / table","Holds relative to a world or attached anchor; pays sustain.",P(-1,-1,-1,.7f,1,.7f,1,-1)),
                new GlyphTemplate(Rune.Spread,"SPREAD / two-headed arrow","Wider distribution of the same mass and power.",P(-1,0,1,0),P(-.45f,.5f,-1,0,-.45f,-.5f),P(.45f,.5f,1,0,.45f,-.5f)),
                new GlyphTemplate(Rune.Split,"SPLIT / Y","Splits quantity and power between outgoing branches.",P(-.8f,1,0,0,.8f,1),P(0,0,0,-1)),
                new GlyphTemplate(Rune.Combine,"COMBINE / circled plus","Shared interaction region, using material properties.",Circle(.9f),P(-.5f,0,.5f,0),P(0,-.5f,0,.5f)),
                new GlyphTemplate(Rune.Target,"TARGET / bullseye","Locks the marked aim point; optional paid tracking.",Circle(.9f),Circle(.15f)),
                new GlyphTemplate(Rune.End,"END / stop bar","Releases control at the end of a route; matter remains.",P(-1,0,.6f,0),P(.6f,1,.6f,-1)),
                new GlyphTemplate(Rune.Assemble,"ASSEMBLE / hourglass","Wait for all incoming material to reach its form before downstream launch.",P(-1,1,1,1,-1,-1,1,-1,-1,1)),
                new GlyphTemplate(Rune.Launch,"LAUNCH / arrow from cup","Capture the player's look direction after assembly; move this connected group together.",P(-.9f,.6f,-.9f,-.6f,-.5f,-.6f),P(-.5f,0,1,0,.5f,.5f),P(1,0,.5f,-.5f)),
                new GlyphTemplate(Rune.Junction,"JUNCTION / dot","Explicit merge point; plain crossing lines stay separate.",Circle(.12f))};
            var reverse=new GlyphTemplate(Rune.Spin,"SPIN / reverse","Opposite orbit direction.",Circle(.8f,-140,290),P(-.7f,.5f,-.3f,.8f,-.3f,.25f)){spin=-1};list.Add(reverse);return list;
        }
        static List<Vector2> Cloud(IEnumerable<IReadOnlyList<Vector2>> paths)
        {
            var all=paths.SelectMany(p=>p).ToList();if(all.Count==0)return all;Vector2 min=all[0],max=all[0];foreach(var p in all){min=Vector2.Min(min,p);max=Vector2.Max(max,p);}float scale=Mathf.Max(max.x-min.x,max.y-min.y,.001f);var center=(max+min)*.5f;
            var cloud=new List<Vector2>();foreach(var path in paths){var normalized=path.Select(p=>(p-center)/scale*2).ToList();cloud.AddRange(DrawnGeometry.Resample(normalized,.075f));}return cloud;
        }
        static float Distance(List<Vector2> a,List<Vector2> b)
        {float One(List<Vector2> from,List<Vector2> to){float sum=0;foreach(var p in from){float best=100;foreach(var q in to)best=Mathf.Min(best,(p-q).sqrMagnitude);sum+=Mathf.Sqrt(best);}return sum/Mathf.Max(1,from.Count);}return (One(a,b)+One(b,a))*.5f;}
        public static Recognition Recognize(IReadOnlyList<InkStroke> strokes)
        {
            var cloud=Cloud(strokes.Select(s=>(IReadOnlyList<Vector2>)s.points));if(cloud.Count<4)return new Recognition{rune=Rune.Unknown,message="Draw the glyph, then finish recognition."};
            float best=100,second=100;GlyphTemplate found=null;foreach(var template in Templates)
            {
                float score=Distance(cloud,Cloud(template.strokes));int diff=Math.Abs(strokes.Count-template.strokes.Count);score+=Mathf.Min(diff,3)*.015f;
                if(score<best){second=best;best=score;found=template;}else second=Mathf.Min(second,score);
            }
            float confidence=Mathf.Clamp01(1-best*2.5f);if(second-best<.025f)confidence=Mathf.Min(confidence,.55f);
            return new Recognition{rune=found.rune,spin=found.spin,confidence=confidence,message=confidence>=.58f?found.name+" · "+Mathf.RoundToInt(confidence*100)+"%":"Uncertain: "+found.name+"? Redraw or correct using the alphabet."};
        }
        public static GlyphTemplate Get(Rune rune)=>Templates.First(t=>t.rune==rune);
        public static string ElementDescription(Rune rune)=>rune==Rune.Unknown?"UNKNOWN · draw or correct this glyph":Get(rune).name+"\n"+Get(rune).meaning;
    }
}
