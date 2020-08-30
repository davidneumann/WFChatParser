using System.Collections.Generic;

namespace Application.Actionables.ProfileBots.Models
{
    public class ProfileStats
    {
        public Dictionary<string, List<ProfileStatLineItem>> StatGroups { get; set; } = new Dictionary<string, List<ProfileStatLineItem>>();
    }
}