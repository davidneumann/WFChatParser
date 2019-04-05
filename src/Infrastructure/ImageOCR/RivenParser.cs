using Application.ChatMessages.Model;
using Application.Interfaces;
using Common;
using Leptonica;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;
using Application.Utils;

namespace ImageOCR
{
    public class RivenParser : IDisposable, IRivenParser
    {
        private TesseractEngine _engine = null;

        private static readonly string[] _suffixes = new string[] { "ada]", "ata]", "bin]", "bo]", "cak]", "can]", "con]", "cron]", "cta]", "des]", "dex]", "do]", "dra]", "lis]", "mag]", "nak]", "nem]", "nent]", "nok]", "pha]", "sus]", "tak]", "tia]", "tin]", "tio]", "tis]", "ton]", "tor]", "tox]", "tron]" };
        private List<Point> _dashPixels = new List<Point>();
        private List<Point> _dPixels = new List<Point>();
        private List<Point> _vPixels = new List<Point>();

        public RivenParser()
        {
            string dataPath = @"tessdata\";
            string language = "eng";
            _engine = new TesseractEngine(dataPath, language, EngineMode.Default, "bazaar");
            _engine.DefaultPageSegMode = PageSegMode.SingleBlock;


            //Polarity pixels
            for (int x = 537; x < 537 + 3; x++)
            {
                for (int y = 23; y < 23 + 4; y++)
                {
                    _dashPixels.Add(new Point(x, y));
                }
            }
            for (int x = 560; x < 560 + 3; x++)
            {
                for (int y = 23; y < 23 + 4; y++)
                {
                    _dashPixels.Add(new Point(x, y));
                }
            }


            for (int x = 561; x < 561 + 4; x++)
            {
                for (int y = 32; y < 32 + 7; y++)
                {
                    _dPixels.Add(new Point(x, y));
                }
            }


            for (int x = 542; x < 542 + 1; x++)
            {
                for (int y = 29; y < 29 + 4; y++)
                {
                    _vPixels.Add(new Point(x, y));
                }
            }
            for (int x = 542; x < 542 + 2; x++)
            {
                for (int y = 18; y < 18 + 3; y++)
                {
                    _vPixels.Add(new Point(x, y));
                }
            }
        }

        public void Dispose()
        {
            _engine.Dispose();
        }

        public Riven ParseRivenTextFromImage(Bitmap croppedRiven, string parsedName)
        {
            //if (croppedRiven.Width != 560 && croppedRiven.Height != 760)
            //    return null;

            Riven result = new Riven()
            {
                Polarity = Polarity.Unknown,
                Rank = 0,
            };

            var newModiRegex = new Regex(@"[+-][\d]");
            // Set the input image
            var debug = new StringBuilder();
            using (var pix = PixConverter.ToPix(croppedRiven))
            {
                // Extract text from result iterator
                var rivenText = new List<string>();
                var currentStep = Step.ReadingName;
                var name = string.Empty;
                var modis = new List<string>();
                var number = 0;
                PageIteratorLevel pageIteratorLevel = PageIteratorLevel.TextLine;
                using (var page = _engine.Process(pix))
                {
                    using (var iter = page.GetIterator())
                    {
                        do
                        {
                            var line = string.Empty;
                            try
                            {
                                line = iter.GetText(pageIteratorLevel).Trim();
                                debug.AppendLine(line);
                            }
                            catch { continue; }
                            if (line.Length > 0 && !(newModiRegex.Match(line).Success && line.Contains(' ')) && currentStep == Step.ReadingName)
                                name = (name + " " + line).Trim();
                            else if (line.Length > 0 && (newModiRegex.Match(line).Success && line.Contains(' ')) && (currentStep == Step.ReadingName || currentStep == Step.ReadingModifiers))
                            {
                                result.Name = name;
                                if (parsedName != null)
                                    result.Name = parsedName;
                                currentStep = Step.ReadingModifiers;
                                modis.Add(line);
                            }
                            else if (line.Length > 0 && !Char.IsDigit(line[0]) && currentStep == Step.ReadingModifiers)
                            {
                                var last = modis.Last();
                                modis.Remove(last);
                                last = last + " " + line;
                                modis.Add(last);
                            }
                            else if (line.Length > 0 && Char.IsDigit(line[0]) && currentStep == Step.ReadingModifiers)
                            {
                                var modiObjects = modis.Select(m => Modifier.ParseString(m)).ToArray();
                                //Handle curses
                                if (name.Contains("-") && modiObjects.Length == 4)
                                {
                                    modiObjects[3].Curse = true;
                                }
                                else if(!name.Contains("-") && modiObjects.Length == 3)
                                {
                                    modiObjects[2].Curse = true;
                                }
                                result.Modifiers = modiObjects;
                                currentStep = Step.ReadingMRLine;
                                if (Int32.TryParse(line, out number))
                                    result.Drain = number;
                            }
                            else if (line.Length > 0 && currentStep == Step.ReadingMRLine)
                            {
                                //MR o 16 D14
                                var splits = line.Split(' ');
                                if (splits.Length == 4)
                                {
                                    if (Int32.TryParse(Regex.Match(splits[3], @"\d+").Value.TrimStart('0'), out number))
                                        result.Rolls = number;
                                }

                                if (splits.Length >= 3 && Int32.TryParse(Regex.Match(splits[2], @"\d+").Value.TrimStart('0'), out number))
                                    result.MasteryRank = number;
                                else if (splits.Length < 3 && Int32.TryParse(Regex.Match(line, @"\d+").Value.TrimStart('0'), out number))
                                    result.MasteryRank = number;
                            }

                            //rivenText.Add(line);
                        } while (iter.Next(pageIteratorLevel));
                    }
                }
            }

            result.ImageId = Guid.NewGuid();
            return result;
        }

        private int GetRank(Bitmap croppedRiven)
        {
            var width = 8;
            var startX = 178;
            var gap = 31;
            for (int i = 0; i < 8; i++)
            {
                var pixelValues = 0f;
                for (int x = startX + i * gap; x < startX + i * gap + width; x++)
                {
                    for (int y = 818; y < 818 + 6; y++)
                    {
                        var pixel = croppedRiven.GetPixel(x, y);
                        pixelValues += ((((float)pixel.R / 255f) + ((float)pixel.G / 255f) + ((float)pixel.B / 255f)) / 3f);
                    }
                }
                if (pixelValues / 48f < 0.75f)
                    return Math.Max(i, 0);
            }
            return 8;
        }

        private bool IsPurple(Point p, Bitmap bitmap)
        {
            var pixel = bitmap.GetPixel(p.X, p.Y);
            var hsv = pixel.ToHsv();
            if (hsv.Hue >= 240 && hsv.Hue <= 280
                && hsv.Value >= 0.4)
                return true;
            return false;
        }
        private Polarity GetPolarity(Bitmap croppedRiven)
        {
            var dashMatches = _dashPixels.Count(p => IsPurple(p, croppedRiven));
            var vMatches = _vPixels.Count(p => IsPurple(p, croppedRiven));
            var dMatches = _dPixels.Count(p => IsPurple(p, croppedRiven));

            if (dashMatches > _dashPixels.Count * 0.9)
                return Polarity.Naramon;
            else if (vMatches > _vPixels.Count * 0.9)
                return Polarity.Madurai;
            else if (dMatches > _dashPixels.Count * 0.9)
                return Polarity.Vazarin;
            else
                return Polarity.Unknown;
        }

        public Bitmap CropToRiven(Bitmap bitmap)
        {
            return bitmap.Clone(new Rectangle(1757, 463, 582, 831), bitmap.PixelFormat);
        }

        public bool IsRivenPresent(Bitmap bitmap)
        {
            if (bitmap.Width != 582 || bitmap.Height != 1043)
                return false;

            var p = bitmap.GetPixel(254, 27);
            if (p.R > 100 || p.G > 100 || p.B > 100) //Not dark
                return false;
            p = bitmap.GetPixel(260, 27);
            if (p.R < 200 || p.G < 200 || p.B < 200) //Not bright
                return false;
            p = bitmap.GetPixel(265, 29);
            if (p.R > 100 || p.G > 100 || p.B > 100) //Not Dark
                return false;
            p = bitmap.GetPixel(271, 31);
            if (p.R < 200 || p.G < 200 || p.B < 200) //Not bright
                return false;
            p = bitmap.GetPixel(275, 28);
            if (p.R > 100 || p.G > 100 || p.B > 100) //Not dark
                return false;
            p = bitmap.GetPixel(247, 53);
            if (p.R > 100 || p.G > 100 || p.B > 100) //Not dark
                return false;
            p = bitmap.GetPixel(252, 53);
            if (p.R < 200 || p.G < 200 || p.B < 200) //Not bright
                return false;
            p = bitmap.GetPixel(258, 54);
            if (p.R > 100 || p.G > 100 || p.B > 100) //Not dark
                return false;

            return true;
        }

        public Polarity ParseRivenPolarityFromColorImage(Bitmap croppedRiven)
        {
            return GetPolarity(croppedRiven);
        }

        public int ParseRivenRankFromColorImage(Bitmap croppedRiven)
        {
            return GetRank(croppedRiven);
        }

        private enum Step
        {
            ReadingName,
            ReadingModifiers,
            ReadingMRLine
        }
    }
}
