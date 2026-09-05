using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// Reads the loadout each culture actually fields, from its own troop tree.
    ///
    /// Hand-written archetype rules cannot know that Vlandia fields crossbows
    /// and Battania longbows -- that knowledge lives in the troop definitions,
    /// and encoding it a second time in our own rules means it can be wrong.
    /// A Battanian fian carries longbow + two quivers + a two-handed sword; an
    /// imperial palatine guard carries a bow + two quivers + a ONE-handed
    /// sword. Same role, different culture, different loadout. Reading it from
    /// the game gets modded cultures right for free.
    ///
    /// This is a survey only: nothing here decides anything yet.
    /// </summary>
    public static class TroopSurvey
    {
        /// <summary>Depth cap on the upgrade tree, matching HeroAdapter's walk.</summary>
        private const int MaxDepth = 10;

        /// <summary>One troop's weapon loadout, reduced to our categories.</summary>
        public sealed class Signature
        {
            public string TroopId;
            public int Tier;
            public bool Mounted;
            public List<WeaponCategory> Weapons = new List<WeaponCategory>();

            public string Render()
            {
                if (Weapons.Count == 0) return "<none>";
                StringBuilder text = new StringBuilder();
                for (int i = 0; i < Weapons.Count; i++)
                {
                    if (i > 0) text.Append('+');
                    text.Append(Weapons[i]);
                }
                return text.ToString();
            }
        }

        /// <summary>
        /// Every distinct troop reachable from a culture's basic and elite
        /// lines. Both are walked because the elite line alone misses whole
        /// roles: several cultures put their crossbows or archers on the
        /// common line and their cavalry on the elite one.
        /// </summary>
        public static List<Signature> Survey(CultureObject culture)
        {
            List<Signature> found = new List<Signature>();
            if (culture == null) return found;

            HashSet<string> seen = new HashSet<string>();
            Collect(culture.EliteBasicTroop, found, seen, 0);
            Collect(culture.BasicTroop, found, seen, 0);
            return found;
        }

        private static void Collect(CharacterObject troop, List<Signature> found, HashSet<string> seen, int depth)
        {
            if (troop == null || depth > MaxDepth) return;
            if (!seen.Add(troop.StringId)) return;

            Signature signature = Describe(troop);
            if (signature != null) found.Add(signature);

            CharacterObject[] upgrades = troop.UpgradeTargets;
            if (upgrades == null) return;

            // Every branch, not just the last: a culture's archer and its
            // infantry are usually two upgrade targets of the same recruit,
            // and following only one of them loses half the roles.
            for (int i = 0; i < upgrades.Length; i++)
            {
                Collect(upgrades[i], found, seen, depth + 1);
            }
        }

        private static Signature Describe(CharacterObject troop)
        {
            IEnumerable<Equipment> sets = troop.BattleEquipments;
            if (sets == null) return null;

            // The first battle set is representative: alternates are cosmetic
            // or minor variations of the same role.
            foreach (Equipment set in sets)
            {
                if (set == null) continue;

                Signature signature = new Signature();
                signature.TroopId = troop.StringId;
                signature.Tier = troop.Tier;
                signature.Mounted = set[EquipmentIndex.Horse].Item != null;

                for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
                {
                    ItemObject item = set[SlotMapping.WeaponSlot(i)].Item;
                    if (item == null) continue;

                    WeaponCategory category = ItemClassifier.Classify(item);
                    if (category == WeaponCategory.None) continue;
                    signature.Weapons.Add(category);
                }

                return signature;
            }

            return null;
        }

        /// <summary>Writes every culture's troop loadouts to the log.</summary>
        public static void Report()
        {
            List<CultureObject> cultures = new List<CultureObject>();
            HashSet<string> seen = new HashSet<string>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                CultureObject culture = hero != null ? hero.Culture : null;
                if (culture == null) continue;
                if (!seen.Add(culture.StringId)) continue;
                cultures.Add(culture);
            }

            for (int c = 0; c < cultures.Count; c++)
            {
                CultureObject culture = cultures[c];
                List<Signature> signatures;
                try
                {
                    signatures = Survey(culture);
                }
                catch (System.Exception ex)
                {
                    ModLog.Error("TROOP survey failed for " + culture.StringId
                                 + ": " + ex.GetType().Name + " " + ex.Message);
                    continue;
                }

                ModLog.Info("TROOP culture=" + culture.StringId + " troops=" + signatures.Count);

                for (int i = 0; i < signatures.Count; i++)
                {
                    Signature s = signatures[i];

                    // Only tier 3 and up: the recruit tiers are all "one spear,
                    // one shield" and say nothing about a culture's identity.
                    if (s.Tier < 3) continue;

                    ModLog.Info("TROOP   " + culture.StringId + " t" + s.Tier
                                + (s.Mounted ? " mounted" : " foot")
                                + " " + s.TroopId + " = " + s.Render());
                }
            }
        }
    }
}
