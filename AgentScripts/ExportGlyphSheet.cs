using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using Arcana.Elemental;
public static class ExportGlyphSheet { public static string Run(){
        var svg=new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='1200' height='1220' viewBox='0 0 1200 1220'><rect width='1200' height='1220' fill='#10242b'/><g fill='#f0eadc' font-family='sans-serif'><text x='48' y='64' font-size='38'>ARCANA · GLYPH NOTEBOOK</text><text x='48' y='102' font-size='19'>Choose an element → connect instructions → draw geometry → close the outer seal</text>");
        var runes=Enum.GetValues(typeof(Rune)).Cast<Rune>().Where(r=>r!=Rune.Unknown).ToArray();
        for(int i=0;i<runes.Length;i++){var rune=runes[i];var def=GlyphAlphabet.Get(rune);int x=48+i%3*390,y=150+i/3*145;svg.Append($"<rect x='{x}' y='{y}' width='365' height='126' rx='12' fill='#1d353b'/><text x='{x+105}' y='{y+48}' font-size='23'>{rune.ToString().ToUpperInvariant()}</text>");foreach(var stroke in def.strokes){string pts=string.Join(" ",stroke.Select(p=>(x+50+p.x*40).ToString("F2",CultureInfo.InvariantCulture)+","+(y+64-p.y*40).ToString("F2",CultureInfo.InvariantCulture)));svg.Append($"<polyline points='{pts}' fill='none' stroke='#60d4bd' stroke-width='3' stroke-linecap='round' stroke-linejoin='round'/>");}svg.Append($"<path d='M{x+50} {y+12} v8' stroke='#d8af6a' stroke-width='3'/>");}
        svg.Append("<text x='48' y='1190' font-size='18'>G / F1 notebook · T target · E use · F carry board · X release · F5/F9 save/load</text></g></svg>");File.WriteAllText(Path.GetFullPath("../../GLYPH_ALPHABET.svg"),svg.ToString());
return "Exported 20 actual runtime glyphs";}}