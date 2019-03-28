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
        private TessBaseAPI _tessBaseAPI = null;

        private static readonly string[] _suffixes = new string[] { "ada]", "ata]", "bin]", "bo]", "cak]", "can]", "con]", "cron]", "cta]", "des]", "dex]", "do]", "dra]", "lis]", "mag]", "nak]", "nem]", "nent]", "nok]", "pha]", "sus]", "tak]", "tia]", "tin]", "tio]", "tis]", "ton]", "tor]", "tox]", "tron]" };

        public RivenParser()
        {
            string dataPath = @"tessdata\";
            string language = "eng";
            OcrEngineMode oem = OcrEngineMode.DEFAULT;
            PageSegmentationMode psm = PageSegmentationMode.SINGLE_BLOCK;

            _tessBaseAPI = new TessBaseAPI();

            var path = Path.Combine(Environment.CurrentDirectory, dataPath);
            // Initialize tesseract-ocr 
            if (!_tessBaseAPI.Init(path, language, oem, new string[] { "bazaar" }))
            {
                throw new Exception("Could not initialize tesseract.");
            }
            // Set the Page Segmentation mode
            _tessBaseAPI.SetPageSegMode(psm);
        }

        public void Dispose()
        {
            _tessBaseAPI.Dispose();
        }

        public Riven ParseRivenImage(string imagePath)
        {
            // Set the input image
            Pix pix = _tessBaseAPI.SetImage(imagePath);

            // Recognize image
            _tessBaseAPI.Recognize();

            ResultIterator resultIterator = _tessBaseAPI.GetIterator();

            // Extract text from result iterator
            var rivenText = new List<string>();
            var currentStep = Step.ReadingName;
            var name = string.Empty;
            var modis = new List<string>();
            var drain = 0;
            var mr = 0;
            var rolls = 0;
            var rawSb = new StringBuilder();
            var number = 0;
            PageIteratorLevel pageIteratorLevel = PageIteratorLevel.RIL_TEXTLINE;
            do
            {
                var line = string.Empty;
                try
                {
                    line = resultIterator.GetUTF8Text(pageIteratorLevel).Trim();
                }
                catch { return new Riven(); }
                rawSb.AppendLine(line);
                if (line.Length > 0 && !(line[0] == '+' || line[0] == '-') && currentStep == Step.ReadingName)
                    name = (name + " " + line).Trim();
                else if (line.Length > 0 && (line[0] == '+' || line[0] == '-') && (currentStep == Step.ReadingName || currentStep == Step.ReadingModifiers))
                {
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
                        drain = number;
                }
                else if (line.Length > 0 && currentStep == Step.ReadingMRLine)
                {
                    //MR o 16 D14
                    var splits = line.Split(' ');
                    if (splits.Length == 4)
                    {
                        if (Int32.TryParse(Regex.Match(splits[3], @"\d+").Value.TrimStart('0'), out number))
                            rolls = number;
                    }

                    if (Int32.TryParse(Regex.Match(splits[2], @"\d+").Value.TrimStart('0'), out number))
                        mr = number;
                }

                //rivenText.Add(line);
            } while (resultIterator.Next(pageIteratorLevel));

            pix.Dispose();

            return new Riven()
            {
                Drain = drain,
                MasteryRank = mr,
                Modifiers = modis.ToArray(),
                Name = name,
                Polarity = Polarity.Unkown,
                Rank = "Unknown",
                Rolls = rolls
            };
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
