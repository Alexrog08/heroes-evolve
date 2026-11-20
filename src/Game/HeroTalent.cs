using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// A hero's talent, the way the rest of the mod should ask for it.
    ///
    /// Two sources, and the higher of them answers. Every hero is dealt a talent
    /// by the hash of his own id; a character TaleWorlds wrote also has a sheet,
    /// and that sheet is a floor under what the dice gave him -- see
    /// AuthoredTalent for why. Asked in one place so growth, seeding, the
    /// campaign-start curve and the census can never disagree about the same man.
    ///
    /// Only original characters have a sheet of their own. The game makes
    /// children and wanderers by copying a template character, so a copy's sheet
    /// speaks for every hero stamped from it rather than for him;
    /// CharacterObject.IsOriginalCharacter is the game's own word for the
    /// difference, and a copy is left to the dice.
    ///
    /// Read from the character's default skills, which the game keeps apart from
    /// the skills the hero actually has: CharacterObject.GetSkillValue answers
    /// from the living hero, while the defaults hold what TaleWorlds wrote,
    /// untouched by anything that happens afterwards -- including this mod's own
    /// curve, which is what makes a floor possible at all. That is why the answer
    /// never drifts and needs nothing stored.
    ///
    /// A sheet is shared where several characters name the same one (a knight's,
    /// a chatelaine's, a ruler's) and his own where the character carries his
    /// skills inline, which BasicCharacterObject.Deserialize then keeps in a set
    /// named after him. Both are what TaleWorlds wrote for that man, and both
    /// are read the same way.
    /// </summary>
    public static class HeroTalent
    {
        private const int CombatIndex = 0;
        private const int CivilIndex = 1;
        private const int NavalIndex = 2;

        /// <summary>
        /// The best skill TaleWorlds wrote a hero in each field, per hero id:
        /// zero where his sheet says nothing there, and all zeroes for a hero
        /// with no sheet at all.
        ///
        /// Cached because reading it walks every skill in the game. What the
        /// dice say is two floats of arithmetic and is worked out per call.
        /// </summary>
        private static readonly Dictionary<string, int[]> _sheets = new Dictionary<string, int[]>();

        /// <summary>Called when a campaign is loaded: another save holds other characters.</summary>
        public static void ResetSession()
        {
            _sheets.Clear();
        }

        /// <summary>Combat aptitude, kept as the bare overload it has always been.</summary>
        public static float For(Hero hero)
        {
            return For(hero, Talent.Combat);
        }

        /// <summary>The hero's talent in one domain: his dice, never below his sheet.</summary>
        public static float For(Hero hero, string domain)
        {
            if (hero == null) return Talent.For(null, domain);

            return AuthoredTalent.AtLeastHisSheet(Talent.For(hero.StringId, domain),
                                                  Sheet(hero)[IndexOf(domain)],
                                                  SkillGrowth.PeakNormFor(domain));
        }

        /// <summary>Whether his sheet, rather than his dice, decides any of his talent.</summary>
        public static bool IsAuthored(Hero hero)
        {
            if (hero == null) return false;

            int[] sheet = Sheet(hero);
            return StandsOnSheet(hero, Talent.Combat, sheet[CombatIndex])
                || StandsOnSheet(hero, Talent.Civil, sheet[CivilIndex])
                || StandsOnSheet(hero, Talent.Naval, sheet[NavalIndex]);
        }

        /// <summary>Whether one field is his sheet's word rather than the dice's.</summary>
        public static bool StandsOnSheet(Hero hero, string domain)
        {
            if (hero == null) return false;

            return StandsOnSheet(hero, domain, Sheet(hero)[IndexOf(domain)]);
        }

        /// <summary>Which fields his sheet decided, for the census.</summary>
        public static string SourceOf(Hero hero)
        {
            if (hero == null) return "hash";

            int[] sheet = Sheet(hero);
            List<string> parts = new List<string>();
            if (StandsOnSheet(hero, Talent.Combat, sheet[CombatIndex])) parts.Add(Talent.Combat);
            if (StandsOnSheet(hero, Talent.Civil, sheet[CivilIndex])) parts.Add(Talent.Civil);
            if (StandsOnSheet(hero, Talent.Naval, sheet[NavalIndex])) parts.Add(Talent.Naval);
            return parts.Count == 0 ? "hash" : "sheet(" + string.Join(",", parts.ToArray()) + ")";
        }

        /// <summary>
        /// The best skill his sheet gives him in each field, for the census to
        /// print beside the talent it produced.
        /// </summary>
        public static int BestWritten(Hero hero, string domain)
        {
            if (hero == null) return 0;

            return Sheet(hero)[IndexOf(domain)];
        }

        /// <summary>
        /// What TaleWorlds wrote this hero in one particular skill, or zero when
        /// he has no sheet or nothing there.
        ///
        /// Read straight rather than cached: the sheet is two property lookups
        /// away and the callers ask once per skill per hero per week, where the
        /// per-field bests are a walk down every skill in the game and are worth
        /// keeping.
        /// </summary>
        public static int WrittenIn(Hero hero, SkillObject skill)
        {
            if (hero == null || skill == null) return 0;

            try
            {
                MBCharacterSkills sheet = SheetOf(hero);
                if (sheet == null || sheet.Skills == null) return 0;

                return sheet.Skills.GetPropertyValue(skill);
            }
            catch
            {
                // A character the game cannot describe simply has no floor here.
                return 0;
            }
        }

        /// <summary>
        /// The id of the sheet TaleWorlds wrote this character on, or null when
        /// he is a copy and has none. A sheet named after the character himself
        /// is his own rather than a shared one.
        /// </summary>
        internal static string SheetIdOf(Hero hero)
        {
            MBCharacterSkills sheet = SheetOf(hero);
            return sheet != null ? sheet.StringId : null;
        }

        private static bool StandsOnSheet(Hero hero, string domain, int bestWritten)
        {
            return AuthoredTalent.Floor(bestWritten, SkillGrowth.PeakNormFor(domain))
                   > Talent.For(hero.StringId, domain);
        }

        private static MBCharacterSkills SheetOf(Hero hero)
        {
            CharacterObject character = hero != null ? hero.CharacterObject : null;
            if (character == null || !character.IsOriginalCharacter) return null;

            return character.GetDefaultCharacterSkills();
        }

        private static int IndexOf(string domain)
        {
            if (domain == Talent.Civil) return CivilIndex;
            if (domain == Talent.Naval) return NavalIndex;
            return CombatIndex;
        }

        private static int[] Sheet(Hero hero)
        {
            string id = hero.StringId;
            if (id == null) return Read(hero);

            int[] values;
            if (_sheets.TryGetValue(id, out values)) return values;

            values = Read(hero);
            _sheets[id] = values;
            return values;
        }

        private static int[] Read(Hero hero)
        {
            int[] values = new int[3];
            try
            {
                MBCharacterSkills sheet = SheetOf(hero);
                if (sheet == null || sheet.Skills == null) return values;

                foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
                {
                    if (skill == null) continue;

                    string domain = AuthoredTalent.DomainOf(skill.StringId);
                    if (domain == null) continue;

                    int value = sheet.Skills.GetPropertyValue(skill);
                    int index = IndexOf(domain);
                    if (value > values[index]) values[index] = value;
                }
            }
            catch
            {
                // A character the game cannot describe is left to the dice, which
                // is what every hero had before. A failed read must not take the
                // weekly growth pass down with it.
            }
            return values;
        }
    }
}
