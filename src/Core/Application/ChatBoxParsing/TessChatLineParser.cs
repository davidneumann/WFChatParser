using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using Application.Data;
using Application.LineParseResult;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Tesseract;

namespace Application.ChatBoxParsing
{
    public class TessChatLineParser : BaseChatLineParser, IChatLineParser
    {
        private TesseractEngine _engine = null;

        public TessChatLineParser() : base()
        {

            string dataPath = DataHelper.TessDataPath;
            string language = "chi_sim+eng";
            _engine = new TesseractEngine(dataPath, language, EngineMode.Default, "bazaar");
            _engine.DefaultPageSegMode = PageSegMode.SingleLine;
        }

        public BaseLineParseResult ParseLine(Bitmap lineImage)
        {
            var image = lineImage;
            if(lineImage.Height < 52)
            {
                using (var mem = new MemoryStream())
                {
                    lineImage.Save(mem, System.Drawing.Imaging.ImageFormat.Png);
                    mem.Seek(0, SeekOrigin.Begin);
                    using (Image<Rgba32> rgbImage = SixLabors.ImageSharp.Image.Load(mem))
                    {
                        var scale = 52f / rgbImage.Height;
                        var width = (int)Math.Round(scale * rgbImage.Width);
                        var height = (int)Math.Round(scale * rgbImage.Height);
                        rgbImage.Mutate(m => m.Resize(width, height).EntropyCrop().Pad(width + 40, height + 40).BackgroundColor(Rgba32.White));


                        mem.Seek(0, SeekOrigin.Begin);
                        mem.SetLength(0);
                        rgbImage.SaveAsPng(mem);
                    }
                    mem.Seek(0, SeekOrigin.Begin);
                    image = new Bitmap(mem);
                }
            }

            var rawBuilder = new StringBuilder();
            var enhancedBuilder = new StringBuilder();
            var clickPoints = new List<ClickPoint>();
            var lastBox = Rect.Empty;
            using (var pix = PixConverter.ToPix(image))
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
                                string word = iter.GetText(pageIteratorLevel).Trim();
                                rawBuilder.Append(word + " ");
                                Rect box = Rect.Empty;
                                if (iter.TryGetBoundingBox(pageIteratorLevel, out box))
                                {
                                    lastBox = box;
                                }

                                //Check for end of riven
                                if (word.Length > 0 && word[0] != '[' && word.IndexOf(']') > 0)
                                {
                                    var affix = word.Substring(0, word.IndexOf(']') + 1).ToLower();
                                    if (affix.IndexOf('-') > 0)
                                        affix = affix.Substring(affix.IndexOf('-') + 1);
                                    if (_affixes.BinarySearch(affix) > 0)
                                    {
                                        //End of riven found we need to add a clickpoint
                                        var rivenName = rawBuilder.ToString().Trim();
                                        var startIndex = rivenName.LastIndexOf('[') + 1;
                                        var endIndex = rivenName.IndexOf(']', startIndex);
                                        rivenName = rivenName.Substring(startIndex, endIndex - startIndex).Trim();
                                        rivenName = rivenName.Substring(0, rivenName.LastIndexOf(' ')).Replace(" ", "") + rivenName.Substring(rivenName.LastIndexOf(' '));
                                        clickPoints.Add(new ClickPoint()
                                        {
                                            Index = clickPoints.Count,
                                            RivenName = rivenName,
                                            X = box != Rect.Empty ? box.X1 : lastBox.X2,
                                            Y = box != Rect.Empty ? (box.Y1 + box.Y2) / 2 : (lastBox.Y1 + lastBox.Y2) / 2
                                        });
                                        enhancedBuilder.Append(word.Substring(0, word.IndexOf(']') + 1)
                                            + "(" + (clickPoints.Count - 1) + ")"
                                            + word.Substring(word.IndexOf(']') + 1) + " ");
                                    }
                                    else
                                        enhancedBuilder.Append(word + " ");
                                }
                                else
                                    enhancedBuilder.Append(word + " ");
                            }
                            catch { continue; }
                        } while (iter.Next(pageIteratorLevel));
                    }
                }
            }

            if (image != lineImage)
                image.Dispose();
            
            return new ChatMessageLineResult()
            {
                ClickPoints = clickPoints,
                EnhancedMessage = enhancedBuilder.ToString().Trim(),
                LineType = LineType.Unknown,
                RawMessage = rawBuilder.ToString().Trim(),
                Timestamp = null,
                Username = null,
            };
        }
    }
}
