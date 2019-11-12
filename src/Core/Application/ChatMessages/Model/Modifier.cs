using Application.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
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
        //private static string[] _possibleEnglishDescriptions = new string[] { "% Puncture", "% Melee Combo Efficiency", "Initial Combo", "% Toxin", "% Damage to Corpus", "% Critical Damage", "% Channeling Damage", "% Cold", "% Magazine Capacity", "% Damage", "% Reload Speed", "% Damage to Infested", "% Slash", "% Status Chance", "% Ammo Maximum", "% Multishot", "% Projectile Speed", "Punch Through", "% Weapon Recoil", "% Damage to Grineer", "% Impact", "% Status Duration", "% Fire Rate(x2 for Bows)", "% Critical Chance for Slide Attack", "% Fire Rate", "% Critical Chance", "% Zoom", "% Electricity", "Range", "% Finisher Damage", "% Melee Damage", "% Heat", "% Attack Speed", "s Combo Duration", "% Channeling Efficiency", "% Chance to gain extra Combo Count" };
        private static Dictionary<string, string[]> _possibleDescriptions = new Dictionary<string, string[]>();
        public static IReadOnlyDictionary<string, string[]> PossibleDescriptions { get { return (IReadOnlyDictionary<string, string[]>)_possibleDescriptions; } }
        private static bool _hasCheckedForUpdates = false;
            
        static Modifier()
        {
            _possibleDescriptions["en"] = new string[] { "% Puncture", "% Melee Combo Efficiency", "Initial Combo", "% Toxin", "% Damage to Corpus", "% Critical Damage", "% Channeling Damage", "% Cold", "% Magazine Capacity", "% Damage", "% Reload Speed", "% Damage to Infested", "% Slash", "% Status Chance", "% Ammo Maximum", "% Multishot", "% Projectile Speed", "Punch Through", "% Weapon Recoil", "% Damage to Grineer", "% Impact", "% Status Duration", "% Fire Rate(x2 for Bows)", "% Critical Chance for Slide Attack", "% Fire Rate", "% Critical Chance", "% Zoom", "% Electricity", "Range", "% Finisher Damage", "% Melee Damage", "% Heat", "% Attack Speed", "s Combo Duration", "% Channeling Efficiency", "% Chance to gain extra Combo Count" };
            _possibleDescriptions["zh"] = new string[] { "% 近战伤害", "% 穿刺伤害", "% 冲击伤害", "% 切割伤害", "% 暴击几率", "% 暴击伤害", "% 电击伤害", "% 火焰伤害", "% 冰冻伤害", "% 毒素伤害", "% 触发时间", "% 对 Corpus 伤害", "% 对 Grineer 伤害", "% 对 Infested 伤害", "% 攻击速度", "% 触发几率", "秒连击持续时间", "% 导引伤害", "% 导引效率", "滑行攻击 % 暴击几率。", "攻击范围", "% 处决伤害", "% 射速（弓类武器效果加倍）", "% 弹药最大值", "% 弹匣容量", "% 伤害", "% 多重射击", "% 投射物速度", "穿透", "% 武器后坐力", "% 装填速度", "% 变焦", "% 射速", "% 近战连击效率", "初始连击", "% 不获得连击几率", "% 额外连击数几率", };

            if (!_hasCheckedForUpdates)
            {
                _hasCheckedForUpdates = true;
                using (var client = new WebClient())
                {
                    try
                    {
                        var fullJson = JObject.Parse(client.DownloadString("https://10o.io/rivens/rivendata.json"));
                        _possibleDescriptions["en"] = fullJson["buffs"].Cast<KeyValuePair<string, JToken>>().Select(p => (string)p.Value["text"]["en"]).Select(str => str.Replace("|val|", "").Trim()).Distinct().ToArray();
                        _possibleDescriptions["zh"] = fullJson["buffs"].Cast<KeyValuePair<string, JToken>>().Select(p => (string)p.Value["text"]["zh"]).Select(str => str.Replace("|val|", "").Trim()).Distinct().ToArray();
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
                description = _possibleDescriptions["en"].OrderBy(desc => Utils.LevenshteinDistance.Compute(roughDesc, desc)).First();
            else if (clientLanguage == ClientLanguage.Chinese)
            {
                roughDesc = roughDesc.Trim();
                if (roughDesc.StartsWith("% "))
                    description = $"% {roughDesc.Substring(2).Replace(" ", "").Replace("(", "（").Replace(")", "）").Trim()}";
                else
                    description = roughDesc.Replace(" ", "").Replace("(", "（").Replace(")", "）").Trim();
            }
            result.Description = description;
            return result;
        }

        public override string ToString()
        {
            return (Value > 0 ? "+": "") + Value + Description;
        }
    }
}