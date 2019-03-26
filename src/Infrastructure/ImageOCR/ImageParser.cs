using Application.ChatMessages.Model;
using Application.Interfaces;
using Common;
using Leptonica;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;

namespace ImageOCR
{
    public class ImageParser : IDisposable
    {
        private TessBaseAPI _tessBaseAPI = null;

        private static readonly string[] _suffixes = new string[] { "ada]", "ata]", "bin]", "bo]", "cak]", "can]", "con]", "cron]", "cta]", "des]", "dex]", "do]", "dra]", "lis]", "mag]", "nak]", "nem]", "nent]", "nok]", "pha]", "sus]", "tak]", "tia]", "tin]", "tio]", "tis]", "ton]", "tor]", "tox]", "tron]" };

        public ImageParser()
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

        public ImageParseResult ParseChatImage(string imagePath)
        {
            // Set the input image
            Pix pix = _tessBaseAPI.SetImage(imagePath);

            // Recognize image
            _tessBaseAPI.Recognize();

            ResultIterator resultIterator = _tessBaseAPI.GetIterator();

            // Extract text from result iterator
            StringBuilder stringBuilder = new StringBuilder();
            var chatText = new List<string>();
            PageIteratorLevel pageIteratorLevel = PageIteratorLevel.RIL_SYMBOL;
            var clickPoints = new List<ClickPoint>();
            var regex = new Regex(@"\[\d\d:\d\d\]", RegexOptions.Compiled);
            var minConf = float.MaxValue;
            do
            {
                var confidence = resultIterator.Confidence(pageIteratorLevel);
                var word = resultIterator.GetUTF8Text(pageIteratorLevel);
                if (confidence <= 90)
                    word = "~~" + word + "~~";
                if (confidence < minConf)
                    minConf = confidence;
                resultIterator.BoundingBox(pageIteratorLevel, out var left, out var top, out var right, out var bottom);
                //if (word[0] != '[' && word.Contains(']') && _suffixes.Any(suffix => word.Contains(suffix)))
                //    clickPoints.Add(new ClickPoint() { X = left, Y = (top + bottom) / 2 });
                if (left < 10 && stringBuilder.Length > 0)// && regex.Match(word).Success)
                {
                    chatText.Add(stringBuilder.ToString());
                    stringBuilder.Clear();
                }
                stringBuilder.Append(word);
            } while (resultIterator.Next(pageIteratorLevel));
            chatText.Add(stringBuilder.ToString());

            pix.Dispose();

            return new ImageParseResult()
            {
                ClickPoints = clickPoints.ToArray(),
                ChatTextLines = chatText.ToArray()
            };
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
            var drain = "0";
            var mr = "0";
            var rolls = "0";
            var rawSb = new StringBuilder();
            PageIteratorLevel pageIteratorLevel = PageIteratorLevel.RIL_TEXTLINE;
            do
            {
                var line = resultIterator.GetUTF8Text(pageIteratorLevel).Trim();
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
                    drain = line;
                }
                else if (line.Length > 0 && currentStep == Step.ReadingMRLine)
                {
                    //MR o 16 D14
                    var splits = line.Split(' ');
                    if (splits.Length == 4)
                        rolls = Regex.Match(splits[3], @"\d+").Value.TrimStart('0');
                    mr = Regex.Match(splits[2], @"\d+").Value.TrimStart('0');
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

        private enum Step
        {
            ReadingName,
            ReadingModifiers,
            ReadingMRLine
        }
    }
}
