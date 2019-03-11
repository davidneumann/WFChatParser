using Application.Interfaces;
using Common;
using Leptonica;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace ImageOCR
{
    public class ImageParser : IDisposable, IImageParser
    {
        private TessBaseAPI _tessBaseAPI = null;

        private static readonly string[] _suffixes = new string[] { "ada]", "ata]", "bin]", "bo]", "cak]", "can]", "con]", "cron]", "cta]", "des]", "dex]", "do]", "dra]", "lis]", "mag]", "nak]", "nem]", "nent]", "nok]", "pha]", "sus]", "tak]", "tia]", "tin]", "tio]", "tis]", "ton]", "tor]", "tox]", "tron]" };

        public ImageParser()
        {
            string dataPath = @"C:\Program Files (x86)\Tesseract-OCR\tessdata";
            string language = "eng";
            OcrEngineMode oem = OcrEngineMode.DEFAULT;
            PageSegmentationMode psm = PageSegmentationMode.SINGLE_BLOCK;

            _tessBaseAPI = new TessBaseAPI();

            // Initialize tesseract-ocr 
            if (!_tessBaseAPI.Init(dataPath, language, oem, new string[] { "bazaar" }))
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
            PageIteratorLevel pageIteratorLevel = PageIteratorLevel.RIL_WORD;
            var clickPoints = new List<ClickPoint>();
            do
            {
                var word = resultIterator.GetUTF8Text(pageIteratorLevel) + " ";
                var debug = resultIterator.BoundingBox(PageIteratorLevel.RIL_WORD, out var left, out var top, out var right, out var bottom);
                if (word[0] != '[' && word.Contains(']') && _suffixes.Any(suffix => word.Contains(suffix)))
                    clickPoints.Add(new ClickPoint() { X = left, Y = (top + bottom) / 2 });
                if (left < 10 && stringBuilder.Length > 0)
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

        public string[] ParseRivenImage(string imagePath)
        {
            // Set the input image
            Pix pix = _tessBaseAPI.SetImage(imagePath);

            // Recognize image
            _tessBaseAPI.Recognize();

            ResultIterator resultIterator = _tessBaseAPI.GetIterator();

            // Extract text from result iterator
            var rivenText = new List<string>();
            PageIteratorLevel pageIteratorLevel = PageIteratorLevel.RIL_TEXTLINE;
            do
            {
                var line = resultIterator.GetUTF8Text(pageIteratorLevel) + " ";
                rivenText.Add(line);
            } while (resultIterator.Next(pageIteratorLevel));

            pix.Dispose();

            return rivenText.ToArray();
        }
    }
}
