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
        }

        public void Dispose()
        {
        }

        public string ParseImage(string imagePath)
        {
            string dataPath = @"C:\Program Files (x86)\Tesseract-OCR\tessdata";
            string language = "eng";
            OcrEngineMode oem = OcrEngineMode.DEFAULT;
            PageSegmentationMode psm = PageSegmentationMode.SINGLE_BLOCK;

            var _tessBaseAPI = new TessBaseAPI();

            // Initialize tesseract-ocr 
            if (!_tessBaseAPI.Init(dataPath, language, oem, null, new string[] { "load_system_dawg", "load_freq_dawg" }, new string[] { "F", "F" }))
            {
                throw new Exception("Could not initialize tesseract.");
            }
            // Set the Page Segmentation mode
            _tessBaseAPI.SetPageSegMode(psm);

            // Set the input image
            Pix pix = _tessBaseAPI.SetImage(imagePath);

            // Recognize image
            _tessBaseAPI.Recognize();

            ResultIterator resultIterator = _tessBaseAPI.GetIterator();

            // Extract text from result iterator
            StringBuilder stringBuilder = new StringBuilder();
            PageIteratorLevel pageIteratorLevel = PageIteratorLevel.RIL_PARA;
            do
            {
                stringBuilder.Append(resultIterator.GetUTF8Text(pageIteratorLevel));
            } while (resultIterator.Next(pageIteratorLevel));

            pix.Dispose();
            _tessBaseAPI.Dispose();
            
            return stringBuilder.ToString();
        }
    }
}
