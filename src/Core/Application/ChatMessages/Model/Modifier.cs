using Application.Enums;
using Newtonsoft.Json;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Application.ChatMessages.Model
{
    public class Modifier
    {
        public float Value { get; set; }
        public string Description { get; set; }
        public bool Curse { get; set; }

        private static Regex _valRegx = new Regex(@"([-+]?[\d\.]+)");
        private static string[] _possibleDescriptions = new string[] { "% Puncture", "% Melee Combo Efficiency", "Initial Combo", "% Toxin", "% Damage to Corpus", "% Critical Damage", "% Channeling Damage", "% Cold", "% Magazine Capacity", "% Damage", "% Reload Speed", "% Damage to Infested", "% Slash", "% Status Chance", "% Ammo Maximum", "% Multishot", "% Projectile Speed", "Punch Through", "% Weapon Recoil", "% Damage to Grineer", "% Impact", "% Status Duration", "% Fire Rate(x2 for Bows)", "% Critical Chance for Slide Attack", "% Fire Rate", "% Critical Chance", "% Zoom", "% Electricity", "Range", "% Finisher Damage", "% Melee Damage", "% Heat", "% Attack Speed", "s Combo Duration", "% Channeling Efficiency", "% Chance to gain extra Combo Count" };

        private static bool _hasCheckedForUpdates = false;
            
        public Modifier()
        {
            if(!_hasCheckedForUpdates)
            {
                _hasCheckedForUpdates = true;
                using (var client = new WebClient())
                {
                    try
                    {
                        _possibleDescriptions = JsonConvert.DeserializeObject<string[]>(client.DownloadString("https://10o.io/rivens/buffnames.json"));
                    }
                    catch
                    {

                    }
                }
            }
        }

        public static Modifier ParseString(string modifier, ClientLanguage clientLanguage)
        {
            var result = new Modifier();
            var val = 0f;
            float.TryParse(_valRegx.Match(modifier).Value, out val);
            result.Value = val;
            var roughDesc = _valRegx.Replace(modifier, "").TrimEnd();
            var description = roughDesc;
            if (clientLanguage == ClientLanguage.English)
                description = _possibleDescriptions.OrderBy(desc => Utils.LevenshteinDistance.Compute(roughDesc, desc)).First();
            else if (clientLanguage == ClientLanguage.Chinese)
                description = roughDesc.Replace(" ", "");
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