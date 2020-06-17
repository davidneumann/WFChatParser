using CornerChatParser.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CornerChatParser.Database
{
    public static class GlyphDatabase
    {
        public static List<Glyph> AllGLyphs = new List<Glyph>();

        static GlyphDatabase()
        {
            AllGLyphs = JsonConvert.DeserializeObject<List<Glyph>>(File.ReadAllText("CornerDB.json"));

            foreach (var glyph in AllGLyphs)
            {
                glyph.ReferenceCorners = new bool[glyph.ReferenceMaxWidth, glyph.ReferenceMaxHeight];
                foreach (var corner in glyph.Corners)
                {
                    var x = (int)Math.Round(corner.X * (glyph.ReferenceMaxWidth - 1));
                    var y = (int)Math.Round(corner.Y * (glyph.ReferenceMaxHeight - 1));
                    glyph.ReferenceCorners[x, y] = true;
                }
                glyph.VerticalWeight = glyph.Corners.Select(v => v.Y).Average();
                glyph.HorizontalWeight = glyph.Corners.Select(v => v.X).Average();
            }
        }
    }
}
