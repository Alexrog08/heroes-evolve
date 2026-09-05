using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// Read-only reporting. Nothing here changes a hero, an item or a save.
    ///
    /// This exists because the come-of-age bug is nearly unreachable by playing:
    /// a fresh campaign's youngest lords are a full in-game year from turning
    /// eighteen, and the heroes the bug really hits -- children generated during
    /// the campaign -- are two decades out. Rather than wait for the defect to
    /// appear before finding out whether the repair code works, the census runs
    /// the entire decision path against the live campaign's real heroes and the
    /// real item catalogue, and writes down what it *would* do.
    /// </summary>
    public static class Diagnostics
    {
        /// <summary>Heroes dry-run per culture when nothing is actually broken.</summary>
        private const int SamplesPerCulture = 2;

        /// <summary>Hard cap on dry-run lines, so a big campaign cannot flood the log.</summary>
        private const int MaxDryRuns = 20;

        /// <summary>
        /// Repairs the live tick has already made since this campaign was
        /// loaded. Bannerlord staggers per-hero daily ticks across the in-game
        /// day, so heroes whose slot falls before the next day boundary are
        /// repaired before the census ever runs. Without this counter the one
        /// census of the session can honestly report "needingGrant=0" for a
        /// population that was broken minutes earlier, which is the opposite of
        /// what the reader would conclude.
        /// </summary>
        private static int _repairsBeforeCensus;

        public static void NoteRepair()
        {
            _repairsBeforeCensus++;
        }

        /// <summary>Called when a campaign is loaded; statics outlive one campaign.</summary>
        public static void ResetSession()
        {
            _repairsBeforeCensus = 0;
        }

        public static void RunCensus(float clanWeight, float skillWeight, int minimumTier, int dominanceMargin)
        {
            ModLog.Info("===== CENSUS BEGIN =====");
            ReportCatalog();
            List<Hero> broken = ReportHeroes();
            ReportDryRuns(broken, clanWeight, skillWeight, minimumTier, dominanceMargin);
            ModLog.Info("===== CENSUS END =====");
        }

        /// <summary>
        /// One pass over every item in the game, counted by the category our
        /// own classifier assigns it. If the mod is ever going to fail by
        /// finding nothing to equip, it shows up here first: a category with
        /// zero items can never fill a slot, whatever the hero's skills say.
        /// </summary>
        private static void ReportCatalog()
        {
            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            if (all == null)
            {
                ModLog.Error("CATALOG item list is null");
                return;
            }

            // Size by the highest enum VALUE, not the member count: the two only
            // agree while WeaponCategory stays contiguous from zero, and the
            // array is indexed by (int)category below.
            int highest = 0;
            foreach (WeaponCategory category in System.Enum.GetValues(typeof(WeaponCategory)))
            {
                if ((int)category > highest) highest = (int)category;
            }

            int[] byCategory = new int[highest + 1];
            int mounts = 0, harnesses = 0, notMerchandise = 0;
            int head = 0, body = 0, leg = 0, hand = 0, cape = 0;

            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (item == null) continue;
                if (item.NotMerchandise) notMerchandise++;

                switch (item.ItemType)
                {
                    case ItemObject.ItemTypeEnum.Horse: mounts++; break;
                    case ItemObject.ItemTypeEnum.HorseHarness: harnesses++; break;
                    case ItemObject.ItemTypeEnum.HeadArmor: head++; break;
                    case ItemObject.ItemTypeEnum.BodyArmor: body++; break;
                    case ItemObject.ItemTypeEnum.LegArmor: leg++; break;
                    case ItemObject.ItemTypeEnum.HandArmor: hand++; break;
                    case ItemObject.ItemTypeEnum.Cape: cape++; break;
                }

                WeaponCategory category = ItemClassifier.Classify(item);
                byCategory[(int)category]++;
            }

            ModLog.Info("CATALOG items=" + all.Count + " notMerchandise=" + notMerchandise);

            StringBuilder weapons = new StringBuilder("CATALOG weapons");
            foreach (WeaponCategory category in System.Enum.GetValues(typeof(WeaponCategory)))
            {
                if (category == WeaponCategory.None) continue;
                weapons.Append(' ').Append(category).Append('=').Append(byCategory[(int)category]);
            }
            ModLog.Info(weapons.ToString());

            ModLog.Info("CATALOG armor head=" + head + " body=" + body + " leg=" + leg
                        + " hand=" + hand + " cape=" + cape
                        + " mounts=" + mounts + " harnesses=" + harnesses);
        }

        /// <summary>
        /// Counts the live hero population the mod acts on and returns the
        /// heroes that are actually broken right now.
        /// </summary>
        private static List<Hero> ReportHeroes()
        {
            List<Hero> broken = new List<Hero>();
            int alive = 0, eligible = 0, dead = 0, templates = 0, children = 0, notLord = 0;

            int failed = 0;

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                alive++;

                // Guarded per hero, for the same reason the live tick guards
                // TryRepair: one malformed hero must not take down the pass.
                // The census is the code least able to afford all-or-nothing --
                // it runs once per session, and it exists precisely to produce
                // information when something is already wrong.
                try
                {
                    if (hero == null) continue;
                    if (hero.IsDead) { dead++; continue; }
                    if (hero.IsTemplate) { templates++; continue; }
                    if (hero.IsChild) { children++; continue; }
                    if (!hero.IsLord) { notLord++; continue; }

                    if (!HeroFilter.IsEligible(hero)) continue;
                    eligible++;

                    if (GrantService.NeedsGrant(hero)) broken.Add(hero);
                }
                catch (System.Exception ex)
                {
                    failed++;
                    if (failed <= 5) ModLog.Error("census hero failed: " + ex.GetType().Name + " " + ex.Message);
                }
            }

            if (failed > 0) ModLog.Error("HEROES skipped=" + failed + " (threw while being inspected)");

            ModLog.Info("HEROES alive=" + alive + " eligible=" + eligible
                        + " (excluded: dead=" + dead + " templates=" + templates
                        + " children=" + children + " nonLord=" + notLord + ")");
            ModLog.Info("HEROES needingGrant=" + broken.Count
                        + " alreadyRepairedThisSession=" + _repairsBeforeCensus);

            for (int i = 0; i < broken.Count; i++)
            {
                Hero hero = broken[i];
                ModLog.Info("BROKEN hero=" + hero.Name
                            + " age=" + (int)hero.Age
                            + " clan=" + (hero.Clan != null ? hero.Clan.Name.ToString() : "<none>")
                            + " culture=" + CultureIdOf(hero));
            }

            return broken;
        }

        /// <summary>
        /// Dry-runs the real decision path. Heroes that are genuinely broken go
        /// first, because those are the ones the mod is about to act on; the
        /// remainder of the budget is filled with a spread of healthy heroes
        /// across cultures, which is what exercises the catalogue lookups for
        /// culture-specific gear.
        /// </summary>
        private static void ReportDryRuns(List<Hero> broken, float clanWeight, float skillWeight,
                                          int minimumTier, int dominanceMargin)
        {
            List<Hero> chosen = new List<Hero>();

            for (int i = 0; i < broken.Count && chosen.Count < MaxDryRuns; i++)
            {
                chosen.Add(broken[i]);
            }

            Dictionary<string, int> perCulture = new Dictionary<string, int>();
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                if (chosen.Count >= MaxDryRuns) break;
                if (!HeroFilter.IsEligible(hero)) continue;
                if (chosen.Contains(hero)) continue;

                string culture = CultureIdOf(hero);
                int seen;
                if (!perCulture.TryGetValue(culture, out seen)) seen = 0;
                if (seen >= SamplesPerCulture) continue;

                perCulture[culture] = seen + 1;
                chosen.Add(hero);
            }

            ModLog.Info("DRYRUN heroes=" + chosen.Count + " (broken=" + broken.Count + ")");

            for (int i = 0; i < chosen.Count; i++)
            {
                DryRun(chosen[i], clanWeight, skillWeight, minimumTier, dominanceMargin);
            }
        }

        /// <summary>
        /// Resolves one hero through the real GrantService and writes the
        /// result to the log without applying it. Returns the same text for a
        /// console command to echo.
        /// </summary>
        public static string DryRun(Hero hero, float clanWeight, float skillWeight,
                                    int minimumTier, int dominanceMargin)
        {
            if (hero == null) return "no hero";

            StringBuilder echo = new StringBuilder();
            try
            {
                ResolvedGrant resolved = GrantService.Resolve(hero, clanWeight, skillWeight, minimumTier, dominanceMargin);
                if (resolved == null)
                {
                    string none = "DRY hero=" + hero.Name + " -> could not resolve (no battle equipment)";
                    ModLog.Info(none);
                    return none;
                }

                string header = "DRY hero=" + hero.Name
                    + " clan=" + (hero.Clan != null ? hero.Clan.Name.ToString() : "<none>")
                    + " clanTier=" + resolved.ClanTier
                    + " maxSkill=" + resolved.MaxCombatSkill
                    + " ceiling=" + resolved.Ceiling
                    + " culture=" + CultureIdOf(hero)
                    + " wantsMount=" + resolved.WantsMount
                    + " mounted=" + resolved.Mounted
                    + " cultureMounted=" + resolved.CultureFieldsMountedElites
                    + " bowViable=" + resolved.Availability.BowViable
                    + " xbowViable=" + resolved.Availability.CrossbowViable
                    + " plannedWeapons=" + resolved.PlannedWeaponCount
                    + " wouldGrant=" + resolved.WouldGrantCount
                    + " needsGrant=" + GrantService.NeedsGrant(hero);

                ModLog.Info(header);
                echo.AppendLine(header);

                for (int i = 0; i < resolved.Slots.Count; i++)
                {
                    ResolvedSlot slot = resolved.Slots[i];
                    string line = "DRY   " + slot.Label + " want=" + slot.Want + " -> " + Describe(slot);
                    ModLog.Info(line);
                    echo.AppendLine(line);
                }
            }
            catch (System.Exception ex)
            {
                string failure = "DRY hero=" + hero.Name + " FAILED " + ex.GetType().Name + " " + ex.Message;
                ModLog.Error(failure);
                echo.AppendLine(failure);
            }

            return echo.ToString();
        }

        private static string Describe(ResolvedSlot slot)
        {
            if (slot.SkipReason != null)
            {
                return "SKIP (" + slot.SkipReason + ")"
                       + (slot.Existing != null ? " existing=" + slot.Existing : "");
            }

            if (slot.Item == null)
            {
                // The failure worth hunting: the planner asked for something the
                // catalogue could not supply under this hero's ceiling/culture.
                return "NONE FOUND"
                       + (slot.Existing != null ? " (keeps existing=" + slot.Existing + ")" : "");
            }

            return slot.Item.StringId
                   + " tier=" + ((int)slot.Item.Tier + 1)
                   + " value=" + slot.Item.Value
                   + (slot.Existing != null ? " (replaces " + slot.Existing + ")" : "");
        }

        internal static string CultureIdOf(Hero hero)
        {
            if (hero == null) return "<none>";
            if (hero.Culture != null) return hero.Culture.StringId;
            if (hero.Clan != null && hero.Clan.Culture != null) return hero.Clan.Culture.StringId;
            return "<none>";
        }
    }
}
