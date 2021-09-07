using ParsingModel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RustRayRecognizer.Data
{
    public class GlyphPacket
    {
        ushort width;
        byte height;
        byte pixelsFromTopOfLine;
        byte[] contents;

        public GlyphPacket(IExtractedGlyph glyph)
        {
            var arr = new bool[glyph.Width, glyph.Height];
            var startX = glyph.Width - 1;
            var endX = 0;
            var startY = glyph.Height - 1;
            var endY = 0;
            for (int y = 0; y < glyph.Height; y++)
            {
                for (int x = 0; x < glyph.Width; x++)
                {
                    arr[x, y] = glyph.RelativeBrights.Any(p => p.X == x && p.Y == y);
                    if (arr[x, y])
                    {
                        startX = Math.Min(startX, x);
                        endX = Math.Max(endX, x);
                        startY = Math.Min(startY, y);
                        endY = Math.Max(endY, y);
                    }
                }
            }

            this.width = (ushort)(endX - startX + 1);
            this.height = (byte)(endY - startY + 1);
            this.pixelsFromTopOfLine = (byte)glyph.PixelsFromTopOfLine;

            var trimmedArr = new bool[width, height];
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    trimmedArr[x - startX, y - startY] = arr[x, y];
                }
            }

            var packedBytesLen = height * width / 8;
            if (height * width % 8 != 0)
                packedBytesLen++;
            this.contents = new byte[packedBytesLen];
            for (int i = 0; i < packedBytesLen; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    var x = (i * 8 + j) % width;
                    var y = (i * 8 + j) / width;
                    if (y >= height)
                        break;
                    if (trimmedArr[x, y])
                        contents[i] |= (byte)(1 << (7 - j));
                }
            }
        }

        public void Send(BinaryWriter stream)
        {
            stream.Write((ushort)width);
            stream.Write((byte)height);
            stream.Write((byte)pixelsFromTopOfLine);
            stream.Write(contents);
        }

        public void Save(string filename, IExtractedGlyph glyph)
        {
            using (var fout = new StreamWriter(filename + ".dat"))
            {
                fout.Write(contents);
            }

            var arr = new bool[glyph.Width, glyph.Height];
            var startX = glyph.Width - 1;
            var endX = 0;
            var startY = glyph.Height - 1;
            var endY = 0;
            for (int y = 0; y < glyph.Height; y++)
            {
                for (int x = 0; x < glyph.Width; x++)
                {
                    arr[x, y] = glyph.RelativeBrights.Any(p => p.X == x && p.Y == y);
                    if (arr[x, y])
                    {
                        startX = Math.Min(startX, x);
                        endX = Math.Max(endX, x);
                        startY = Math.Min(startY, y);
                        endY = Math.Max(endY, y);
                    }
                }
            }
            var trimmedArr = new bool[width, height];
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    trimmedArr[x - startX, y - startY] = arr[x, y];
                }
            }
            using (var b = new Bitmap(width, height))
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (trimmedArr[x, y])
                            b.SetPixel(x, y, Color.White);
                        else
                            b.SetPixel(x, y, Color.Black);
                    }
                }
                b.Save(filename + ".png");
            }
        }
    }

    public static class RustyDataTxRx
    {
        public static string ConnectionAddress { get; set; }
        public static int ConnectionPort { get; set; } = 3333;

        public static char[] ParseCharacters(IExtractedGlyph[] glyphs)
        {
            var _client = new TcpClient(ConnectionAddress, ConnectionPort);
            var _stream = _client.GetStream();
            var _fout = new BinaryWriter(_stream);
            var _fin = new BinaryReader(_stream);

            _fout.Write((ushort)glyphs.Length);

            foreach (var glyph in glyphs)
            {
                var packet = new GlyphPacket(glyph);
                packet.Send(_fout);
            }
            _fout.Flush();
            var response_count = _fin.ReadUInt16();
            var results = new char[response_count];
            Console.WriteLine($"Glyph recog response count: {response_count}");
            var sb = new StringBuilder();
            for (int i = 0; i < response_count; i++)
            {
                results[i] = _fin.ReadChar();
                sb.Append(results[i]);
            }
            Console.WriteLine(sb.ToString());

            _client.Dispose();
            _stream.Dispose();
            _fout.Dispose();
            _fin.Dispose();

            return results;
        }
    }
}
