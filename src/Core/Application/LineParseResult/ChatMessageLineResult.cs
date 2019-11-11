using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;

namespace Application.LineParseResult
{
    public class ChatMessageLineResult : BaseLineParseResult
    {
        public string Username { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string EnhancedMessage { get; set; }
        public List<ClickPoint> ClickPoints { get; set; }

        public Rectangle MessageBounds { get; set; }
        public void Append(ChatMessageLineResult wrappedLine, int lineHeight, int lineOffset)
        {
            if (MessageBounds.Bottom + lineHeight > lineOffset)
            {
                MessageBounds = new System.Drawing.Rectangle(MessageBounds.Left, MessageBounds.Y, MessageBounds.Width, 
                    (lineOffset + lineHeight) - MessageBounds.Top);
            }
            //Trim all lines
            this.RawMessage = this.RawMessage.Trim();
            wrappedLine.RawMessage = wrappedLine.RawMessage.Trim();
            this.EnhancedMessage = this.EnhancedMessage.Trim();
            wrappedLine.EnhancedMessage = wrappedLine.EnhancedMessage.Trim();

            //Combine raw messages
            this.RawMessage += " " + wrappedLine.RawMessage;

            var wrappedEnhanced = wrappedLine.EnhancedMessage;
            var rivenRegex = new Regex(@"(\[[^\]]+\])\((\d+)\)");
            //Find all rivens in enhanced message
            var m = rivenRegex.Matches(wrappedEnhanced);
            for (int i = 0; i < m.Count; i++)
            {
                //Ex: [Gammacor Acri-visican](0)
                //Group[1] = rivenname + square brackets
                //Group[2] = only index inside of paren
                var newId = int.Parse(m[i].Groups[2].Value) + this.ClickPoints.Count;
                var riven = m[i].Groups[1] + "(" + (newId) + ")";
                //Replace entire riven with new riven string
                wrappedEnhanced = wrappedEnhanced.Substring(0, m[i].Index) + riven + wrappedEnhanced.Substring(m[i].Index + m[i].Length);
                //Update clickpoint ids to account for new positions
                var cp = wrappedLine.ClickPoints[i];
                cp.Index = newId;
                this.ClickPoints.Add(cp);
            }
            this.EnhancedMessage += " " + wrappedEnhanced;
        }

        public override string GetKey()
        {
            return Timestamp + Username;
        }

        public override bool KeyReady()
        {
            return this.Timestamp != string.Empty & this.Username != string.Empty;
        }
    }
}
