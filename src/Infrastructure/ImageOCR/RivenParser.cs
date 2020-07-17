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
using Application.Data;
using Application.Enums;

namespace ImageOCR
{
    public class RivenParser : IDisposable, IRivenParser
    {
        private TesseractEngine _engine = null;

        private static readonly string[] _suffixes = new string[] { "ada]", "ata]", "bin]", "bo]", "cak]", "can]", "con]", "cron]", "cta]", "des]", "dex]", "do]", "dra]", "lis]", "mag]", "nak]", "nem]", "nent]", "nok]", "pha]", "sus]", "tak]", "tia]", "tin]", "tio]", "tis]", "ton]", "tor]", "tox]", "tron]" };
        private List<Point> _dashPixels = new List<Point>();
        private List<Point> _dPixels = new List<Point>();
        private List<Point> _vPixels = new List<Point>();

        private LineParser _lineParser;
        private WordParser _wordParser;
        private ClientLanguage _clientLanguage;

        public RivenParser(ClientLanguage clientLanguage)
        {
            _clientLanguage = clientLanguage;
            string dataPath = DataHelper.TessDataPath;
            Console.WriteLine($"Trying to use datapath: {dataPath}");
            string language = "chi_sim+eng";
            if (_clientLanguage == ClientLanguage.English)
                language = "eng";
            _engine = new TesseractEngine(dataPath, language, EngineMode.Default, "bazaar");
            _engine.DefaultPageSegMode = PageSegMode.SingleLine;
            _lineParser = new LineParser(_clientLanguage);
            _wordParser = new WordParser(_clientLanguage);

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

        public Riven ParseRivenTextFromImage(Bitmap cleanedRiven, string parsedName)
        {
            //if (croppedRiven.Width != 560 && croppedRiven.Height != 760)
            //    return null;

            Riven result = new Riven()
            {
                Polarity = Polarity.Unknown,
                Rank = 0,
            };

            var allLines = GetLines(cleanedRiven);

            var newModiRegex = new Regex(@"[+-][\d]");
            // Set the input image
            var debug = allLines.Aggregate("", (str, line) => (str + "\n" + line).Trim());
            var currentStep = Step.ReadingName;
            var name = string.Empty;
            var modis = new List<string>();
            var number = 0;
            foreach (var line in allLines)
            {
                if (line.Length > 0 && !(newModiRegex.Match(line).Success && line.Contains(' ')) && currentStep == Step.ReadingName)
                    name = (name + " " + line).Trim();
                else if (line.Length > 0 
                    && ((newModiRegex.Match(line).Success && line.Contains(' ')) 
                          || (Char.IsDigit(line[0]) && line.Length > 6 && line.Contains(' ')) //Handle missing + or -
                       )
                    && (currentStep == Step.ReadingName || currentStep == Step.ReadingModifiers))
                {
                    result.Name = name;
                    if (parsedName != null && _clientLanguage == ClientLanguage.English)
                    {
                        result.Name = parsedName;
                        name = result.Name;
                    }
                    else
                        result.Name = name.Replace("—", "-"); ;
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
                    currentStep = Step.ReadingMRLine;
                    if (Int32.TryParse(line, out number))
                        result.Drain = number;
                }
                else if (line.Length > 0 && currentStep == Step.ReadingMRLine)
                {
                    //MR o 16 D14
                    //MR 6 10 . 02
                    //var matches = Regex.Match(line, @"MR[^\d]*(\d+)[^\d]*(\d+)?");
                    //Should now either be x or x     x
                    var matches = Regex.Match(line, @"(\d+)\s*(\d+)?");
                    if (matches.Success)
                    {
                        if (matches.Groups[2].Success)
                        {

                            if (Int32.TryParse(matches.Groups[2].Value, out number))
                                result.Rolls = number;
                        }
                        else
                            result.Rolls = 0;

                        if (matches.Groups[1].Success)
                        {
                            if (Int32.TryParse(matches.Groups[1].Value, out number))
                                result.MasteryRank = number;
                        }
                    }
                }
            }

            name = name.Replace("—", "-");
            if (modis.Count > 0)
            {
                var modiObjects = modis.Select(m => Modifier.ParseString(m, _clientLanguage)).ToArray();
                //Handle curses
                if (name.Contains("-") && modiObjects.Length == 4)
                {
                    modiObjects[3].Curse = true;
                }
                else if (!name.Contains("-") && modiObjects.Length == 3)
                {
                    modiObjects[2].Curse = true;
                }
                result.Modifiers = modiObjects;
            }

#if DEBUG && false
            Console.WriteLine(debug.ToString());
#endif
            result.ImageId = Guid.NewGuid();
            return result;
        }

        private List<string> GetLines(Bitmap cleanedRiven)
        {
            List<string> allLines = new List<string>();
            var lineRects = new List<Rectangle>();
            for (int y = 0; y < cleanedRiven.Height;)
            {
                var lineRect = GetNextLineRect(y, cleanedRiven);
                if (lineRect == Rectangle.Empty || lineRect.Top >= cleanedRiven.Height)
                    break;
                lineRects.Add(lineRect);
                y = lineRect.Bottom;
            }

#if DEBUG
            foreach (var file in Directory.GetFiles(Environment.CurrentDirectory).Select(f => new FileInfo(f)).Where(f => f.Name.StartsWith("debug_")))
            {
                try
                {
                    File.Delete(file.FullName);
                }
                catch { }
            }
#endif
            for (int i = 0; i < lineRects.Count; i++)
            {
                var lineRect = lineRects[i];
                using (var lineBitmap = new Bitmap(lineRect.Width, lineRect.Height))
                {
                    //for (int backgroundX = 0; backgroundX < lineBitmap.Width; backgroundX++)
                    //{
                    //    for (int backgroundY = 0; backgroundY < lineBitmap.Height; backgroundY++)
                    //    {
                    //        lineBitmap.SetPixel(backgroundX, backgroundY, Color.White);
                    //    }
                    //}
                    //for (int referenceX = lineRect.Left; referenceX < lineRect.Right; referenceX++)
                    //{
                    //    for (int referenceY = lineRect.Top; referenceY < lineRect.Bottom; referenceY++)
                    //    {
                    //        //Color pixel = cleanedRiven.GetPixel(referenceX, referenceY);
                    //        //if(pixel.R < 128)
                    //        //    lineBitmap.SetPixel(10 + referenceX - lineRect.Left, 10 + referenceY - lineRect.Top, Color.Black);
                    //        //else
                    //        //    lineBitmap.SetPixel(10 + referenceX - lineRect.Left, 10 + referenceY - lineRect.Top, Color.White);
                    //        lineBitmap.SetPixel(10 + referenceX - lineRect.Left, 10 + referenceY - lineRect.Top, cleanedRiven.GetPixel(referenceX, referenceY));
                    //    }
                    //}

                    //Copy pixels
                    for (int referenceX = lineRect.Left; referenceX < lineRect.Right; referenceX++)
                    {
                        for (int referenceY = lineRect.Top; referenceY < lineRect.Bottom; referenceY++)
                        {
                            //Color pixel = cleanedRiven.GetPixel(referenceX, referenceY);
                            //if(pixel.R < 128)
                            //    lineBitmap.SetPixel(10 + referenceX - lineRect.Left, 10 + referenceY - lineRect.Top, Color.Black);
                            //else
                            //    lineBitmap.SetPixel(10 + referenceX - lineRect.Left, 10 + referenceY - lineRect.Top, Color.White);
                            lineBitmap.SetPixel(referenceX - lineRect.Left, referenceY - lineRect.Top, cleanedRiven.GetPixel(referenceX, referenceY));
                        }
                    }

                    //Resize up
                    var scale = 48f / lineBitmap.Height;
                    using (var resized = new Bitmap(lineBitmap, new Size((int)(lineBitmap.Width * scale), (int)(lineBitmap.Height * scale))))
                    {
                        for (int x = 0; x < resized.Width; x++)
                        {
                            for (int y = 0; y < resized.Height; y++)
                            {
                                Color p = resized.GetPixel(x, y);
                                if (p.A < byte.MaxValue)
                                    resized.SetPixel(x, y, Color.FromArgb(byte.MaxValue, p.R, p.G, p.B));
                            }
                        }

                        //Add padding
                        using (var padding = new Bitmap(resized.Width + 20, resized.Height + 20))
                        {
                            //Background
                            for (int backgroundX = 0; backgroundX < padding.Width; backgroundX++)
                            {
                                for (int backgroundY = 0; backgroundY < padding.Height; backgroundY++)
                                {
                                    padding.SetPixel(backgroundX, backgroundY, Color.White);
                                }
                            }

                            for (int x = 0; x < resized.Width; x++)
                            {
                                for (int y = 0; y < resized.Height; y++)
                                {
                                    padding.SetPixel(10 + x, 10 + y, resized.GetPixel(x, y));
                                }
                            }

#if DEBUG
                            try
                            {
                                padding.Save("debug_ " + allLines.Count + ".png");
                            }
                            catch { }
#endif
                            if (i != lineRects.Count - 2)
                            {
                                allLines.Add(_lineParser.ParseLine(padding));
                            }
                            else
                            {
                                string line = "";
                                int number = 0;
                                line = _lineParser.ParseLine(padding);
                                if (int.TryParse(line, out number))
                                    allLines.Add(line);
                                else
                                {
                                    line = _wordParser.ParseWord(padding);
                                    if (int.TryParse(line, out number))
                                        allLines.Add(line);
                                    else
                                        allLines.Add("-1");
                                }
                            }
                        }
                    }
                }
            }
#if DEBUG
            try
            {
                var debugs = Directory.GetFiles(Environment.CurrentDirectory).Where(f => f.Substring(f.LastIndexOf("\\") + 1).StartsWith("debug_")).OrderBy(f => f).Select(f => new Bitmap(f)).ToArray();
                var height = debugs.Aggregate(0, (prod, next) => prod + next.Height);
                var width = debugs.Max(f => f.Width);
                using (var combinedDebug = new Bitmap(width, height))
                {
                    var offset = 0;
                    for (int i = 0; i < debugs.Length; i++)
                    {
                        var startX = width / 2 - debugs[i].Width / 2;
                        for (int x = 0; x < debugs[i].Width; x++)
                        {
                            for (int y = 0; y < debugs[i].Height; y++)
                            {
                                combinedDebug.SetPixel(startX + x, offset + y, debugs[i].GetPixel(x, y));
                            }
                        }
                        offset += debugs[i].Height;
                    }
                    combinedDebug.Save("debug_combined.png");
                }
                debugs.ToList().ForEach(f => f.Dispose());
            }
            catch (Exception e) { }
#endif
            return allLines;
        }

        private Rectangle GetNextLineRect(int lastY, Bitmap bitmap)
        {
            var result = Rect.Empty;
            var startingY = -1;
            var endingY = -1;
            var startX = -1;
            var endX = -1;
            for (int y = lastY; y < bitmap.Height; y++)
            {
                var pixelFound = false;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).R < 128)
                    {
                        pixelFound = true;
                        if (x < startX || startX < 0)
                            startX = x;
                        if (x > endX)
                            endX = x;

                        if (startingY < 0 || y < startingY)
                            startingY = y;
                        if (endingY < y)
                            endingY = y;
                    }
                }
                if (!pixelFound && endingY > 0 && endingY - startingY > 12)
                    break;
            }
            endX++;
            endingY++;

            if (startingY > 0 && endingY < 0)
                endingY = bitmap.Height;

            if (endingY - startingY > 65)
                endingY = startingY + (endingY - startingY) / 2;

            if (startingY > 0)
                return new Rectangle(startX, startingY, endX - startX, endingY - startingY);
            else
                return Rectangle.Empty;
        }

        private int GetRank(Bitmap croppedRiven)
        {
            var width = 8;
            var startX = 178;
            var gap = 31;
            var inputRiven = croppedRiven;
            if (inputRiven.Width != 582)
                inputRiven = new Bitmap(croppedRiven, new Size(582, 831));

            for (int i = 0; i < 8; i++)
            {
                var pixelValues = 0f;
                for (int x = startX + i * gap; x < startX + i * gap + width; x++)
                {
                    for (int y = 818; y < 818 + 6; y++)
                    {
                        var pixel = inputRiven.GetPixel(x, y);
                        pixelValues += ((((float)pixel.R / 255f) + ((float)pixel.G / 255f) + ((float)pixel.B / 255f)) / 3f);
                    }
                }
                if (pixelValues / 48f < 0.75f)
                {
                    if (inputRiven.Width != croppedRiven.Width)
                        inputRiven.Dispose();
                    return Math.Max(i, 0);
                }
            }
            if (inputRiven.Width != croppedRiven.Width)
                inputRiven.Dispose();
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
            var inputRiven = croppedRiven;
            if (inputRiven.Width != 582)
                inputRiven = new Bitmap(croppedRiven, new Size(582, 831));

            var dashMatches = _dashPixels.Count(p => IsPurple(p, inputRiven));
            var vMatches = _vPixels.Count(p => IsPurple(p, inputRiven));
            var dMatches = _dPixels.Count(p => IsPurple(p, inputRiven));

            var result = Polarity.Unknown;
            if (dashMatches > _dashPixels.Count * 0.9)
                result = Polarity.Naramon;
            else if (vMatches > _vPixels.Count * 0.9)
                result = Polarity.Madurai;
            else if (dMatches > _dashPixels.Count * 0.9)
                result = Polarity.Vazarin;
            else
                result = Polarity.Unknown;

            if (inputRiven.Width != croppedRiven.Width)
                inputRiven.Dispose();
            return result;
        }

        public Bitmap CropToRiven(Bitmap bitmap)
        {
            return bitmap.Clone(new Rectangle((int)(bitmap.Width * 0.443115234375),
                (int)(bitmap.Height * 0.3578703703703704),
                (int)(bitmap.Width * 0.11376953125),
                (int)(bitmap.Height * 0.3083333333333333)),
                bitmap.PixelFormat);
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
