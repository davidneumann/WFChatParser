using System;

namespace Application.ChatMessages.Model
{
    public class Riven
    {
        public int MessagePlacementId { get; set; }
        public int Drain { get; set; }
        public Polarity Polarity { get; set; }
        public int Rank { get; set; }
        public Modifier[] Modifiers { get; set; }
        public int MasteryRank { get; set; }
        public int Rolls { get; set; }
        public string Name { get; set; }
        public Guid ImageId { get; set; } = Guid.Empty;
    }
}