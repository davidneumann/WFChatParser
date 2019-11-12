using Application.Data;
using Application.Enums;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace ImageOCR.ComplexRivenParser
{
    internal class CharacterParser
    {
        private TesseractEngine _engine = null;
        private ClientLanguage _language;

        public CharacterParser(ClientLanguage clientLanguage)
        {
            _language = clientLanguage;
            string dataPath = DataHelper.TessDataPath;
            string language = "chi_sim+eng";
            if (_language == ClientLanguage.English)
                language = "eng";
            _engine = new TesseractEngine(dataPath, language, EngineMode.Default, "bazaar");
            _engine.DefaultPageSegMode = PageSegMode.SingleChar;
        }

        public string ParseCharacter(Bitmap bitmap)
        {
            var sb = new StringBuilder();
            using (var pix = PixConverter.ToPix(bitmap))
            {
                PageIteratorLevel pageIteratorLevel = PageIteratorLevel.Symbol;
                using (var page = _engine.Process(pix))
                {
                    using (var iter = page.GetIterator())
                    {
                        do
                        {
                            try
                            {
                                sb.AppendLine(iter.GetText(pageIteratorLevel).Trim());
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
