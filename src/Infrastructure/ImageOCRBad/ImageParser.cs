using Common;
using Leptonica;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace ImageOCRBad
{
    public class ImageParser : IDisposable
    {
        private TessBaseAPI _tessBaseAPI = null;
        public ImageParser()
        {
            string dataPath = @"C:\Program Files (x86)\Tesseract-OCR\tessdata";
            string language = "eng";
            OcrEngineMode oem = OcrEngineMode.DEFAULT;
            PageSegmentationMode psm = PageSegmentationMode.SINGLE_BLOCK;

            _tessBaseAPI = new TessBaseAPI();

            // Initialize tesseract-ocr 
            if (!_tessBaseAPI.Init(dataPath, language, oem, null, new string[] { "load_system_dawg", "load_freq_dawg", "user_patterns_suffix" }, new string[] { "F", "F", "user-patterns" }))
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

        public ClickPoint[] ParseImage(string imagePath, string outputDirectory)
        {
            // Set the input image
            Pix pix = _tessBaseAPI.SetImage(imagePath);

            // Recognize image
            _tessBaseAPI.Recognize();

            ResultIterator resultIterator = _tessBaseAPI.GetIterator();

            // Extract text from result iterator
            StringBuilder stringBuilder = new StringBuilder();
            PageIteratorLevel pageIteratorLevel = PageIteratorLevel.RIL_WORD;
            var clickPoints = new List<ClickPoint>();
            do
            {
                var word = resultIterator.GetUTF8Text(pageIteratorLevel);
                var debug = resultIterator.BoundingBox(PageIteratorLevel.RIL_WORD, out var left, out var top, out var right, out var bottom);
                if (word.Contains('[') && !word.EndsWith("]"))
                    clickPoints.Add(new ClickPoint() { X = right - 10, Y = (top + bottom) / 2 });
                if (left < 10)
                    stringBuilder.AppendLine();
                stringBuilder.Append(word);
            } while (resultIterator.Next(pageIteratorLevel));

            pix.Dispose();

            var file = new FileInfo(imagePath);
            File.WriteAllText(Path.Combine(outputDirectory, file.Name + ".txt"), stringBuilder.ToString());
            return clickPoints.ToArray();
        }
    }
}
