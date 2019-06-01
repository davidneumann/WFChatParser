using Application.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace ImageOCR
{
    public class LineParser
    {
        private TesseractEngine _engine = null;

        public LineParser()
        {
            string dataPath =  DataHelper.TessDataPath;
            string language = "chi_sim+eng";
            _engine = new TesseractEngine(dataPath, language, EngineMode.Default, "bazaar");
            _engine.DefaultPageSegMode = PageSegMode.SingleLine;
        }

        public string ParseLine(Bitmap bitmap)
        {
            var sb = new StringBuilder();
            using (var pix = PixConverter.ToPix(bitmap))
            {
                PageIteratorLevel pageIteratorLevel = PageIteratorLevel.TextLine;
                using (var page = _engine.Process(pix))
                {
                    using (var iter = page.GetIterator())
                    {
                        do
                        {
                            try
                            {
                                sb.Append(iter.GetText(pageIteratorLevel).Trim());
                            }
                            catch { continue; }
                        } while (iter.Next(pageIteratorLevel));
                    }
                }
            }
            return sb.ToString().Trim();
        }
    }
}
