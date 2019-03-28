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

namespace ImageOCR
{
    public class RivenParser : IDisposable, IRivenParser
    {
        private TesseractEngine _engine = null;

        private static readonly string[] _suffixes = new string[] { "ada]", "ata]", "bin]", "bo]", "cak]", "can]", "con]", "cron]", "cta]", "des]", "dex]", "do]", "dra]", "lis]", "mag]", "nak]", "nem]", "nent]", "nok]", "pha]", "sus]", "tak]", "tia]", "tin]", "tio]", "tis]", "ton]", "tor]", "tox]", "tron]" };

        public RivenParser()
        {
            string dataPath = @"tessdata\";
            string language = "eng";
            _engine = new TesseractEngine(dataPath, language, EngineMode.Default, "bazaar");
            _engine.DefaultPageSegMode = PageSegMode.SingleBlock;
        }

        public void Dispose()
        {
            _engine.Dispose();
        }

        public Riven ParseRivenImage(Bitmap croppedRiven)
        {
            if (croppedRiven.Width != 560 && croppedRiven.Height != 740)
                return null;

            Riven result = new Riven()
            {
                Polarity = Polarity.Unkown,
                Rank = "Unknown",
            };

            // Set the input image
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
                            }
                            catch { continue; }
                            if (line.Length > 0 && !(line[0] == '+' || line[0] == '-') && currentStep == Step.ReadingName)
                                name = (name + " " + line).Trim();
                            else if (line.Length > 0 && (line[0] == '+' || line[0] == '-') && (currentStep == Step.ReadingName || currentStep == Step.ReadingModifiers))
                            {
                                result.Name = name;
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
                                result.Modifiers = modis.ToArray();
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

            result.ImageID = Guid.NewGuid();
            return result;
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

        private enum Step
        {
            ReadingName,
            ReadingModifiers,
            ReadingMRLine
        }
    }
}
