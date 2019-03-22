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
    public class RivenParser : IDisposable
    {
        private TessBaseAPI _tessBaseAPI = null;

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

        public string[] ParseRivenImage(string imagePath)
        {
            // Set the input image
            Pix pix = _tessBaseAPI.SetImage(imagePath);

            // Recognize image
            _tessBaseAPI.Recognize();

            ResultIterator resultIterator = _tessBaseAPI.GetIterator();

            // Extract text from result iterator
            var rivenText = new List<string>();
            PageIteratorLevel pageIteratorLevel = PageIteratorLevel.RIL_SYMBOL;
            do
            {
                var r = resultIterator.GetUTF8Text(pageIteratorLevel);
                var c = resultIterator.Confidence(pageIteratorLevel);
                var line = resultIterator.GetUTF8Text(pageIteratorLevel) + " ";
                rivenText.Add(line);
            } while (resultIterator.Next(pageIteratorLevel));

            pix.Dispose();

            return rivenText.ToArray();
        }
    }
}
