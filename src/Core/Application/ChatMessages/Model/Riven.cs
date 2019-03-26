namespace Application.ChatMessages.Model
{
    public class Riven
    {
        public int MessagePlacementId { get; set; }
        public string Drain { get; set; }
        public Polarity Polarity { get; set; }
        public string Rank { get; set; }
        public string[] Modifiers { get; set; }
        public string MasteryRank { get; set; }
        public string Rolls { get; set; }
        public string Name { get; set; }
    }
}