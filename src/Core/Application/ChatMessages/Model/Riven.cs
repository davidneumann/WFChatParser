using System;

namespace Application.ChatMessages.Model
{
    public class Riven
    {
        public int MessagePlacementId { get; set; }
        public int Drain { get; set; } = -1;
        public Polarity Polarity { get; set; }
        public int Rank { get; set; } = -1;
        public Modifier[] Modifiers { get; set; }
        public int MasteryRank { get; set; } = -1;
        public int Rolls { get; set; } = -1;
        public string Name { get; set; }
        public Guid ImageId { get; set; } = Guid.Empty;
    }
}