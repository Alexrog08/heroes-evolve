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
            ReportTiers();
            ReportGold();
            TroopSurvey.Report();
            ReportFormations();
            ReportSuspectKits();
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
            int[] bySupport = new int[highest + 1];
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

                // Counted a second way on purpose. byCategory is the item's
                // primary identity, which is what Classify reports; bySupport is
                // how many items the catalogue would actually accept for that
                // request, which is what FindBest uses. The two differ exactly
                // by the hand-and-a-half weapons, so printing only the first
                // hides whether the wildcard is working at all.
                foreach (WeaponCategory wanted in System.Enum.GetValues(typeof(WeaponCategory)))
                {
                    if (wanted == WeaponCategory.None) continue;
                    if (ItemClassifier.Supports(item, wanted)) bySupport[(int)wanted]++;
                }
            }

            ModLog.Info("CATALOG items=" + all.Count + " notMerchandise=" + notMerchandise);

            StringBuilder weapons = new StringBuilder("CATALOG weapons");
            foreach (WeaponCategory category in System.Enum.GetValues(typeof(WeaponCategory)))
            {
                if (category == WeaponCategory.None) continue;
                weapons.Append(' ').Append(category).Append('=').Append(byCategory[(int)category]);
            }
            ModLog.Info(weapons.ToString());

            StringBuilder usable = new StringBuilder("CATALOG accepted");
            foreach (WeaponCategory category in System.Enum.GetValues(typeof(WeaponCategory)))
            {
                if (category == WeaponCategory.None) continue;
                usable.Append(' ').Append(category).Append('=').Append(bySupport[(int)category]);
            }
            ModLog.Info(usable.ToString());

            ModLog.Info("CATALOG armor head=" + head + " body=" + body + " leg=" + leg
                        + " hand=" + hand + " cape=" + cape
                        + " mounts=" + mounts + " harnesses=" + harnesses);
        }

        /// <summary>
        /// Armour and mounts broken down by tier and culture, with sample item
        /// ids at the low tiers. ItemObject.Tier is computed at load from item
        /// properties and appears nowhere in the module XML, so this is the only
        /// way to answer what a given tier actually looks like -- specifically,
        /// which is the lowest tier that still reads as military kit rather than
        /// a peasant tunic, which is what the free grant should hand out.
        /// </summary>
        private static void ReportTiers()
        {
            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            if (all == null) return;

            ItemObject.ItemTypeEnum[] kinds =
            {
                ItemObject.ItemTypeEnum.BodyArmor,
                ItemObject.ItemTypeEnum.HeadArmor,
                ItemObject.ItemTypeEnum.LegArmor,
                ItemObject.ItemTypeEnum.HandArmor,
                ItemObject.ItemTypeEnum.Cape,
                ItemObject.ItemTypeEnum.Horse
            };

            for (int k = 0; k < kinds.Length; k++)
            {
                ItemObject.ItemTypeEnum kind = kinds[k];

                // culture id -> per-tier counts, tier 1..6 in slots 1..6.
                Dictionary<string, int[]> byCulture = new Dictionary<string, int[]>();
                // "culture|tier" -> a few example ids, for the low tiers only.
                Dictionary<string, List<string>> samples = new Dictionary<string, List<string>>();

                for (int i = 0; i < all.Count; i++)
                {
                    ItemObject item = all[i];
                    if (item == null || item.ItemType != kind) continue;
                    if (item.NotMerchandise) continue;

                    int tier = (int)item.Tier + 1;
                    if (tier < 1) tier = 1;
                    if (tier > 6) tier = 6;

                    string culture = item.Culture != null ? item.Culture.StringId : "<any>";

                    int[] counts;
                    if (!byCulture.TryGetValue(culture, out counts))
                    {
                        counts = new int[7];
                        byCulture[culture] = counts;
                    }
                    counts[tier]++;

                    if (tier > 3) continue;
                    string key = culture + "|" + tier;
                    List<string> ids;
                    if (!samples.TryGetValue(key, out ids))
                    {
                        ids = new List<string>();
                        samples[key] = ids;
                    }
                    if (ids.Count < 4) ids.Add(item.StringId);
                }

                foreach (KeyValuePair<string, int[]> pair in byCulture)
                {
                    int[] c = pair.Value;
                    ModLog.Info("TIER " + kind + " culture=" + pair.Key
                                + " t1=" + c[1] + " t2=" + c[2] + " t3=" + c[3]
                                + " t4=" + c[4] + " t5=" + c[5] + " t6=" + c[6]);
                }

                // Only body and head armour are worth sampling by name: they are
                // what makes a hero read as a soldier or a peasant on the field.
                if (kind != ItemObject.ItemTypeEnum.BodyArmor && kind != ItemObject.ItemTypeEnum.HeadArmor) continue;

                foreach (KeyValuePair<string, List<string>> pair in samples)
                {
                    ModLog.Info("TIERSAMPLE " + kind + " " + pair.Key + " -> " + string.Join(", ", pair.Value.ToArray()));
                }
            }
        }

        /// <summary>
        /// What the poorest lords could actually afford. The free grant only has
        /// to carry a hero until the purchase engine takes over, so the question
        /// that decides how generous it must be is whether a genuinely poor lord
        /// can buy his own way up -- not what the richest can.
        /// </summary>
        private static void ReportGold()
        {
            int poorestAvailable = int.MaxValue;
            string poorestHero = "<none>";
            string poorestClan = "<none>";

            int poorestClanGold = int.MaxValue;
            string poorestClanName = "<none>";

            int under1000 = 0, under3000 = 0, under10000 = 0, counted = 0, leaders = 0;
            long total = 0;

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;
                    if (hero.Clan == null || hero.Clan.IsEliminated) continue;

                    Clan clan = hero.Clan;
                    int warParties = clan.WarPartyComponents != null ? clan.WarPartyComponents.Count : 0;
                    int reserve = BudgetMath.Reserve(warParties, 1.0f);

                    // Clan.Gold is not a separate purse. Disassembled from
                    // v1.4.8: Clan::get_Gold returns Leader.Gold (or 0 with no
                    // leader). So for a clan leader, hero.Gold and clan.Gold are
                    // the same coins, and adding both counts his money twice --
                    // which is what inflated the first census's mean. Only the
                    // clan side is counted for a leader.
                    bool isLeader = clan.Leader == hero;
                    if (isLeader) leaders++;
                    int ownGold = isLeader ? 0 : hero.Gold;
                    int available = BudgetMath.Available(ownGold, clan.Gold, 0, reserve);

                    counted++;
                    total += available;
                    if (available < 1000) under1000++;
                    if (available < 3000) under3000++;
                    if (available < 10000) under10000++;

                    if (available < poorestAvailable)
                    {
                        poorestAvailable = available;
                        poorestHero = hero.Name + " (leader=" + isLeader
                                      + " own=" + hero.Gold + " clan=" + clan.Gold
                                      + " parties=" + warParties + " reserve=" + reserve + ")";
                        poorestClan = clan.Name.ToString();
                    }

                    if (clan.Gold < poorestClanGold)
                    {
                        poorestClanGold = clan.Gold;
                        poorestClanName = clan.Name + " tier=" + clan.Tier + " parties=" + warParties;
                    }
                }
                catch
                {
                    // One unreadable hero must not cost us the whole picture.
                }
            }

            if (counted == 0)
            {
                ModLog.Info("GOLD no eligible heroes with a clan");
                return;
            }

            ModLog.Info("GOLD heroes=" + counted + " clanLeaders=" + leaders
                        + " meanAvailable=" + (int)(total / counted)
                        + " under1000=" + under1000 + " under3000=" + under3000
                        + " under10000=" + under10000);
            ModLog.Info("GOLD poorestHero available=" + poorestAvailable + " " + poorestHero
                        + " clan=" + poorestClan);
            ModLog.Info("GOLD poorestClan gold=" + poorestClanGold + " " + poorestClanName);
        }

        /// <summary>
        /// How TaleWorlds itself labels each hero's battlefield role.
        ///
        /// lords.xml tags every authored lord with default_group, and the
        /// distribution is blunt: 326 of 391 are Cavalry, Battania is inverted
        /// (37 of 40 Ranged), and there is not one Vlandian ranged lord nor a
        /// single crossbow lord anywhere. That is a far better archetype source
        /// than inferring one from skills -- but it is only useful if it
        /// survives to the heroes this mod actually repairs.
        ///
        /// Authored lords are not those heroes. The come-of-age bug hits
        /// children generated during the campaign, and whether they inherit a
        /// meaningful DefaultFormationClass appears in no XML file. So the split
        /// below is the whole point: authored (character id "lord_...") against
        /// generated, plus the children who will become the real test cases.
        /// </summary>
        private static void ReportFormations()
        {
            Dictionary<string, int> authored = new Dictionary<string, int>();
            Dictionary<string, int> generated = new Dictionary<string, int>();
            Dictionary<string, int> children = new Dictionary<string, int>();
            int childCount = 0;

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (hero == null || hero.IsDead || hero.IsTemplate) continue;

                    CharacterObject character = hero.CharacterObject;
                    if (character == null) continue;

                    string formation = character.DefaultFormationClass.ToString();

                    if (hero.IsChild)
                    {
                        childCount++;
                        Bump(children, formation);
                        continue;
                    }

                    if (!HeroFilter.IsEligible(hero)) continue;

                    bool isAuthored = character.StringId != null && character.StringId.StartsWith("lord_");
                    Bump(isAuthored ? authored : generated, formation);
                }
                catch
                {
                    // One unreadable hero must not cost the whole breakdown.
                }
            }

            ModLog.Info("FORMATION authored  " + Render(authored));
            ModLog.Info("FORMATION generated " + Render(generated));
            ModLog.Info("FORMATION children  " + Render(children) + " total=" + childCount);
        }

        private static void Bump(Dictionary<string, int> counts, string key)
        {
            int n;
            if (!counts.TryGetValue(key, out n)) n = 0;
            counts[key] = n + 1;
        }

        private static string Render(Dictionary<string, int> counts)
        {
            if (counts.Count == 0) return "<none>";
            StringBuilder text = new StringBuilder();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (text.Length > 0) text.Append(' ');
                text.Append(pair.Key).Append('=').Append(pair.Value);
            }
            return text.ToString();
        }

        /// <summary>
        /// Hunts the come-of-age failure by its symptoms rather than by the
        /// exact signature GrantService.NeedsGrant looks for.
        ///
        /// NeedsGrant only fires on empty weapon slots or on one specific item
        /// id (the vanilla dummy spatha), and it never looks at armour at all.
        /// An advanced save with 600 adult lords -- many of them born and grown
        /// during the campaign -- reported zero heroes needing a grant, which
        /// either means the bug does not occur in v1.4.8 or means the detector
        /// is too narrow. The bug was reported as "civilian clothes and a single
        /// one-handed sword", and a hero holding an ordinary sword in ordinary
        /// clothes matches neither of NeedsGrant's two tests.
        ///
        /// So look for the symptoms instead: too few weapons, missing armour, or
        /// body armour still at tier 1 -- which the tier census showed is
        /// literally civilian clothing (aserai_civil_d, vlandian_woman_dress,
        /// nord_casual_tunic). Reported, never acted on.
        /// </summary>
        private static void ReportSuspectKits()
        {
            int[] weaponCounts = new int[SlotSnapshot.WeaponSlotCount + 1];
            int[] bodyTiers = new int[8];
            int noBody = 0, examined = 0, suspects = 0, young = 0, youngSuspects = 0;

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;
                    if (hero.BattleEquipment == null) continue;

                    examined++;

                    int weapons = 0;
                    for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
                    {
                        if (hero.BattleEquipment[SlotMapping.WeaponSlot(i)].Item != null) weapons++;
                    }
                    weaponCounts[weapons]++;

                    ItemObject body = hero.BattleEquipment[EquipmentIndex.Body].Item;
                    int bodyTier = 0;
                    if (body == null) noBody++;
                    else
                    {
                        bodyTier = (int)body.Tier + 1;
                        if (bodyTier < 1) bodyTier = 1;
                        if (bodyTier > 6) bodyTier = 6;
                        bodyTiers[bodyTier]++;
                    }

                    // 18-25: the cohort that has come of age recently enough for
                    // the failure to still be visible on them.
                    bool isYoung = hero.Age < 26f;
                    if (isYoung) young++;

                    bool suspect = weapons <= 1 || body == null || bodyTier == 1;
                    if (!suspect) continue;

                    suspects++;
                    if (isYoung) youngSuspects++;

                    if (suspects <= 25)
                    {
                        ModLog.Info("SUSPECT hero=" + hero.Name
                                    + " age=" + (int)hero.Age
                                    + " weapons=" + weapons
                                    + " body=" + (body == null ? "<none>" : body.StringId + " t" + bodyTier)
                                    + " formation=" + (hero.CharacterObject != null
                                        ? hero.CharacterObject.DefaultFormationClass.ToString() : "?")
                                    + " charId=" + (hero.CharacterObject != null ? hero.CharacterObject.StringId : "?")
                                    + " culture=" + CultureIdOf(hero)
                                    + " | " + DescribeSlots(hero));
                    }
                }
                catch
                {
                    // One unreadable hero must not cost the sweep.
                }
            }

            StringBuilder counts = new StringBuilder("SUSPECT weaponCount");
            for (int i = 0; i < weaponCounts.Length; i++) counts.Append(' ').Append(i).Append('=').Append(weaponCounts[i]);
            ModLog.Info(counts.ToString());

            StringBuilder tiers = new StringBuilder("SUSPECT bodyTier none=" + noBody);
            for (int i = 1; i <= 6; i++) tiers.Append(" t").Append(i).Append('=').Append(bodyTiers[i]);
            ModLog.Info(tiers.ToString());

            ModLog.Info("SUSPECT examined=" + examined + " suspects=" + suspects
                        + " under26=" + young + " suspectsUnder26=" + youngSuspects);
        }

        private static string DescribeSlots(Hero hero)
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                EquipmentIndex slot = SlotMapping.WeaponSlot(i);
                ItemObject item = hero.BattleEquipment[slot].Item;
                text.Append(SlotMapping.NameOf(slot)).Append('=').Append(item == null ? "-" : item.StringId).Append(' ');
            }
            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                ItemObject item = hero.BattleEquipment[slot].Item;
                text.Append(SlotMapping.NameOf(slot)).Append('=').Append(item == null ? "-" : item.StringId).Append(' ');
            }
            ItemObject horse = hero.BattleEquipment[EquipmentIndex.Horse].Item;
            text.Append("Horse=").Append(horse == null ? "-" : horse.StringId);
            return text.ToString();
        }

        /// <summary>
        /// Counts the live hero population the mod acts on and returns the
        /// heroes that are actually broken right now.
        /// </summary>
        private static List<Hero> ReportHeroes()
        {
            List<Hero> broken = new List<Hero>();
            int alive = 0, eligible = 0, dead = 0, templates = 0, children = 0, notLord = 0, player = 0;

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

                    // IsEligible also drops the player character, which no
                    // bucket above catches -- without this the printed counts
                    // silently fail to add up to `alive`.
                    if (!HeroFilter.IsEligible(hero)) { player++; continue; }
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
                        + " children=" + children + " nonLord=" + notLord
                        + " player=" + player + ")");
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
                    + " needsGrant=" + GrantService.NeedsGrant(hero)
                    + " role=" + resolved.Role
                    + " formation=" + (hero.CharacterObject != null
                                            ? hero.CharacterObject.DefaultFormationClass.ToString() : "<none>")
                    + " charId=" + (hero.CharacterObject != null ? hero.CharacterObject.StringId : "<none>")
                    + " | skills=" + RenderSkills(HeroAdapter.ReadSkills(hero))
                    + " | current=" + resolved.CurrentWeapons
                    + " | target=" + resolved.TargetWeapons
                    + " | placed=" + resolved.PlacedWeapons
                    + " | ifStripped=" + IfStripped(hero, resolved);

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

        /// <summary>
        /// What this hero would be planned if every weapon slot were empty.
        ///
        /// The `target` field alone reads as if it were the whole answer, and it
        /// is not: it holds only the archetype core (a melee weapon and a
        /// shield, say), while the remaining slots are filled afterwards by the
        /// skill walk. A cavalry lord whose target says "Spear,Shield" still
        /// picks up a bow in the spare pair if Bow ranks high enough -- which is
        /// exactly what vanilla gave those lords in the first place. Without
        /// this line the log invites the conclusion that a strong secondary
        /// skill is being thrown away.
        ///
        /// Pure computation on a synthetic empty snapshot; the hero is not read
        /// for equipment and never written to.
        /// </summary>
        private static string IfStripped(Hero hero, ResolvedGrant resolved)
        {
            WeaponCategory[] none = new WeaponCategory[SlotSnapshot.WeaponSlotCount];
            for (int i = 0; i < none.Length; i++) none[i] = WeaponCategory.None;

            // Mount state is kept as the real plan decided it, so the mounted
            // ranged rules stay the same as the ones the hero is actually judged
            // by; only the weapon slots are emptied.
            SlotSnapshot empty = new SlotSnapshot(none, resolved.Mounted,
                                                  false, false, false, false, false, false);

            List<PlannedSlot> plan = LoadoutPlanner.Plan(HeroAdapter.ReadSkills(hero), empty,
                                                         resolved.Availability,
                                                         HeroLoadoutBehavior.DominanceMargin,
                                                         resolved.CultureFieldsMountedElites,
                                                         HeroAdapter.ReadRole(hero));

            if (plan.Count == 0) return "<none>";

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < plan.Count; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(plan[i].Category);
            }
            return text.ToString();
        }

        /// <summary>
        /// Every combat skill with its value. Two rounds of argument about why a
        /// hero was planned as an archer were spent without anyone being able to
        /// see the numbers the decision was made from.
        /// </summary>
        private static string RenderSkills(SkillProfile skills)
        {
            return "1h=" + skills.Get(SkillKind.OneHanded)
                   + " 2h=" + skills.Get(SkillKind.TwoHanded)
                   + " pole=" + skills.Get(SkillKind.Polearm)
                   + " bow=" + skills.Get(SkillKind.Bow)
                   + " xbow=" + skills.Get(SkillKind.Crossbow)
                   + " throw=" + skills.Get(SkillKind.Throwing)
                   + " ride=" + skills.Get(SkillKind.Riding);
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
