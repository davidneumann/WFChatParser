using Application.Data;
using Application.Enums;
using Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace TesseractService.Parsers
{
    public class LineParser : ILineParser
    {
        private TesseractEngine _engine = null;
        private ClientLanguage _language;

        public LineParser(ClientLanguage clientLanguage)
        {
            _language = clientLanguage;
            string dataPath =  DataHelper.TessDataPath;
            string language = "chi_sim+eng";
            if(_language == ClientLanguage.English)
                language = language = "eng";
            _engine = new TesseractEngine(dataPath, language, EngineMode.Default, "bazaar");
            _engine.DefaultPageSegMode = PageSegMode.SingleLine;
        }

        public void Dispose()
        {
            _engine.Dispose();
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
