using System;
using System.Drawing;
using System.Linq;

namespace Application.Actionables.ProfileBots
{
    internal class Header
    {
        private static string[] _headerOptions = new string[] { "ACCOLADES", "MASTERY RANK", "CLAN", "MARKED FOR DEATH BY" };

        public Bitmap Bitmap { get; set; }
        public int Anchor { get; set; }
        public string Text { get; } = "UNKNOWN";
        public HeaderOption Value { get; } = HeaderOption.Unknown;

        public Header(Bitmap bitmap, int yAnchor, string rawText)
        {
            Anchor = yAnchor;
            Bitmap = bitmap;
            var best = _headerOptions.Select(Text => (Text, Score: Utils.LevenshteinDistance.Compute(rawText, Text))).OrderBy(o => o.Score).First();
            if (best.Score < 5)
            {
                Text = best.Text;
                switch (Text)
                {
                    case "ACCOLADES":
                        Value = HeaderOption.Accolades;
                        break;
                    case "MASTERY RANK":
                        Value = HeaderOption.MasteryRank;
                        break;
                    case "CLAN":
                        Value = HeaderOption.Clan;
                        break;
                    case "MARKED FOR DEATH BY":
                        Value = HeaderOption.MarkedForDeathBy;
                        break;
                    default:
                        Value = HeaderOption.Unknown;
                        break;
                }
            }
        }
    }
}
