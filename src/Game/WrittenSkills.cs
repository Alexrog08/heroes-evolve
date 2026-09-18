using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// The highest figure TaleWorlds wrote anybody in each skill: the envelope
    /// he balanced the game inside.
    ///
    /// Asked because this mod can multiply past it without noticing. A target is
    /// the peak norm times a talent, and for a skill a lord has focus in it is
    /// that again times his focus -- two multipliers compounding. A well-invested
    /// steward reached 380 that way, where the highest stewardship TaleWorlds
    /// wrote any lord is 240 and no hero of his holds more than 250 in any civil
    /// skill at all. The engine would display it; the game was never balanced for
    /// it, and the armies a steward feeds are balanced against his sheet.
    ///
    /// Read from the heroes themselves rather than written down as a constant.
    /// A mod with a stronger roster raises the envelope with it, and a skill
    /// another mod adds that nobody has been written any of keeps it at zero,
    /// which leaves its heroes at their own talent and no further.
    ///
    /// Read through the game's own SkillObject rather than by name, which is
    /// not fussiness: Smithing is called Crafting in the data, and reading the
    /// sheets by the name on screen answers that no lord in Calradia has ever
    /// forged anything, when 458 of them were written with it and Peric holds
    /// 240.
    ///
    /// The vanilla envelope, for reference: One Handed, Two Handed and Polearm
    /// 300 (Caladog in all three), Bow 260, Crossbow and Throwing 200, Riding
    /// 230, Athletics 240; Trade, Medicine and Engineering 250, Tactics,
    /// Roguery, Charm, Steward, Leadership and Crafting 240, Scouting 230;
    /// Shipmaster 280, Mariner 270, Boatswain 260. Every civil skill lands
    /// between 230 and 250, which is the envelope this class exists to hold.
    /// </summary>
    public static class WrittenSkills
    {
        private static Dictionary<string, int> _highest;
        private static int _heroes;

        /// <summary>Called when a campaign is loaded: another save holds another roster.</summary>
        public static void ResetSession()
        {
            _highest = null;
            _heroes = 0;
        }

        /// <summary>
        /// The most TaleWorlds wrote anyone in this skill, or zero where he wrote
        /// nobody any of it.
        /// </summary>
        public static int HighestIn(SkillObject skill)
        {
            if (skill == null || skill.StringId == null) return 0;

            Build();

            int value;
            return _highest.TryGetValue(skill.StringId, out value) ? value : 0;
        }

        /// <summary>The whole envelope on one line, and how many heroes it was read from.</summary>
        public static string Describe()
        {
            Build();

            StringBuilder text = new StringBuilder();
            text.Append("heroes=").Append(_heroes);
            foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
            {
                if (skill == null || skill.StringId == null) continue;

                int value;
                _highest.TryGetValue(skill.StringId, out value);
                text.Append(' ').Append(skill.StringId).Append('=').Append(value);
            }
            return text.ToString();
        }

        private static void Build()
        {
            if (_highest != null) return;

            _highest = new Dictionary<string, int>();
            foreach (CharacterObject character in CharacterObject.All)
            {
                try
                {
                    if (character == null || !character.IsHero) continue;

                    // Originals only. The game stamps children and wanderers from
                    // a template character, and a copy carries that template's
                    // sheet, so counting copies would weigh one written sheet
                    // once per hero made from it -- and in a long campaign the
                    // copies outnumber the originals.
                    if (!character.IsOriginalCharacter) continue;

                    MBCharacterSkills sheet = character.GetDefaultCharacterSkills();
                    if (sheet == null || sheet.Skills == null) continue;

                    _heroes++;
                    foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
                    {
                        if (skill == null || skill.StringId == null) continue;

                        int value = sheet.Skills.GetPropertyValue(skill);

                        int highest;
                        if (_highest.TryGetValue(skill.StringId, out highest) && highest >= value) continue;
                        _highest[skill.StringId] = value;
                    }
                }
                catch
                {
                    // One unreadable character must not cost the envelope.
                }
            }
        }
    }
}
