using System;

namespace Application.LogParser
{
    public class LogMessage
    {
        public double? Timer { get; set; }
        public string Category { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public DateTimeOffset ActualTime { get; set; }

        public override string ToString()
        {
            return Timer.HasValue ? $"{Timer.Value:F3} {Category} [{Level}]: {Message}" : $"{Category} [{Level}]: {Message}";
        }
    }
}