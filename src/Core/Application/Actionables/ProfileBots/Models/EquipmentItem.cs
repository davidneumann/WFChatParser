namespace Application.Actionables.ProfileBots.Models
{
    public class EquipmentItem
    {
        public string Name { get; set; }
        public bool IsRanked { get; set; }
        public byte Rank { get; set; } = 0;
    }
}