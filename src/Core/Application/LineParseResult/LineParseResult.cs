using System;
using System.Collections.Generic;
using System.Text;

namespace Application.LineParseResult
{
    public class LineParseResult
    {
        public string RawMessage { get; set; }
        public string Message { get; set; }
        public List<ClickPoint> ClickPoints { get; set; }

        public void Append(LineParseResult lineParseResult)
        {
            this.RawMessage = this.RawMessage.Trim();
            lineParseResult.RawMessage = lineParseResult.RawMessage.Trim();
            this.Message = this.Message.Trim();
            lineParseResult.Message = lineParseResult.Message.Trim();

            this.RawMessage += " " + lineParseResult.RawMessage;
            var message = lineParseResult.Message;
            var addedRivens = 0;
            for (int i = 0; i < message.Length;)
            {
                if (message[i] == '[' && i + 1 < message.Length && Char.IsDigit(message[i + 1]))
                {
                    var id = Int32.Parse(message.Substring(i + 1, message.IndexOf(']', i + 1) - i - 1));
                    var newId = this.ClickPoints.Count + addedRivens;
                    message = message.Replace("[" + id + "]", "[" + newId + "]");
                    var p = lineParseResult.ClickPoints[addedRivens];
                    lineParseResult.ClickPoints[0] = new ClickPoint() { Index = this.Message.Length + i + 1, X = p.X, Y = p.Y };
                    i = i + ("[" + newId + "]").ToString().Length;
                    addedRivens++;
                }
                else
                    i++;
            }
            this.ClickPoints.AddRange(lineParseResult.ClickPoints);
            this.Message += " " + message;
        }
    }
}
