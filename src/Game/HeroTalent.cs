using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// A hero's talent, the way the rest of the mod should ask for it.
    ///
    /// Two sources. A ruler or clan leader TaleWorlds wrote carries a template
    /// made for his station, and it says how formidable he was meant to be (see
    /// AuthoredTalent). Everyone else -- lords on a shared archetype, the heroes
    /// a campaign makes as it goes, and any domain a template leaves blank --
    /// takes the id-seeded hash, as every hero did before. Asked in one place so
    /// growth, seeding and the census can never disagree about the same man.
    ///
    /// Only original characters count. The game makes children and wanderers by
    /// copying a template character, so a copy's template speaks for every hero
    /// stamped from it rather than for him; CharacterObject.IsOriginalCharacter
    /// is the game's own word for the difference.
    ///
    /// Read from the character's default skills, which the game keeps apart from
    /// the skills the hero actually has: CharacterObject.GetSkillValue answers
    /// from the living hero, while the defaults hold what the template wrote,
    /// untouched by anything that happens after. That is why the answer never
    /// drifts and needs nothing stored.
    /// </summary>
    public static class HeroTalent
    {
        private const int CombatIndex = 0;
        private const int CivilIndex = 1;
        private const int NavalIndex = 2;

        /// <summary>Authored talent per hero id and domain; zero where the template says nothing.</summary>
        private static readonly Dictionary<string, float[]> _authored = new Dictionary<string, float[]>();

        /// <summary>Called when a campaign is loaded: another save holds other characters.</summary>
        public static void ResetSession()
        {
            _authored.Clear();
        }

        /// <summary>Combat talent, the bare overload Talent.For has always had.</summary>
        public static float For(Hero hero)
        {
            return For(hero, Talent.Combat);
        }

        /// <summary>The hero's talent in one domain: what TaleWorlds wrote, or the hash.</summary>
        public static float For(Hero hero, string domain)
        {
            if (hero == null) return Talent.For(null, domain);

            float authored = Authored(hero)[IndexOf(domain)];
            return authored > 0f ? authored : Talent.For(hero.StringId, domain);
        }

        /// <summary>Whether any of this hero's talent comes from a template.</summary>
        public static bool IsAuthored(Hero hero)
        {
            if (hero == null) return false;

            float[] values = Authored(hero);
            return values[CombatIndex] > 0f || values[CivilIndex] > 0f || values[NavalIndex] > 0f;
        }

        /// <summary>Which domains were read from a template, for the census.</summary>
        public static string SourceOf(Hero hero)
        {
            if (hero == null) return "hash";

            float[] values = Authored(hero);
            List<string> parts = new List<string>();
            if (values[CombatIndex] > 0f) parts.Add(Talent.Combat);
            if (values[CivilIndex] > 0f) parts.Add(Talent.Civil);
            if (values[NavalIndex] > 0f) parts.Add(Talent.Naval);
            return parts.Count == 0 ? "hash" : "authored(" + string.Join(",", parts.ToArray()) + ")";
        }

        /// <summary>
        /// The id of the skill template TaleWorlds wrote this character on, or
        /// null when he has none or is a copy. The census reads it to show which
        /// sheet each authored talent came from, and how many were set aside.
        /// </summary>
        internal static string TemplateOf(Hero hero)
        {
            MBCharacterSkills template = TemplateSkillsOf(hero);
            return template != null ? template.StringId : null;
        }

        /// <summary>
        /// A character written without a template still has default skills: the
        /// game makes him a set of his own under his own id
        /// (BasicCharacterObject.Deserialize falls back to CreateObject with the
        /// character's StringId). A set named after the man himself is therefore
        /// no template at all.
        /// </summary>
        private static MBCharacterSkills TemplateSkillsOf(Hero hero)
        {
            CharacterObject character = hero != null ? hero.CharacterObject : null;
            if (character == null || !character.IsOriginalCharacter) return null;

            MBCharacterSkills defaults = character.GetDefaultCharacterSkills();
            if (defaults == null || defaults.StringId == character.StringId) return null;
            return defaults;
        }

        private static int IndexOf(string domain)
        {
            if (domain == Talent.Civil) return CivilIndex;
            if (domain == Talent.Naval) return NavalIndex;
            return CombatIndex;
        }

        private static float[] Authored(Hero hero)
        {
            string id = hero.StringId;
            if (id == null) return Read(hero);

            float[] values;
            if (_authored.TryGetValue(id, out values)) return values;

            values = Read(hero);
            _authored[id] = values;
            return values;
        }

        private static float[] Read(Hero hero)
        {
            float[] values = new float[3];
            try
            {
                MBCharacterSkills defaults = TemplateSkillsOf(hero);
                if (defaults == null || defaults.Skills == null) return values;

                // An archetype speaks for every lord stamped from it, not for him.
                if (!AuthoredTalent.IsLeaderTemplate(defaults.StringId)) return values;

                int combat = 0, civil = 0, naval = 0;
                foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
                {
                    if (skill == null) continue;

                    string domain = AuthoredTalent.DomainOf(skill.StringId);
                    if (domain == null) continue;

                    int value = defaults.Skills.GetPropertyValue(skill);
                    if (domain == Talent.Combat)
                    {
                        if (value > combat) combat = value;
                    }
                    else if (domain == Talent.Naval)
                    {
                        if (value > naval) naval = value;
                    }
                    else if (value > civil)
                    {
                        civil = value;
                    }
                }

                values[CombatIndex] = AuthoredTalent.From(combat, Talent.Combat);
                values[CivilIndex] = AuthoredTalent.From(civil, Talent.Civil);
                values[NavalIndex] = AuthoredTalent.From(naval, Talent.Naval);
            }
            catch
            {
                // A character the game cannot describe keeps the hash, which is
                // what every hero had before. A failed read must not take the
                // weekly growth pass down with it.
            }
            return values;
        }
    }
}
