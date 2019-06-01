using Application.Data;
using System.Drawing;
using System.Text;
using Tesseract;

namespace ImageOCR
{
    public class WordParser
    {
        private TesseractEngine _engine = null;

        public WordParser()
        {
            string dataPath = DataHelper.TessDataPath;
            string language = "chi_sim+eng";
            _engine = new TesseractEngine(dataPath, language, EngineMode.Default, "bazaar");
            _engine.DefaultPageSegMode = PageSegMode.SingleWord;
        }

        public string ParseWord(Bitmap bitmap)
        {
            var sb = new StringBuilder();
            using (var pix = PixConverter.ToPix(bitmap))
            {
                PageIteratorLevel pageIteratorLevel = PageIteratorLevel.Word;
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