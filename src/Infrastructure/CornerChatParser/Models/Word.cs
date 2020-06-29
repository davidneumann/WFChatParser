using Application.ChatLineExtractor;
using System;
using System.Collections.Generic;
using System.Text;

namespace RelativeChatParser.Models
{
    public class Word
    {
        public ChatColor WordColor;
        public List<Letter> Letters = new List<Letter>();

        public static Word MakeSpaceWord(int spaces)
        {
            var word = new Word();
            word.WordColor = ChatColor.Unknown;
            var space = new Letter(new FuzzyGlyph() { Character = " " }, null);
            for (int i = 0; i < Math.Max(1, spaces); i++)
            {
                word.Letters.Add(space);
            }

            return word;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var letter in Letters)
            {
                sb.Append(letter);
            }
            return sb.ToString();
        }
    }
}
