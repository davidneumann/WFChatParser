using System.Linq;
using System.Text.RegularExpressions;

namespace Application.ChatMessages.Model
{
    public class Modifier
    {
        public float Value { get; set; }
        public string Description { get; set; }
        public bool Curse { get; set; }

        private static Regex _valRegx = new Regex(@"([-+]?[\d\.]+)");
        private static string[] _possibleDescriptions = new string[] { "% Zoom", "% Weapon Recoil", "% Toxin", "% Status Duration", "% Status Chance", "% Critical Hit Chance for Slide Attack", "% Slash", "s Combo Duration", "% Reload Speed", "% Range", "% Puncture", "Punch Through", "% Projectile Speed", "% Multishot", "% Melee Damage", "% Magazine Capacity", "% Impact", "% Heat", "% Fire Rate(x2 for Bows)", "% Fire Rate", "% Finisher Damage", "% Electricity", "% Damage to Infested", "% Damage to Grineer", "% Damage to Corpus", "% Damage", "% Critical Damage", "% Critical Chance", "% Cold", "% Channeling Efficiency", "% Channeling Damage", "% Attack Speed", "% Ammo Maximum" };

        public static Modifier ParseString(string modifier, bool english = true)
        {
            var result = new Modifier();
            var val = 0f;
            float.TryParse(_valRegx.Match(modifier).Value, out val);
            result.Value = val;
            var roughDesc = _valRegx.Replace(modifier, "").TrimEnd();
            var description = english ? _possibleDescriptions.OrderBy(desc => Utils.LevenshteinDistance.Compute(roughDesc, desc)).First() : roughDesc;
            result.Description = description;
            return result;
        }

        public override string ToString()
        {
            return (Value > 0 ? "+": "") + Value + Description;
        }

        public static void OverrideDescriptions(string[] newDescriptions)
        {
            _possibleDescriptions = newDescriptions;
        }
    }
}