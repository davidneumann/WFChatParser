using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CornerChatParser
{
    public static class GlyphDatabase
    {
        public static List<Glyph> AllGLyphs = new List<Glyph>();

        static GlyphDatabase()
        {
            AllGLyphs = JsonConvert.DeserializeObject<List<Glyph>>(File.ReadAllText("CornerDB.json"));

            foreach (var glyph in AllGLyphs)
            {
                glyph.ReferenceCorners = new bool[glyph.ReferenceWidth, glyph.ReferenceHeight];
                foreach (var corner in glyph.Corners)
                {
                    var x = (int)Math.Round(corner.X * (glyph.ReferenceWidth - 1));
                    var y = (int)Math.Round(corner.Y * (glyph.ReferenceHeight - 1));
                    glyph.ReferenceCorners[x, y] = true;
                }
                glyph.VerticalWeight = glyph.Corners.Select(v => v.Y).Average();
                glyph.HorizontalWeight = glyph.Corners.Select(v => v.X).Average();
            }
        }
    }
}
