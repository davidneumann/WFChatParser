using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Actionables.ProfileBots.Models
{
    public class Profile
    {
        public string Name { get; set; }
        public List<Accolades> Accolades = new List<Accolades>();
        public byte MasteryRank { get; set; }
        public uint TotalXp { get; set; }
        public uint XpToLevel { get; set; }
        public string ClanName { get; set; }

        public List<EquipmentItem> Equipment { get; set; }
        public ProfileStats ProfileStats { get; set; }
    }
}
