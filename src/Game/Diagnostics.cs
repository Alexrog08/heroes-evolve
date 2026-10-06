using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using HeroesEvolve.Core;

namespace HeroesEvolve
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
            RobberyTally.Reset();
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
            ReportRiding();
            ReportCohorts();
            CultureProfile.Report();
            CultureArchetypes.Report();
            ReportVariety(dominanceMargin);
            ReportDefectRate();
            ReportSkillCurve();
            ReportChase();
            ReportPlayerCharacters();
            ReportMyClan();
            ReportAttributes();
            ReportTalentSpread();
            ReportAuthoredTalent();
            ReportTraits();
            ReportFeuds();
            ReportAllSkills();
            ReportGaps();
            ReportNaval();
            CaravanWatch.Report();
            ReportClanWealth();
            ReportGearVsClan();
            ReportWornBySlot();
            ReportUniqueGear();
            ReportWeaponPerks();
            ReportBanners();
            ReportTierSpread();
            ReportMarkets();
            ReportQuality();
            ReportHeadroom(clanWeight, skillWeight, minimumTier);
            ReportShopping(clanWeight, skillWeight, minimumTier);
            PurchaseWatch.Report(clanWeight, skillWeight, minimumTier);
            List<Hero> broken = ReportHeroes();
            ReportDryRuns(broken, clanWeight, skillWeight, minimumTier, dominanceMargin);
            ModLog.Info("===== CENSUS END =====");
        }

        /// <summary>
        /// The four traits the plunder model weighs, the chance they produce,
        /// and by name the lords with no honour.
        ///
        /// Written because the model was balanced against a range the game does
        /// not generate. PlunderRules clamps every trait to minus two through
        /// plus two and the design was reasoned in those terms -- a paragon at
        /// zero percent, a brute at a hundred -- but the first measurement of a
        /// live campaign found Honor confined to minus one through plus one,
        /// with not one lord of 495 at either extreme. Months of tuning against
        /// men who do not exist.
        ///
        /// So all four are counted now, and with them the thing that actually
        /// wants balancing: the chance itself. It is computed against a
        /// stranger -- relation zero, no kinship -- so what comes out is
        /// character and nothing else, which is the only version of the number
        /// that can be compared between two lords.
        ///
        /// The named list stays for a duller reason: the reprisal branch of
        /// PrisonerDialogue can only be seen by capturing a lord whose Honor is
        /// below zero, and the game keeps traits behind an encyclopedia page
        /// you have to already suspect someone to open.
        /// </summary>
        private static void ReportTraits()
        {
            int[] honour = new int[5];
            int[] mercy = new int[5];
            int[] generosity = new int[5];
            int[] calculating = new int[5];
            List<int> chances = new List<int>();
            List<string> dishonourable = new List<string>();
            int examined = 0;

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;
                    examined++;

                    int h = hero.GetTraitLevel(DefaultTraits.Honor);
                    int m = hero.GetTraitLevel(DefaultTraits.Mercy);
                    int g = hero.GetTraitLevel(DefaultTraits.Generosity);
                    int c = hero.GetTraitLevel(DefaultTraits.Calculating);

                    Count(honour, h);
                    Count(mercy, m);
                    Count(generosity, g);
                    Count(calculating, c);

                    // Against a stranger, so relation and kinship cannot muddy
                    // what is meant to be a reading of the man.
                    float chance = PlunderRules.Chance(false, h, m, g, c,
                                                       hero.GetSkillValue(DefaultSkills.Roguery),
                                                       0, PlunderRules.Kinship.None,
                                                       Settings.RobberyMultiplier());
                    chances.Add((int)(chance * 100f + 0.5f));

                    if (h >= 0) continue;

                    dishonourable.Add(hero.Name
                                      + " honour=" + h
                                      + " clan=" + (hero.Clan != null ? hero.Clan.Name.ToString() : "<none>")
                                      + " culture=" + CultureIdOf(hero)
                                      + " kingdom=" + (hero.MapFaction != null
                                          ? hero.MapFaction.Name.ToString() : "<none>")
                                      + (hero.IsPrisoner ? " PRISONER" : "")
                                      // Marked because a friend is never a
                                      // reprisal, however poor his reputation
                                      // elsewhere -- see PrisonerDialogue.
                                      // IsReprisal. Without this the list names
                                      // men the branch will refuse.
                                      + (Hero.MainHero != null && hero.IsFriend(Hero.MainHero)
                                         ? " YOUR FRIEND (no reprisal)" : ""));
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }

            ModLog.Info("TRAITS examined=" + examined);
            ModLog.Info("TRAITS Honor       weight=" + PlunderRules.HonorWeight + " " + Spread(honour));
            ModLog.Info("TRAITS Generosity  weight=" + PlunderRules.GenerosityWeight + " " + Spread(generosity));
            ModLog.Info("TRAITS Mercy       weight=" + PlunderRules.MercyWeight + " " + Spread(mercy));
            ModLog.Info("TRAITS Calculating weight=" + PlunderRules.CalculatingWeight + " " + Spread(calculating));

            // The number every one of those weights exists to produce. Percent
            // of prisoners this lord would rob, all else being neutral.
            ModLog.Info("PLUNDERCHANCE percent " + Percentiles(chances));

            // Whether the player is currently exempt, and why. Expected to read
            // locked=True only inside the story mode's opening phase, where
            // every town is shut to him and being stripped is unrecoverable --
            // see TutorialLock. A census run on any ordinary campaign that
            // reports locked=True is reporting a bug, because it means the mod
            // is refusing a robbery it should allow for the rest of that save.
            ModLog.Info("PLUNDERCHANCE tutorial locksTheMap=" + TutorialLock.LocksTheMap()
                        + " playerCanBeStripped=" + PlunderService.CanBeStripped(Hero.MainHero));

            // What the robbery handed back. slotsLeftEmpty is the number to
            // watch and should be nought: a lord only ever buys a better
            // version of what he already carries and can never fill an empty
            // slot, so anything counted here is a slot that stays bare for the
            // rest of that hero's life. A non-zero count means some culture
            // sells nothing at all for that slot at the rag tier.
            ModLog.Info("RAGS handedOut=" + Rags.HandedOut
                        + " slotsLeftEmpty=" + Rags.SlotsLeftEmpty
                        + " (body tier " + Core.RagTier.Body
                        + ", everything else " + Core.RagTier.Everything + ")");

            for (int i = 0; i < dishonourable.Count; i++)
            {
                ModLog.Info("TRAITS dishonourable " + dishonourable[i]);
            }
        }

        /// <summary>The feud lines, under the traits that drive them. See Feuds.</summary>
        internal static void ReportFeuds()
        {
            try
            {
                List<string> feuds = Feuds();
                for (int i = 0; i < feuds.Count; i++) ModLog.Info("FEUD " + feuds[i]);
            }
            catch (System.Exception ex)
            {
                ModLog.Error("FEUD failed: " + ex.GetType().Name + " " + ex.Message);
            }
        }

        /// <summary>Tallies one trait level into its bucket, clamped as the rules clamp it.</summary>
        private static void Count(int[] buckets, int level)
        {
            int bucket = level + 2;
            if (bucket < 0) bucket = 0;
            if (bucket > 4) bucket = 4;
            buckets[bucket]++;
        }

        private static string Spread(int[] buckets)
        {
            return "minus2=" + buckets[0] + " minus1=" + buckets[1] + " zero=" + buckets[2]
                   + " plus1=" + buckets[3] + " plus2=" + buckets[4];
        }

        /// <summary>
        /// What lords wear in each slot, separately.
        ///
        /// Written to settle one question: the trip budget divides a lord's
        /// money evenly between the slots he can improve, which is only fair if
        /// a given tier costs roughly the same everywhere. If it does not --
        /// if boots are cheap and body armour dear -- then an even split dresses
        /// him in the best boots in Calradia over a shirt, and the budget wants
        /// weighting by slot instead.
        ///
        /// It answered the question and the answer was neither. Lords wear body
        /// armour at 6.77 and leg armour at 3.17 -- three and a half tiers of
        /// spread, far worse than feared -- and no budget can touch it, because
        /// the catalogue is what causes it. Counted across every culture: 203
        /// body armours and 431 helmets at tier 6, and for gloves and leg armour
        /// at tier 5 or 6, nothing at all. Bannerlord simply does not make them.
        /// Their ceiling is tier 4, and there are four tier-4 leggings in the
        /// whole game, all Battanian.
        ///
        /// So weighting the trip budget by slot would be worse than useless: it
        /// would hand more money to a slot with nothing better to sell. The even
        /// split already handles this correctly, because what cannot be spent on
        /// leggings stays in the pot and buys a better cuirass.
        ///
        /// Kept in the census anyway. It is the line that would catch a mod
        /// adding high-tier leg armour, or a future change that starts dressing
        /// lords lopsidedly for a reason the game itself is not responsible for.
        /// </summary>
        private static void ReportTierBySlot()
        {
            Dictionary<string, int> total = new Dictionary<string, int>();
            Dictionary<string, int> count = new Dictionary<string, int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;
                    if (hero.BattleEquipment == null) continue;

                    for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
                    {
                        Tally(total, count, hero, SlotMapping.WeaponSlot(i));
                    }
                    foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
                    {
                        Tally(total, count, hero, slot);
                    }
                    Tally(total, count, hero, EquipmentIndex.Horse);
                    Tally(total, count, hero, EquipmentIndex.HorseHarness);
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }

            List<string> slots = new List<string>(count.Keys);
            slots.Sort();
            foreach (string slot in slots)
            {
                if (count[slot] == 0) continue;
                ModLog.Info("SLOTTIER " + slot + " worn=" + count[slot]
                            + " meanTier=" + Mean2(total[slot], count[slot] * 100));
            }
        }

        private static void Tally(Dictionary<string, int> total, Dictionary<string, int> count,
                                  Hero hero, EquipmentIndex slot)
        {
            ItemObject item = hero.BattleEquipment[slot].Item;
            if (item == null) return;

            string name = SlotMapping.NameOf(slot);
            int t;
            total.TryGetValue(name, out t);
            total[name] = t + (int)(item.Tierf * 100f);

            int n;
            count.TryGetValue(name, out n);
            count[name] = n + 1;
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
            int clothing = 0, kit = 0;

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

                if (ItemCatalog.IsArmorSlot(item.ItemType))
                {
                    if (KitTier.IsClothing((int)item.Tier + 1)) clothing++;
                    else kit++;
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
            // What is installed and what each one brought, which is how a
            // player learns the ids he may put in ExcludedModules. Printed
            // whether or not anything is excluded: the question "what could I
            // exclude?" is the one that comes first.
            ModLog.Info("MODULES declaredItems " + ItemModules.Describe());
            ModLog.Info("CATALOG armor kit=" + kit + " clothing=" + clothing
                        + " (below tier " + KitTier.Lowest + ", refused for a battle outfit)");
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
                                    + (hero.IsPrisoner ? " PRISONER" : "")
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

        /// <summary>
        /// The Riding distribution across the lords the mod acts on, split by
        /// the role the game gave them.
        ///
        /// The question this answers is where to put the floor below which a
        /// Cavalry-labelled lord should be left on foot. Picking that number by
        /// intuition would be guessing; picking it from the percentiles of the
        /// real population is not. The reference points that matter are the
        /// game's own: every merchandise war horse requires Riding 10 or more,
        /// the culture basics sit at 10-20, tier-2 mounts at 30-45 and tier-3 at
        /// 50-65, so a threshold only has meaning relative to those rungs.
        /// </summary>
        private static void ReportRiding()
        {
            List<int> mountedRoles = new List<int>();
            List<int> footRoles = new List<int>();
            List<int> everyone = new List<int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;

                    int riding = HeroAdapter.ReadSkills(hero).Get(SkillKind.Riding);
                    everyone.Add(riding);

                    if (BattleRoleRules.IsMounted(HeroAdapter.ReadRole(hero))) mountedRoles.Add(riding);
                    else footRoles.Add(riding);
                }
                catch
                {
                    // One unreadable hero must not cost the distribution.
                }
            }

            ModLog.Info("RIDING all       " + Percentiles(everyone));
            ModLog.Info("RIDING mountedRole " + Percentiles(mountedRoles));
            ModLog.Info("RIDING footRole  " + Percentiles(footRoles));
            ModLog.Info("RIDING mountedRole below: " + BelowCounts(mountedRoles));
        }

        /// <summary>
        /// Every living lord's mount and harness, and how many of the pairs do
        /// not fit: a camel saddle on a horse, a horse harness on a camel.
        ///
        /// Written for a subscriber's report of exactly that, which the market
        /// was producing -- it let the Aserai swap one beast for the other and
        /// left the saddle where it was. The market no longer swaps beasts and
        /// now replaces a saddle that does not fit, so in a save from before
        /// mismatched should fall week by week toward what TaleWorlds' own
        /// sheets start with, and in a new campaign stay there. The first few
        /// are named so a line can be checked by eye.
        /// </summary>
        /// <summary>
        /// Every kingdom's field strength, two lines each: its lords' war
        /// parties, how full they are against their limit, and its armies; then
        /// what that limit is made of, averaged per party, largest part first.
        ///
        /// Written for a report that Vlandia was losing its war with small
        /// armies against Battania's large ones. Two different causes look the
        /// same from the map, and only one of them could be this mod's. A lower
        /// limit comes from the leader -- Steward skill and a string of perks
        /// across Leadership, One and Two Handed, Athletics, Bow, Tactics and
        /// Scouting (DefaultPartySizeLimitModel), which the starting curve can
        /// move -- or from clan tier and kingdom policy, which it cannot.
        /// Parties short of their limit are gold, recruits and losses instead.
        /// The limit is read through the game's own model with its lines, so
        /// every part carries the name the game gives it.
        ///
        /// A third line is the ruler alone: who he is, whether TaleWorlds wrote
        /// him or the campaign made him, his party against its limit, where
        /// that limit ranks among his own lords, and what it is made of. For
        /// the question of whether a king who inherits the crown still rides
        /// with a king's party. The office itself is worth 20 men (Faction
        /// Leader Bonus) and 60 more under the Royal Guard policy; everything
        /// else a first-generation king has over his lords is his own sheet --
        /// Steward, Leadership and their perks -- and an heir has his own.
        /// </summary>
        internal static List<string> Kingdoms()
        {
            List<string> lines = new List<string>();
            PartySizeLimitModel model = Campaign.Current != null && Campaign.Current.Models != null
                ? Campaign.Current.Models.PartySizeLimitModel : null;

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom == null || kingdom.IsEliminated) continue;

                List<int> men = new List<int>();
                List<int> limits = new List<int>();
                List<int> steward = new List<int>();
                List<int> leadership = new List<int>();
                List<int> tiers = new List<int>();
                List<int> gold = new List<int>();
                int lords = 0, held = 0;
                Dictionary<string, float> parts = new Dictionary<string, float>();
                int rulerMen = -1, rulerLimit = -1;
                StringBuilder rulerParts = new StringBuilder();

                foreach (Clan clan in kingdom.Clans)
                {
                    if (clan == null) continue;

                    // Whether the purse or the cells are what keep a kingdom's
                    // parties empty. Gold is the one input to recruiting this
                    // mod spends from -- a robbed lord buys his way back into
                    // kit -- and a lord in a cell leads no party at all.
                    gold.Add(clan.Gold);
                    if (clan.Heroes != null)
                    {
                        foreach (Hero member in clan.Heroes)
                        {
                            if (member == null || !member.IsAlive || member.IsChild || !member.IsLord) continue;
                            lords++;
                            if (member.IsPrisoner) held++;
                        }
                    }

                    if (clan.WarPartyComponents == null) continue;

                    foreach (WarPartyComponent war in clan.WarPartyComponents)
                    {
                        try
                        {
                            MobileParty party = war != null ? war.MobileParty : null;
                            if (party == null || party.LeaderHero == null || party.Party == null) continue;
                            if (party == MobileParty.MainParty) continue;

                            men.Add(party.MemberRoster.TotalManCount);
                            limits.Add(party.Party.PartySizeLimit);
                            steward.Add(party.LeaderHero.GetSkillValue(DefaultSkills.Steward));
                            leadership.Add(party.LeaderHero.GetSkillValue(DefaultSkills.Leadership));
                            tiers.Add(clan.Tier);

                            bool isRuler = party.LeaderHero == kingdom.Leader;
                            if (isRuler)
                            {
                                rulerMen = party.MemberRoster.TotalManCount;
                                rulerLimit = party.Party.PartySizeLimit;
                            }

                            if (model == null) continue;
                            ExplainedNumber limit = model.GetPartyMemberSizeLimit(party.Party, true);
                            foreach (System.ValueTuple<string, float> line in limit.GetLines())
                            {
                                string name = string.IsNullOrEmpty(line.Item1) ? "?" : line.Item1;
                                float sum;
                                parts.TryGetValue(name, out sum);
                                parts[name] = sum + line.Item2;

                                if (isRuler)
                                {
                                    rulerParts.Append(rulerParts.Length == 0 ? " " : ", ")
                                              .Append(name).Append('=').Append(line.Item2.ToString("0"));
                                }
                            }
                        }
                        catch
                        {
                            // One unreadable party must not cost the kingdom.
                        }
                    }
                }

                if (men.Count == 0) continue;

                int total = 0, room = 0;
                for (int i = 0; i < men.Count; i++) { total += men[i]; room += limits[i]; }

                int armies = 0, armyMen = 0;
                if (kingdom.Armies != null)
                {
                    foreach (Army army in kingdom.Armies)
                    {
                        if (army == null) continue;
                        armies++;
                        armyMen += army.TotalManCount;
                    }
                }

                lines.Add(kingdom.StringId
                          + " parties=" + men.Count
                          + " men=" + total
                          + " fill=" + (room > 0 ? total * 100 / room : 0) + "%"
                          + " men " + Percentiles(men)
                          + " | limit " + Percentiles(limits)
                          + " | armies=" + armies + " armyMen=" + armyMen
                          + " | clanTier " + Percentiles(tiers)
                          + " | steward " + Percentiles(steward)
                          + " | leadership " + Percentiles(leadership)
                          + " | clanGold " + Percentiles(gold)
                          + " | lordsHeld=" + held + "/" + lords);

                List<KeyValuePair<string, float>> ranked = new List<KeyValuePair<string, float>>(parts);
                ranked.Sort((a, b) => b.Value.CompareTo(a.Value));
                StringBuilder made = new StringBuilder(kingdom.StringId + " limitPerParty");
                for (int i = 0; i < ranked.Count; i++)
                {
                    made.Append(i == 0 ? " " : ", ")
                        .Append(ranked[i].Key).Append('=')
                        .Append((ranked[i].Value / men.Count).ToString("0.0"));
                }
                lines.Add(made.ToString());

                Hero ruler = kingdom.Leader;
                if (ruler != null)
                {
                    int larger = 0;
                    for (int i = 0; i < limits.Count; i++)
                    {
                        if (limits[i] > rulerLimit) larger++;
                    }

                    lines.Add(kingdom.StringId + " ruler=" + ruler.Name
                              + " age=" + (int)ruler.Age
                              + " authored=" + HeroTalent.IsAuthored(ruler)
                              + " clanTier=" + (ruler.Clan != null ? ruler.Clan.Tier : 0)
                              + " steward=" + ruler.GetSkillValue(DefaultSkills.Steward)
                              + " leadership=" + ruler.GetSkillValue(DefaultSkills.Leadership)
                              + (rulerLimit < 0
                                  ? " party=none" + (ruler.IsPrisoner ? " (prisoner)" : "")
                                  : " party=" + rulerMen + "/" + rulerLimit
                                    + " limitRank=" + (larger + 1) + "/" + limits.Count
                                    + " |" + rulerParts));
                }
            }

            return lines;
        }

        internal static string MountFit()
        {
            int mounted = 0, harnessed = 0, mismatched = 0, unusable = 0;
            StringBuilder named = new StringBuilder();
            StringBuilder unusableNamed = new StringBuilder();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                if (hero == null || hero.IsChild || hero.BattleEquipment == null) continue;

                // Lords and companions: everyone the market dresses. Companions
                // were left out at first, and the rider who started this --
                // Kuruntai the Hawk, a caravan master -- is one. The player's
                // own character chooses his own gear.
                if (!hero.IsLord && hero.CompanionOf == null) continue;
                if (hero == Hero.MainHero) continue;

                ItemObject mount = hero.BattleEquipment[EquipmentIndex.Horse].Item;
                if (mount == null) continue;
                mounted++;

                // And riders carrying a weapon they cannot use from the saddle,
                // which the market now replaces first. Should fall the same
                // way the saddles do.
                for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
                {
                    ItemObject weapon = hero.BattleEquipment[SlotMapping.WeaponSlot(i)].Item;
                    if (weapon == null || ItemClassifier.IsUsableMounted(weapon, hero)) continue;

                    unusable++;
                    if (unusable <= 5)
                    {
                        unusableNamed.Append(unusable == 1 ? " e.g. " : ", ")
                                     .Append(hero.Name).Append('(').Append(weapon.StringId).Append(')');
                    }
                    break;
                }

                ItemObject harness = hero.BattleEquipment[EquipmentIndex.HorseHarness].Item;
                if (harness == null) continue;
                harnessed++;

                if (MarketScanner.HarnessFits(harness, mount)) continue;
                mismatched++;
                if (mismatched <= 5)
                {
                    named.Append(mismatched == 1 ? " e.g. " : ", ")
                         .Append(hero.Name).Append('(').Append(mount.StringId)
                         .Append('+').Append(harness.StringId).Append(')');
                }
            }

            return "lords+companions mounted=" + mounted + " harnessed=" + harnessed
                   + " mismatched=" + mismatched + named
                   + " | unusableMounted=" + unusable + unusableNamed;
        }

        /// <summary>
        /// Who has sworn vengeance on whom, and what that and a prisoner's
        /// name do to robbery. The census for one promise and one check.
        ///
        /// The promise is that a robbed house answers the house that robbed
        /// it, once for each robbery, without the robberies of the whole map
        /// running away. oaths is the ledger: how many vengeances are
        /// outstanding in the game's log, and between how many pairs of
        /// houses -- a pair being one house and the house it swore against,
        /// so two houses that have each robbed the other are two. The count
        /// grows for years, because two houses have to meet before one can
        /// collect, and levels off when the oldest oaths begin to lapse:
        /// simulated, about a hundred and seventy by the thirteenth year and
        /// about two hundred and thirty from the twentieth.
        ///
        /// perCapture says what the rules do today to every capture that could
        /// happen between lords at war: what a captor would do on his own
        /// character, what the prisoner's name adds for an honourable captor,
        /// what vengeance adds where an oath is held, and for how many of
        /// those captures one is. own is the part the robbery dial was
        /// calibrated on, and nothing here changes it.
        ///
        /// The check is that none of it moves how the houses stand with one
        /// another. A robbery of character costs the robber's house much
        /// what it always cost, with the one house he robbed, and vengeance
        /// and justice cost nothing, so the standing lines should drift only
        /// as the campaign drifts them: simulated, a mean near -4 across all
        /// pairs at thirteen years with one pair in twenty at minus thirty or
        /// worse. Relation between AI lords is not decoration -- clans leave
        /// kingdoms on it, armies cost influence by it, marriages and
        /// alliances are refused over it -- which is why the lines are kept
        /// although nothing here is meant to move them.
        ///
        /// The player line is the same reading for him alone, with the lords
        /// at war with him as captors and his own name beside it:
        /// swornAgainst is the oaths held against his house and by how many
        /// houses, owedTo the ones his house holds, and belowMinus30 the
        /// captors the game itself lets execute a captive they hold
        /// (PlayerCaptivityCampaignBehavior.OnPrisonerTaken, two in a hundred
        /// below that standing). session is what has happened since the
        /// campaign was loaded (RobberyTally).
        /// </summary>
        internal static List<string> Feuds()
        {
            List<string> lines = new List<string>();
            if (Campaign.Current == null || Hero.MainHero == null) return lines;

            // Houses that can be on either end of a robbery: led, alive and
            // not bandits. The player's own is read separately, below.
            List<Clan> houses = new List<Clan>();

            foreach (Clan clan in Clan.All)
            {
                if (clan == null || clan.IsEliminated || clan.IsBanditFaction) continue;
                if (clan == Clan.PlayerClan) continue;

                Hero leader = clan.Leader;
                if (leader == null || !leader.IsAlive) continue;

                houses.Add(clan);
            }

            // The ledger, read once: how many oaths each house holds against
            // each other house.
            int sworn;
            Dictionary<string, int> oaths = VengeanceOaths.Outstanding(out sworn);

            List<int> all = new List<int>();
            List<int> atWar = new List<int>();

            for (int i = 0; i < houses.Count; i++)
            {
                Hero one = houses[i].Leader;
                IFaction realm = houses[i].MapFaction;

                for (int j = i + 1; j < houses.Count; j++)
                {
                    int standing = one.GetRelation(houses[j].Leader);
                    IFaction other = houses[j].MapFaction;

                    all.Add(standing);
                    if (realm != null && other != null && realm != other
                        && FactionManager.IsAtWarAgainstFaction(realm, other))
                    {
                        atWar.Add(standing);
                    }
                }
            }

            lines.Add("houses=" + houses.Count + " oaths outstanding=" + sworn + " pairs=" + oaths.Count);
            lines.Add("standing all   " + Percentiles(all)
                      + " atOrBelowMinus30=" + Share(AtOrBelow(all, -30), all.Count));
            lines.Add("standing atWar " + Percentiles(atWar)
                      + " atOrBelowMinus30=" + Share(AtOrBelow(atWar, -30), atWar.Count));

            // Every lord this mod may strip, and how many of each house are
            // known for what: minus two, minus one, and everybody else.
            List<Hero> lords = new List<Hero>();
            Dictionary<Clan, int[]> names = new Dictionary<Clan, int[]>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (hero == null || hero == Hero.MainHero || !HeroFilter.IsEligible(hero)) continue;

                    Clan clan = hero.Clan;
                    if (clan == null || clan == Clan.PlayerClan || clan.IsBanditFaction) continue;

                    lords.Add(hero);

                    int[] counts;
                    if (!names.TryGetValue(clan, out counts))
                    {
                        counts = new int[3];
                        names[clan] = counts;
                    }

                    int level = hero.GetTraitLevel(DefaultTraits.Honor);
                    counts[level <= -2 ? 0 : (level == -1 ? 1 : 2)]++;
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }

            // Every capture that could happen today between lords at war,
            // each counted once: this captor, a lord of that house.
            float rate = Settings.RobberyMultiplier();
            int[] levels = { -2, -1, 0 };
            double own = 0, named = 0, total = 0;
            long pairs = 0, owedPairs = 0;

            foreach (Hero captor in lords)
            {
                IFaction mine = captor.MapFaction;
                if (mine == null) continue;

                // An outlaw company's man robs as a bandit does, whatever his
                // character: the same test the robbery itself makes.
                bool outlaw = PlunderService.RobsAsBandit(captor);

                int h = captor.GetTraitLevel(DefaultTraits.Honor);
                int m = captor.GetTraitLevel(DefaultTraits.Mercy);
                int g = captor.GetTraitLevel(DefaultTraits.Generosity);
                int c = captor.GetTraitLevel(DefaultTraits.Calculating);
                int roguery = captor.GetSkillValue(DefaultSkills.Roguery);

                foreach (Clan house in houses)
                {
                    if (house == captor.Clan) continue;

                    int[] counts;
                    if (!names.TryGetValue(house, out counts)) continue;

                    IFaction theirs = house.MapFaction;
                    if (theirs == null || theirs == mine) continue;
                    if (!FactionManager.IsAtWarAgainstFaction(mine, theirs)) continue;

                    int standing = captor.GetRelation(house.Leader);

                    float vengeance = oaths.ContainsKey(VengeanceOaths.Key(captor.Clan, house))
                        ? PlunderRules.Vengeance(standing, PlunderRules.Kinship.None, Settings.RobberyRate)
                        : 0f;

                    for (int k = 0; k < levels.Length; k++)
                    {
                        int n = counts[k];
                        if (n == 0) continue;

                        float character;
                        float chance = PlunderRules.Chance(outlaw, h, m, g, c, roguery, standing,
                                                           PlunderRules.Kinship.None, levels[k], rate,
                                                           out character);
                        float final = vengeance > chance ? vengeance : chance;

                        pairs += n;
                        own += n * (double)character;
                        named += n * (double)chance;
                        total += n * (double)final;
                        if (vengeance > 0f) owedPairs += n;
                    }
                }
            }

            lines.Add("perCapture atWar pairs=" + pairs
                      + " own=" + Share(own, pairs)
                      + " justice=+" + Share(named - own, pairs)
                      + " vengeance=+" + Share(total - named, pairs)
                      + " total=" + Share(total, pairs)
                      + " | owed=" + Share(owedPairs, pairs));

            // And the one house the player can do anything about.
            Hero player = Hero.MainHero;
            Clan hisHouse = player.Clan;
            IFaction banner = player.MapFaction;
            List<int> hisStanding = new List<int>();
            int swornAgainstHim = 0, housesAgainstHim = 0, owedToHim = 0, housesOwingHim = 0;

            foreach (Clan house in houses)
            {
                hisStanding.Add(player.GetRelation(house.Leader));
                if (hisHouse == null) continue;

                int count;
                if (oaths.TryGetValue(VengeanceOaths.Key(house, hisHouse), out count))
                {
                    swornAgainstHim += count;
                    housesAgainstHim++;
                }

                if (oaths.TryGetValue(VengeanceOaths.Key(hisHouse, house), out count))
                {
                    owedToHim += count;
                    housesOwingHim++;
                }
            }

            double hisOwn = 0, hisTotal = 0;
            int captors = 0, executioners = 0;

            foreach (Hero captor in lords)
            {
                IFaction theirs = captor.MapFaction;
                if (banner == null || theirs == null || theirs == banner) continue;
                if (!FactionManager.IsAtWarAgainstFaction(theirs, banner)) continue;

                float captorOwn;
                float chance = PlunderService.ChanceFor(PlunderService.RobsAsBandit(captor), captor, player,
                                                        out captorOwn);
                float vengeance = hisHouse != null && oaths.ContainsKey(VengeanceOaths.Key(captor.Clan, hisHouse))
                    ? PlunderService.VengeanceFor(captor, player)
                    : 0f;

                captors++;
                hisOwn += captorOwn;
                hisTotal += vengeance > chance ? vengeance : chance;
                if (captor.GetRelation(player) < -30) executioners++;
            }

            int honourXp = 0;
            try
            {
                honourXp = Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor);
            }
            catch
            {
                // The level beside it is the part that matters.
            }

            lines.Add("player honour=" + player.GetTraitLevel(DefaultTraits.Honor)
                      + " honourXp=" + honourXp
                      + " standing " + Percentiles(hisStanding)
                      + " | swornAgainst=" + swornAgainstHim + " byHouses=" + housesAgainstHim
                      + " owedTo=" + owedToHim + " fromHouses=" + housesOwingHim
                      + " | atWar captors=" + captors
                      + " own=" + Share(hisOwn, captors)
                      + " total=" + Share(hisTotal, captors)
                      + " belowMinus30=" + executioners);

            lines.Add("session " + RobberyTally.Describe());

            return lines;
        }

        /// <summary>
        /// One lord against the player, both ways: what he would do holding
        /// the player, and what the player would be charged for stripping
        /// him. Reads only.
        /// </summary>
        internal static string FeudWith(Hero hero)
        {
            Hero player = Hero.MainHero;
            if (hero == null || player == null) return "hev: no such hero.";
            if (hero == player) return "hev: name somebody else.";

            int standing = hero.GetRelation(player);

            // Each way on its own: two houses can each hold an oath against
            // the other, and collecting one leaves the other standing.
            int sworn;
            Dictionary<string, int> oaths = VengeanceOaths.Outstanding(out sworn);

            int his = 0, mine = 0;
            if (hero.Clan != null && player.Clan != null && hero.Clan != player.Clan)
            {
                oaths.TryGetValue(VengeanceOaths.Key(hero.Clan, player.Clan), out his);
                oaths.TryGetValue(VengeanceOaths.Key(player.Clan, hero.Clan), out mine);
            }

            float own;
            float chance = PlunderService.ChanceFor(PlunderService.RobsAsBandit(hero), hero, player, out own);
            float vengeance = his > 0 ? PlunderService.VengeanceFor(hero, player) : 0f;
            float final = vengeance > chance ? vengeance : chance;

            StringBuilder text = new StringBuilder();
            text.Append(hero.Name)
                .Append(hero.Clan != null ? " of " + hero.Clan.Name : "")
                .Append(": standing with you ").Append(standing)
                .Append(", his Honor ").Append(hero.GetTraitLevel(DefaultTraits.Honor))
                .Append(", yours ").Append(player.GetTraitLevel(DefaultTraits.Honor)).Append('.');

            text.Append("\nOaths of vengeance outstanding: his house against yours ").Append(his)
                .Append(", yours against his ").Append(mine).Append('.');

            text.Append("\nHolding you he strips you ").Append(Share(final, 1)).Append(" of the time");
            if (vengeance > 0f)
            {
                text.Append(", as vengeance: it costs him nothing and answers one oath");
            }
            else
            {
                text.Append("; ").Append(Share(own, 1)).Append(" is his own character");
                if (chance > own) text.Append(", the rest your name");
            }
            if (standing < -30) text.Append(". The game itself lets him execute a captive he holds at this standing");
            text.Append('.');

            if (mine > 0)
            {
                text.Append("\nStripping him would be vengeance: it costs you nothing and answers one oath.");
            }
            else
            {
                bool hadItComing = PlunderRules.IsReprisal(hero.GetTraitLevel(DefaultTraits.Honor))
                                   && !hero.IsFriend(player);

                text.Append("\nStripping him would cost you standing=")
                    .Append(PlunderRules.AfterReprisal(RobberyCost.Standing, hadItComing))
                    .Append(" with his house and honour=").Append(RobberyReckoning.NamePrice(hadItComing))
                    .Append(hadItComing ? " (half: he has no honour and is no friend of yours)" : "")
                    .Append(", and he would swear vengeance for it.");
            }

            return text.ToString();
        }

        /// <summary>A part of a whole as a percentage, written the same on every machine.</summary>
        private static string Share(double part, long whole)
        {
            if (whole <= 0) return "n/a";
            return (part * 100.0 / whole).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>How many of these standings are at this figure or under it.</summary>
        private static int AtOrBelow(List<int> values, int line)
        {
            int count = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] <= line) count++;
            }

            return count;
        }

        internal static string Percentiles(List<int> values)
        {
            if (values.Count == 0) return "n=0";

            int[] sorted = values.ToArray();
            System.Array.Sort(sorted);

            return "n=" + sorted.Length
                   + " min=" + sorted[0]
                   + " p5=" + At(sorted, 0.05f)
                   + " p10=" + At(sorted, 0.10f)
                   + " p25=" + At(sorted, 0.25f)
                   + " p50=" + At(sorted, 0.50f)
                   + " p75=" + At(sorted, 0.75f)
                   + " p90=" + At(sorted, 0.90f)
                   + " max=" + sorted[sorted.Length - 1];
        }

        private static int At(int[] sorted, float fraction)
        {
            int index = (int)(fraction * (sorted.Length - 1));
            if (index < 0) index = 0;
            if (index >= sorted.Length) index = sorted.Length - 1;
            return sorted[index];
        }

        /// <summary>
        /// How many mounted-role lords fall under each rung of the game's own
        /// horse difficulty ladder. A threshold is only worth setting where it
        /// actually separates people.
        /// </summary>
        private static string BelowCounts(List<int> values)
        {
            int[] rungs = { 5, 10, 15, 20, 30, 40, 50 };
            StringBuilder text = new StringBuilder();

            for (int r = 0; r < rungs.Length; r++)
            {
                int n = 0;
                for (int i = 0; i < values.Count; i++)
                {
                    if (values[i] < rungs[r]) n++;
                }
                if (r > 0) text.Append(' ');
                text.Append('<').Append(rungs[r]).Append('=').Append(n);
            }
            return text.ToString();
        }

        /// <summary>
        /// Role mix and Riding, per culture, split by whether the hero existed
        /// when the campaign started or was born into it.
        ///
        /// This is the population the mount rule actually governs. Heroes who
        /// were already on the map keep whatever they have; the decision only
        /// ever falls on the ones born during play. And the mix is expected to
        /// differ sharply by culture -- in lords.xml every Khuzait lord carries
        /// a mounted role and only one Battanian in forty does -- so a single
        /// map-wide percentage would hide the thing that matters. The
        /// campaign-born rows are the ones to read.
        ///
        /// Cohort is decided from BirthDay against elapsed campaign time rather
        /// than from the character id, because a generated id only means the
        /// hero was not authored by hand, not that the campaign produced him.
        /// </summary>
        private static void ReportCohorts()
        {
            // CampaignTime.Now.ElapsedYearsUntilNow is years from now until now,
            // which is zero -- it measures forward from the instance it is read
            // on. There is no CampaignStartTime to ask either, and hardcoding
            // 1084 would break on any mod that moves the start.
            //
            // So derive the boundary from the data: every hand-authored lord
            // (character id "lord_...") already existed on day one, so the
            // latest birth year among them is at or just before the campaign's
            // start. The youngest authored characters are toddlers, which puts
            // the marker within a couple of years of the true start -- close
            // enough to separate a hero born in play from one who was not.
            double latestAuthoredBirthYear = double.MinValue;
            foreach (Hero probe in Hero.AllAliveHeroes)
            {
                try
                {
                    if (probe == null || probe.CharacterObject == null) continue;
                    if (probe.CharacterObject.StringId == null) continue;
                    if (!probe.CharacterObject.StringId.StartsWith("lord_")) continue;

                    double born = probe.BirthDay.ToYears;
                    if (born > latestAuthoredBirthYear) latestAuthoredBirthYear = born;
                }
                catch
                {
                    // Skip anything unreadable; the marker only needs the bulk.
                }
            }

            if (latestAuthoredBirthYear <= double.MinValue)
            {
                ModLog.Info("COHORT no authored lords found; cohort split unavailable");
                return;
            }

            Dictionary<string, List<int>> ridingByKey = new Dictionary<string, List<int>>();
            Dictionary<string, int> roleCounts = new Dictionary<string, int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;

                    bool bornInPlay = hero.BirthDay.ToYears > latestAuthoredBirthYear;
                    string cohort = bornInPlay ? "born" : "start";
                    string culture = CultureIdOf(hero);
                    BattleRole role = HeroAdapter.ReadRole(hero);

                    Bump(roleCounts, cohort + "|" + culture + "|" + role);

                    if (!BattleRoleRules.IsMounted(role)) continue;

                    string key = cohort + "|" + culture;
                    List<int> riding;
                    if (!ridingByKey.TryGetValue(key, out riding))
                    {
                        riding = new List<int>();
                        ridingByKey[key] = riding;
                    }
                    riding.Add(HeroAdapter.ReadSkills(hero).Get(SkillKind.Riding));
                }
                catch
                {
                    // One unreadable hero must not cost the breakdown.
                }
            }

            ModLog.Info("COHORT now=" + (int)CampaignTime.Now.ToYears
                        + " latestAuthoredBirth=" + (int)latestAuthoredBirthYear);

            foreach (KeyValuePair<string, int> pair in roleCounts)
            {
                ModLog.Info("COHORT role " + pair.Key.Replace("|", " ") + " n=" + pair.Value);
            }

            // For mounted-role heroes only: what fraction would still be mounted
            // at each candidate floor. This is the number the threshold decision
            // is actually made against.
            foreach (KeyValuePair<string, List<int>> pair in ridingByKey)
            {
                List<int> riding = pair.Value;
                ModLog.Info("COHORT riding " + pair.Key.Replace("|", " ")
                            + " " + Percentiles(riding)
                            + " | mountedAt " + MountedFractions(riding));
            }
        }

        /// <summary>
        /// The share of a mounted-role group that clears each candidate floor.
        /// Expressed as a percentage because the choice is "how many of these
        /// lords do we want on horses", not "what number feels right".
        /// </summary>
        private static string MountedFractions(List<int> riding)
        {
            int[] floors = { 5, 10, 15, 20, 30, 40, 50 };
            StringBuilder text = new StringBuilder();

            for (int f = 0; f < floors.Length; f++)
            {
                int clear = 0;
                for (int i = 0; i < riding.Count; i++)
                {
                    if (riding[i] >= floors[f]) clear++;
                }
                int pct = riding.Count == 0 ? 0 : (clear * 100) / riding.Count;
                if (f > 0) text.Append(' ');
                text.Append(floors[f]).Append("=>").Append(pct).Append('%');
            }
            return text.ToString();
        }

        /// <summary>
        /// How many different loadouts exist inside one culture and one role --
        /// what the game actually fields, against what this mod would produce.
        ///
        /// The worry this answers is repaired lords turning into copies of each
        /// other. Two levels of sameness are possible and only the first is
        /// measured here: the shape of the loadout (spear+shield+sword+javelin
        /// against spear+shield+mace+polearm). The second level -- which item
        /// fills each category -- has no variety at all by construction, since
        /// ItemCatalog.FindBest walks the catalogue in order and keeps the first
        /// item of the highest tier, so two lords of one culture and ceiling
        /// receive the identical sword.
        ///
        /// Signatures are sorted before comparison: slot order carries no
        /// meaning, and leaving it in would invent variety that is not there.
        /// Run this on a fresh campaign for the cleanest reference -- every lord
        /// is then authored, undrifted and untouched by this mod.
        /// </summary>
        /// <summary>
        /// Heroes above this age are veterans and are not the reference. This
        /// mod only ever equips lords who came of age with the generation bug,
        /// so measuring our variety against lords who have been accumulating
        /// gear and skills for forty years compares the wrong two things.
        /// </summary>
        private const float YoungLordAge = 25f;

        private static void ReportVariety(int dominanceMargin)
        {
            // Whole population first: useful context, and the only sample large
            // enough per culture to see the game's full range of shapes.
            RunVariety("VARIETY", dominanceMargin, float.MaxValue, 5);

            // Then the cohort that actually matters. Samples are far smaller, so
            // the floor drops to three -- below that a distinct-count is noise.
            RunVariety("YOUNG", dominanceMargin, YoungLordAge, 3);
        }

        private static void RunVariety(string tag, int dominanceMargin, float maximumAge, int minimumGroup)
        {
            Dictionary<string, Dictionary<string, int>> actual =
                new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, Dictionary<string, int>> planned =
                new Dictionary<string, Dictionary<string, int>>();

            Survey(true, dominanceMargin, maximumAge, actual);
            Survey(false, dominanceMargin, maximumAge, planned);

            foreach (KeyValuePair<string, Dictionary<string, int>> pair in actual)
            {
                int heroes = 0;
                foreach (KeyValuePair<string, int> sig in pair.Value) heroes += sig.Value;
                if (heroes < minimumGroup) continue;

                Dictionary<string, int> plannedCounts;
                planned.TryGetValue(pair.Key, out plannedCounts);

                ModLog.Info(tag + " " + pair.Key + " n=" + heroes
                            + " gameDistinct=" + pair.Value.Count
                            + " oursDistinct=" + (plannedCounts == null ? 0 : plannedCounts.Count));

                DumpTop(tag + "   game", pair.Key, pair.Value);
                if (plannedCounts != null) DumpTop(tag + "   ours", pair.Key, plannedCounts);
            }
        }

        private static void Survey(bool actual, int dominanceMargin, float maximumAge,
                                   Dictionary<string, Dictionary<string, int>> into)
        {
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;
                    if (hero.BattleEquipment == null) continue;
                    if (hero.Age > maximumAge) continue;

                    BattleRole role = HeroAdapter.ReadRole(hero);
                    string group = CultureIdOf(hero) + " " + role;

                    string signature = actual
                        ? ActualSignature(hero)
                        : PlannedSignature(hero, role, dominanceMargin);
                    if (signature == null) continue;

                    Dictionary<string, int> counts;
                    if (!into.TryGetValue(group, out counts))
                    {
                        counts = new Dictionary<string, int>();
                        into[group] = counts;
                    }

                    int n;
                    counts.TryGetValue(signature, out n);
                    counts[signature] = n + 1;
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }
        }

        /// <summary>The five commonest signatures of a group, most frequent first.</summary>
        private static void DumpTop(string prefix, string group, Dictionary<string, int> counts)
        {
            for (int printed = 0; printed < 5; printed++)
            {
                string best = null;
                int bestCount = 0;

                foreach (KeyValuePair<string, int> pair in counts)
                {
                    if (pair.Value <= bestCount) continue;
                    best = pair.Key;
                    bestCount = pair.Value;
                }

                if (best == null) break;
                ModLog.Info(prefix + " " + group + " x" + bestCount + " " + best);

                // Removing the winner is what makes the next pass find the
                // runner-up; these dictionaries are local to this report.
                counts.Remove(best);
            }
        }

        private static string ActualSignature(Hero hero)
        {
            List<WeaponCategory> categories = new List<WeaponCategory>();
            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                ItemObject item = hero.BattleEquipment[SlotMapping.WeaponSlot(i)].Item;
                if (item == null) continue;

                WeaponCategory category = ItemClassifier.Classify(item);
                if (category == WeaponCategory.None) continue;
                categories.Add(category);
            }
            return Render(categories, hero.BattleEquipment[EquipmentIndex.Horse].Item != null);
        }

        private static string PlannedSignature(Hero hero, BattleRole role, int dominanceMargin)
        {
            CultureObject culture = hero.Culture;
            if (culture == null && hero.Clan != null) culture = hero.Clan.Culture;

            SkillProfile skills = HeroAdapter.ReadSkills(hero);
            int clanTier = hero.Clan != null ? hero.Clan.Tier : 0;
            int ceiling = TierCeiling.Compute(clanTier, skills.MaxCombatSkill,
                                              Settings.ClanWeight,
                                              Settings.SkillWeight,
                                              Settings.MinimumTier);

            if (!CultureProfile.MountsItsLords(culture)) role = BattleRoleRules.Dismounted(role);

            WeaponCategory[] none = new WeaponCategory[SlotSnapshot.WeaponSlotCount];
            for (int i = 0; i < none.Length; i++) none[i] = WeaponCategory.None;
            SlotSnapshot empty = new SlotSnapshot(none, false, false, false, false, false, false, false);

            bool cultureMounted = CultureProfile.MountsItsLords(culture);
            MountedRangedAvailability availability = ItemCatalog.RangedAvailability(hero, culture, ceiling);

            LoadoutTarget target = LoadoutPlanner.PlanTarget(skills, empty, availability,
                                                             dominanceMargin, cultureMounted, role);
            List<PlannedSlot> plan = LoadoutPlanner.Plan(skills, empty, availability,
                                                         dominanceMargin, cultureMounted, role);

            List<WeaponCategory> categories = new List<WeaponCategory>();
            for (int i = 0; i < plan.Count; i++) categories.Add(plan[i].Category);
            return Render(categories, target.WantsMount);
        }

        /// <summary>Sorted, so slot order cannot masquerade as variety.</summary>
        private static string Render(List<WeaponCategory> categories, bool mounted)
        {
            if (categories.Count == 0) return mounted ? "<empty>+horse" : "<empty>";

            string[] names = new string[categories.Count];
            for (int i = 0; i < categories.Count; i++) names[i] = categories[i].ToString();
            System.Array.Sort(names, System.StringComparer.Ordinal);

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < names.Length; i++)
            {
                if (i > 0) text.Append('+');
                text.Append(names[i]);
            }
            if (mounted) text.Append("+horse");
            return text.ToString();
        }

        /// <summary>
        /// How many campaign-born lords the game generated badly, per culture.
        ///
        /// This sets the ceiling on any population built out of repaired heroes.
        /// The equipment symptom is the narrow measure -- sixteen lords in the
        /// lab save, three percent of those born in play -- but the underlying
        /// failure showed itself in the skills: the broken heroes had one combat
        /// skill or none at all, while healthy ones carried a spread of six.
        /// A hero generated with no skills but two weapons passes NeedsGrant and
        /// is invisible to it, so the true defect rate can only be higher than
        /// the repair rate, and by how much is the number this reports.
        ///
        /// Counts campaign-born lords only. Those present at the start were
        /// authored by hand and are not generated at all.
        /// </summary>
        private static void ReportDefectRate()
        {
            double latestAuthoredBirthYear = double.MinValue;
            foreach (Hero probe in Hero.AllAliveHeroes)
            {
                try
                {
                    if (probe == null || probe.CharacterObject == null) continue;
                    if (probe.CharacterObject.StringId == null) continue;
                    if (!probe.CharacterObject.StringId.StartsWith("lord_")) continue;

                    double born = probe.BirthDay.ToYears;
                    if (born > latestAuthoredBirthYear) latestAuthoredBirthYear = born;
                }
                catch
                {
                    // The marker only needs the bulk of the authored roster.
                }
            }
            if (latestAuthoredBirthYear <= double.MinValue) return;

            Dictionary<string, int> bornPerCulture = new Dictionary<string, int>();
            Dictionary<string, int> noSkills = new Dictionary<string, int>();
            Dictionary<string, int> oneSkill = new Dictionary<string, int>();
            Dictionary<string, int> brokenKit = new Dictionary<string, int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;
                    if (hero.BirthDay.ToYears <= latestAuthoredBirthYear) continue;

                    string culture = CultureIdOf(hero);
                    Bump(bornPerCulture, culture);

                    SkillProfile skills = HeroAdapter.ReadSkills(hero);

                    // The six weapon skills; Riding is excluded, as everywhere.
                    int nonZero = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        if (skills.Get((SkillKind)i) > 0) nonZero++;
                    }

                    if (nonZero == 0) Bump(noSkills, culture);
                    else if (nonZero == 1) Bump(oneSkill, culture);

                    if (GrantService.NeedsGrant(hero)) Bump(brokenKit, culture);
                }
                catch
                {
                    // One unreadable hero must not cost the rate.
                }
            }

            foreach (KeyValuePair<string, int> pair in bornPerCulture)
            {
                int zero, one, kit;
                noSkills.TryGetValue(pair.Key, out zero);
                oneSkill.TryGetValue(pair.Key, out one);
                brokenKit.TryGetValue(pair.Key, out kit);

                int degenerate = zero + one;
                int pct = pair.Value == 0 ? 0 : (degenerate * 100) / pair.Value;
                int kitPct = pair.Value == 0 ? 0 : (kit * 100) / pair.Value;

                ModLog.Info("DEFECT " + pair.Key
                            + " bornInPlay=" + pair.Value
                            + " zeroSkills=" + zero
                            + " oneSkillOnly=" + one
                            + " degenerate=" + degenerate + " (" + pct + "%)"
                            + " brokenKit=" + kit + " (" + kitPct + "%)");
            }
        }

        /// <summary>
        /// What a healthy lord's combat skills look like at each age, and how
        /// they are shaped.
        ///
        /// The mod is about to seed skills on repaired heroes, and the value to
        /// seed cannot be invented: a lord repaired at forty-eight should end up
        /// where his healthy contemporaries are, not where a number that felt
        /// right puts him. Two things are needed and neither is guessable.
        ///
        /// The level: the campaign's own skill-by-age curve, so a catch-up has
        /// somewhere to catch up to.
        ///
        /// The shape: a healthy lord does not carry one skill, he carries a
        /// spread -- Zoana runs 85/81/76/72/70/70, Kjarvon 187/157/155/135/68/36
        /// -- so seeding a single skill would produce something no lord in the
        /// game resembles. Reported as the median ratio of each ranked skill to
        /// the best one.
        ///
        /// Degenerate heroes (one weapon skill or none) are excluded from both:
        /// they are the population being fixed, and leaving them in would drag
        /// the target down toward the defect it is meant to repair.
        /// </summary>
        private static void ReportSkillCurve()
        {
            int[] bounds = { 18, 25, 35, 45, 55, 200 };
            string[] labels = { "18-24", "25-34", "35-44", "45-54", "55+" };

            // Split by cohort. The 55+ bucket is otherwise half authored lords,
            // seeded as veterans on day one, and reading their percentiles as a
            // progression target would import TaleWorlds' starting roster rather
            // than what a campaign actually grows.
            double latestAuthoredBirthYear = double.MinValue;
            foreach (Hero probe in Hero.AllAliveHeroes)
            {
                try
                {
                    if (probe == null || probe.CharacterObject == null) continue;
                    if (probe.CharacterObject.StringId == null) continue;
                    if (!probe.CharacterObject.StringId.StartsWith("lord_")) continue;
                    double born = probe.BirthDay.ToYears;
                    if (born > latestAuthoredBirthYear) latestAuthoredBirthYear = born;
                }
                catch
                {
                    // One unreadable hero must not cost the rest of the probe.
                    // Silent because this is a survey and the worst a skipped
                    // hero can do is move the boundary it is looking for by a
                    // year -- logging it per hero would bury the census it is
                    // part of. Every other swallow in this mod says why; this
                    // one had said nothing at all.
                }
            }

            List<int>[] authoredByBucket = new List<int>[labels.Length];
            for (int i = 0; i < labels.Length; i++) authoredByBucket[i] = new List<int>();

            List<int>[] maxByBucket = new List<int>[labels.Length];
            for (int i = 0; i < labels.Length; i++) maxByBucket[i] = new List<int>();

            // Ratio of the Nth-best weapon skill to the best, as a percentage.
            List<int>[] shape = new List<int>[6];
            for (int i = 0; i < shape.Length; i++) shape[i] = new List<int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;

                    SkillProfile skills = HeroAdapter.ReadSkills(hero);

                    int[] weapon = new int[6];
                    int nonZero = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        weapon[i] = skills.Get((SkillKind)i);
                        if (weapon[i] > 0) nonZero++;
                    }
                    if (nonZero <= 1) continue;

                    System.Array.Sort(weapon);
                    System.Array.Reverse(weapon);

                    int best = weapon[0];
                    if (best <= 0) continue;

                    for (int i = 0; i < 6; i++) shape[i].Add((weapon[i] * 100) / best);

                    bool bornInPlay = latestAuthoredBirthYear > double.MinValue
                                      && hero.BirthDay.ToYears > latestAuthoredBirthYear;

                    int age = (int)hero.Age;
                    for (int b = 0; b < labels.Length; b++)
                    {
                        if (age >= bounds[b] && age < bounds[b + 1])
                        {
                            if (bornInPlay) maxByBucket[b].Add(best);
                            else authoredByBucket[b].Add(best);
                            break;
                        }
                    }
                }
                catch
                {
                    // One unreadable hero must not cost the curve.
                }
            }

            for (int b = 0; b < labels.Length; b++)
            {
                ModLog.Info("SKILLAGE born     " + labels[b] + " " + Percentiles(maxByBucket[b]));
                ModLog.Info("SKILLAGE authored " + labels[b] + " " + Percentiles(authoredByBucket[b]));
            }

            // The line this one is read against. Measured over the 458 lord
            // sheets TaleWorlds shipped, the same way: six weapon lines sorted
            // high to low, each as a share of that sheet's own best, median at
            // each rank. It is what SkillGrowth.TargetForRank is calibrated to,
            // so the two lines together say whether the world came out the
            // shape he drew. The first four ranks are the ones the rule sets;
            // the last two are whatever a lord's own sheet and his years leave
            // behind.
            StringBuilder text = new StringBuilder("SKILLSHAPE medianRatioToBest");
            for (int i = 0; i < shape.Length; i++)
            {
                int[] sorted = shape[i].ToArray();
                if (sorted.Length == 0) { text.Append(" s").Append(i + 1).Append("=n/a"); continue; }
                System.Array.Sort(sorted);
                text.Append(" s").Append(i + 1).Append('=').Append(At(sorted, 0.50f)).Append('%');
            }
            text.Append(" | TaleWorlds' own sheets s1=100% s2=80% s3=66% s4=54% s5=44% s6=27%");
            ModLog.Info(text.ToString());
        }

        /// <summary>
        /// Every character the player has controlled, alive or dead, with their
        /// skills and the focus invested in each.
        ///
        /// Two things need it. The player is meant to be the most capable hero
        /// on the map, so their skills are the ceiling AI growth must not cross
        /// -- and that ceiling can only be read from the character themselves.
        /// And the focus allocation shows where a human actually spent a
        /// campaign's worth of points, which is the shape a deliberately built
        /// hero has, as opposed to the one the AI's own allocator produces.
        ///
        /// Dead characters are included: a campaign that has passed through
        /// several generations keeps its history in DeadOrDisabledHeroes, and
        /// the founder is usually the most developed character the save has
        /// ever held.
        /// </summary>
        /// <summary>
        /// Every living hero of the player's clan, set beside the lords his age.
        ///
        /// Written for a question the rest of the census cannot answer, because
        /// every other line filters on HeroFilter.IsEligible and that refuses
        /// heroes travelling in the main party. Those heroes are refused for a
        /// reason about equipment -- their inventory belongs to the player --
        /// and the same filter happens to gate skill growth, so a companion
        /// riding with the player gets none of it while every lord on the map
        /// gets one to three points a year for life.
        ///
        /// That may be fine. A companion in the player's party fights when the
        /// player fights, earns experience the ordinary way, and receives focus
        /// and perks by hand, which no AI lord does. Whether it comes out even
        /// depends entirely on how much a given player fights, which is not
        /// something that can be reasoned about from here -- so it is measured
        /// instead, against the cohort the man would be compared to if he were
        /// anyone else.
        ///
        /// Reports the best of the six weapon skills, which is what SKILLAGE
        /// buckets, so the two are the same measurement and the comparison is
        /// honest. The cohort median is printed on the same line: a companion
        /// well under it is falling behind the map, and over it is not.
        /// </summary>
        private static void ReportMyClan()
        {
            if (Clan.PlayerClan == null) return;

            // Which of the six the engine actually set on the player's house.
            // Recorded because the guard that used to read them refused nine of
            // his heroes, and the next person to widen that list should be able
            // to see what it catches.
            Clan mine = Clan.PlayerClan;
            ModLog.Info("MYCLAN clan=" + mine.Name
                        + " tier=" + mine.Tier
                        + " minorFaction=" + mine.IsMinorFaction
                        + " banditFaction=" + mine.IsBanditFaction
                        + " outlaw=" + mine.IsOutlaw
                        + " sect=" + mine.IsSect
                        + " nomad=" + mine.IsNomad
                        + " mafia=" + mine.IsMafia);

            int[] bounds = { 18, 25, 35, 45, 55, 200 };
            string[] labels = { "18-24", "25-34", "35-44", "45-54", "55+" };

            // The map to measure against: every lord this mod does grow.
            List<int>[] cohort = new List<int>[labels.Length];
            List<int>[] cohortTotal = new List<int>[labels.Length];
            for (int i = 0; i < cohort.Length; i++)
            {
                cohort[i] = new List<int>();
                cohortTotal[i] = new List<int>();
            }

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligibleToGrow(hero)) continue;
                    int b = Bucket(bounds, (int)hero.Age);
                    if (b < 0) continue;
                    int best = BestWeaponSkill(hero);
                    if (best > 0) cohort[b].Add(best);

                    int total = AllSkillTotal(hero);
                    if (total > 0) cohortTotal[b].Add(total);
                }
                catch
                {
                    // One unreadable hero must not cost the comparison.
                }
            }

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (hero == null || hero.Clan != Clan.PlayerClan) continue;
                    if (hero.IsChild || hero.IsTemplate) continue;

                    int b = Bucket(bounds, (int)hero.Age);
                    int best = BestWeaponSkill(hero);
                    int median = b >= 0 ? Median(cohort[b]) : 0;

                    MobileParty party = hero.PartyBelongedTo;
                    string place;
                    if (hero == Hero.MainHero) place = "you";
                    else if (party != null && party == MobileParty.MainParty) place = "inYourParty";
                    else if (party != null && party.LeaderHero == hero) place = "leadsAParty";
                    else if (party != null) place = "inAParty";
                    else place = "noParty";

                    int total = AllSkillTotal(hero);
                    int totalMedian = b >= 0 ? Median(cohortTotal[b]) : 0;

                    ModLog.Info("MYCLAN " + hero.Name
                                + " age=" + (int)hero.Age
                                + " where=" + place
                                + " grown=" + HeroFilter.IsEligibleToGrow(hero)
                                // The repair's own answer, not the general
                                // one: a companion can be robbed and still
                                // never be handed the starting kit. The kit
                                // also needs its moment -- once, when the game
                                // makes his gear -- which this cannot show.
                                + " geared=" + HeroFilter.IsEligibleForRepair(hero)
                                // The third filter, and it was missing. A
                                // census that reports two of the three answers
                                // cannot show a hero the mod repairs and grows
                                // but never shops for, which is exactly what
                                // every companion was.
                                + " shops=" + HeroFilter.IsEligibleToShop(hero)
                                + " why=" + (HeroFilter.WhyIneligible(hero) ?? "-")
                                + (HeroFilter.WhyIneligible(hero) == null
                                   && !HeroFilter.IsEligibleForRepair(hero)
                                   ? " noKit=" + HeroFilter.WhyNoRepair(hero) : "")
                                + " isLord=" + hero.IsLord
                                + " companionOf=" + (hero.CompanionOf != null
                                                     ? hero.CompanionOf.Name.ToString() : "none")
                                + " cohort=" + (b >= 0 ? labels[b] : "?")
                                + " | bestWeapon=" + best + " vs " + median
                                + " (" + Signed(best - median) + ")"
                                + " | allSkills=" + total + " vs " + totalMedian
                                + " (" + Signed(total - totalMedian) + ")");

                    // Every skill by name, because the totals say a man is
                    // behind without saying at what. A companion who lost
                    // ground in Trade and Medicine while keeping up with a
                    // sword is a different problem from one who is simply
                    // older than his level.
                    ModLog.Info("MYCLAN " + hero.Name + " skills " + EverySkill(hero));
                }
                catch
                {
                    // Reported best-effort; one bad hero must not stop the rest.
                }
            }
        }

        /// <summary>
        /// Every skill added together: one number for how developed a person is.
        ///
        /// A sum rather than an average because a hero with nothing in half the
        /// skill list should read as less developed than one who is competent
        /// across it, and an average would hide exactly that.
        /// </summary>
        private static int AllSkillTotal(Hero hero)
        {
            if (hero == null) return 0;

            int total = 0;
            foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
            {
                if (skill == null) continue;
                total += hero.GetSkillValue(skill);
            }
            return total;
        }

        /// <summary>Every skill by name and value, compactly, for one hero.</summary>
        private static string EverySkill(Hero hero)
        {
            StringBuilder text = new StringBuilder();
            foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
            {
                if (skill == null) continue;
                if (text.Length > 0) text.Append(' ');
                text.Append(skill.StringId).Append('=').Append(hero.GetSkillValue(skill));
                text.Append('/').Append(hero.HeroDeveloper != null
                                        ? hero.HeroDeveloper.GetFocus(skill) : 0);
            }
            return text.ToString();
        }

        private static string Signed(int n)
        {
            return n >= 0 ? "+" + n : n.ToString();
        }

        /// <summary>The best of the six weapon skills, as SKILLAGE measures it.</summary>
        private static int BestWeaponSkill(Hero hero)
        {
            if (hero == null) return 0;

            SkillProfile skills = HeroAdapter.ReadSkills(hero);
            int best = 0;
            for (int i = 0; i < 6; i++)
            {
                int v = skills.Get((SkillKind)i);
                if (v > best) best = v;
            }
            return best;
        }

        private static int Bucket(int[] bounds, int age)
        {
            for (int b = 0; b + 1 < bounds.Length; b++)
            {
                if (age >= bounds[b] && age < bounds[b + 1]) return b;
            }
            return -1;
        }

        private static int Median(List<int> values)
        {
            if (values == null || values.Count == 0) return 0;
            values.Sort();
            return values[values.Count / 2];
        }

        /// <summary>
        /// The player's skills against everyone else's, and how far each hero
        /// is behind the target this mod is walking him toward.
        ///
        /// Written for one question the rest of the census cannot answer: does
        /// a lord climb the way the player does, only lower? That is the shape
        /// the design asks for -- the player fastest and furthest, the lords on
        /// a similar curve beneath him, separated by talent -- and nothing has
        /// ever checked it. The two men are grown by completely different
        /// machinery: the player earns experience by swinging a weapon, and a
        /// lord is moved by SkillGrowth on a weekly tick. There is no reason
        /// the two curves should match except that somebody looked.
        ///
        /// The line to watch is the gap. PointsStep closes twelve hundredths of
        /// the distance to the target each year and returns exactly zero once a
        /// hero arrives, so if growth were a staircase -- climb, stall at the
        /// cap, wait for a birthday to raise it, climb again -- the heroes would
        /// pile up AT their targets and gapAtOrOver would be most of them.
        ///
        /// What the arithmetic predicts instead is a steady chase. The target
        /// is peak * Maturity(age), and maturity runs from 0.55 at eighteen to
        /// 1.0 at sixty, so it rises by peak * 0.0107 a year. Growth is
        /// gap * 0.12 * talent and the target's rise is peak * 0.0107 * talent
        /// with the same peak, so the two balance where
        ///
        ///     gap * 0.12 = 150 * 0.0107   ->   gap = 13.4
        ///
        /// and the talent cancels out of it entirely. Every hero, gifted or
        /// hopeless, should settle about thirteen points short and travel
        /// alongside his target rather than sitting on it.
        ///
        /// So the census prints the prediction beside the measurement. If the
        /// gaps cluster near thirteen the curve is smooth and the rate is
        /// right; if they cluster at zero it is a staircase and the rate is too
        /// fast for the target that restrains it.
        /// </summary>
        private static void ReportChase()
        {
            // Stamped so two censuses become a rate rather than two snapshots.
            ModLog.Info("CHASE date day=" + (int)CampaignTime.Now.ToDays
                        + " daysPerYear=" + (int)CampaignTime.DaysInYear);

            Hero me = Hero.MainHero;
            if (me != null)
            {
                try
                {
                    ModLog.Info("CHASE player name=" + me.Name
                                + " age=" + (int)me.Age
                                + " level=" + me.Level
                                + " bestWeapon=" + BestWeaponSkill(me)
                                + " allSkills=" + AllSkillTotal(me));
                    ModLog.Info("CHASE player skills " + EverySkill(me));
                }
                catch
                {
                    // The player is one hero; losing him costs the comparison
                    // and not the census.
                }
            }

            List<int> best = new List<int>();
            List<int> targets = new List<int>();
            List<int> gaps = new List<int>();
            List<int> totals = new List<int>();
            int atOrOver = 0;

            // Banded by talent, because the prediction is that the band makes
            // no difference to the gap. If it does, the cancellation above is
            // wrong somewhere.
            string[] bands = { "talent<1.0", "talent1.0-1.3", "talent>1.3" };
            List<int>[] gapByBand = new List<int>[bands.Length];
            for (int i = 0; i < gapByBand.Length; i++) gapByBand[i] = new List<int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligibleToGrow(hero)) continue;

                    float talent = HeroTalent.For(hero, Talent.Combat);
                    int target = SkillGrowth.PrimaryTarget(hero.Age, talent);
                    if (target <= 0) continue;

                    int top = BestWeaponSkill(hero);
                    int gap = target - top;

                    best.Add(top);
                    targets.Add(target);
                    gaps.Add(gap);
                    totals.Add(AllSkillTotal(hero));
                    if (gap <= 0) atOrOver++;

                    int band = talent < 1.0f ? 0 : (talent <= 1.3f ? 1 : 2);
                    gapByBand[band].Add(gap);
                }
                catch
                {
                    // One unreadable hero must not cost the population.
                }
            }

            ModLog.Info("CHASE heroes n=" + best.Count
                        + " atOrOverTarget=" + atOrOver
                        + " (" + (best.Count > 0 ? atOrOver * 100 / best.Count : 0) + "%)");
            ModLog.Info("CHASE heroes bestWeapon " + Percentiles(best));
            ModLog.Info("CHASE heroes target     " + Percentiles(targets));
            ModLog.Info("CHASE heroes gap        " + Percentiles(gaps));
            ModLog.Info("CHASE heroes allSkills  " + Percentiles(totals));

            for (int i = 0; i < bands.Length; i++)
            {
                ModLog.Info("CHASE gapByTalent " + bands[i] + " " + Percentiles(gapByBand[i]));
            }

            ModLog.Info("CHASE predicted equilibriumGap=13"
                        + " (talent cancels; a median near 13 is a smooth chase,"
                        + " near 0 is a staircase and the rate is too fast)");
        }

        private static void ReportPlayerCharacters()
        {
            Hero main = Hero.MainHero;
            if (main != null) DescribeDeveloped(main, "current");

            foreach (Hero hero in Hero.DeadOrDisabledHeroes)
            {
                try
                {
                    if (hero == null) continue;

                    // Former player characters: dead, and belonging to the
                    // player's own clan. Not a perfect filter -- relatives share
                    // it -- but the skills tell the two apart at a glance.
                    if (hero.Clan == null || hero.Clan != Clan.PlayerClan) continue;

                    DescribeDeveloped(hero, hero.IsDead ? "dead" : "disabled");
                }
                catch
                {
                    // One unreadable hero must not cost the report.
                }
            }
        }

        private static void DescribeDeveloped(Hero hero, string state)
        {
            try
            {
                StringBuilder text = new StringBuilder("PLAYER ");
                text.Append(state).Append(' ').Append(hero.Name)
                    .Append(" age=").Append((int)hero.Age)
                    .Append(" level=").Append(hero.Level)
                    .Append(" | ");

                foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
                {
                    int value = hero.GetSkillValue(skill);
                    int focus = hero.HeroDeveloper != null ? hero.HeroDeveloper.GetFocus(skill) : 0;
                    if (value <= 0 && focus <= 0) continue;

                    text.Append(skill.Name).Append('=').Append(value);
                    if (focus > 0) text.Append('(').Append(focus).Append("f)");
                    text.Append(' ');
                }

                ModLog.Info(text.ToString());
            }
            catch (System.Exception ex)
            {
                ModLog.Error("PLAYER report failed for " + hero.Name
                             + ": " + ex.GetType().Name + " " + ex.Message);
            }
        }

        /// <summary>
        /// Starting attributes of the young, split by whether they were born
        /// into the player's clan.
        ///
        /// The question is whether the player's children begin life better than
        /// ordinary nobles -- higher Vigor, Control and the rest -- which would
        /// mean an inherited character has a genuinely higher ceiling than any
        /// AI lord and not merely more attention. The evidence so far argues
        /// against it: Baldimos and Terea, both inherited into the player clan
        /// and both fifty, top out at 132 and 109, squarely inside the AI band,
        /// while Lina, created at character creation, reached 288. That points
        /// at play time rather than breeding.
        ///
        /// Attributes settle it, because they are set at birth and the player
        /// cannot have spent points on a hero who has not come of age. Anyone
        /// under twenty-five is close enough to their starting values to
        /// compare; the player's own characters are excluded, since those have
        /// had points spent on them by hand.
        /// </summary>
        private static void ReportAttributes()
        {
            List<int> clanTotals = new List<int>();
            List<int> otherTotals = new List<int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (hero == null || hero.IsDead) continue;
                    if (hero == Hero.MainHero) continue;

                    // Adult lords between eighteen and twenty-five, and nothing
                    // else. This comparison has now been wrong twice, both times
                    // by admitting a population that does not belong: first every
                    // hero under twenty-five, wanderers and notables included,
                    // and then every noble CHILD, who has no attributes assigned
                    // yet and reads as six -- one per attribute, the floor. In a
                    // fresh campaign that put nearly four hundred heroes in a
                    // bucket lords.xml only has three hundred and ninety-one
                    // entries for in total, and drove the median to the minimum.
                    //
                    // The window is narrow on purpose: old enough to have
                    // attributes, young enough that the player cannot have spent
                    // points on them.
                    if (hero.IsTemplate) continue;
                    if (hero.IsChild) continue;
                    if (!hero.IsLord) continue;
                    if (hero.Age < 18f || hero.Age > 25f) continue;

                    int total = 0;
                    StringBuilder detail = new StringBuilder();
                    foreach (CharacterAttribute attribute in TaleWorlds.CampaignSystem.Extensions.Attributes.All)
                    {
                        int value = hero.GetAttributeValue(attribute);
                        total += value;
                        detail.Append(attribute.Name).Append('=').Append(value).Append(' ');
                    }

                    bool playerClan = hero.Clan != null && hero.Clan == Clan.PlayerClan;
                    if (playerClan) clanTotals.Add(total); else otherTotals.Add(total);

                    // The player's own children are few; name them so the
                    // comparison can be read case by case rather than only in
                    // aggregate.
                    if (playerClan)
                    {
                        ModLog.Info("ATTR playerClan " + hero.Name
                                    + " age=" + (int)hero.Age
                                    + " total=" + total + " | " + detail);
                    }
                }
                catch
                {
                    // One unreadable hero must not cost the comparison.
                }
            }

            ModLog.Info("ATTR playerClan adults18to25 " + Percentiles(clanTotals));
            ModLog.Info("ATTR otherLords adults18to25 " + Percentiles(otherTotals));
        }

        /// <summary>
        /// How talent actually lands across this campaign's lords, and who the
        /// gifted ones are.
        ///
        /// The distribution is triangular by construction, so the arithmetic is
        /// known: for a threshold t above the midpoint, the share above it is
        /// 2(1-t)^2. Over six hundred lords that puts roughly six above 1.90,
        /// one or two above 1.95 and almost none at the ceiling -- which is the
        /// intent, a handful of exceptional lords per campaign arrived at by
        /// probability rather than by a rule that forces them.
        ///
        /// The bottom of it is no longer triangular. A lord TaleWorlds wrote
        /// keeps his sheet as a floor, so the poor tail here is made of heroes
        /// born in play, hired out of taverns, or written weak in the first place.
        ///
        /// Arithmetic is not evidence, though. This reports where the hashes of
        /// the real hero ids actually fell, and names the top few so they can be
        /// looked up in game.
        /// </summary>
        private static void ReportTalentSpread()
        {
            List<int> all = new List<int>();
            List<string> gifted = new List<string>();
            int above190 = 0, above195 = 0, below070 = 0, atLevel = 0, pastTheBest = 0;

            // The two figures that mean something on this scale, rather than the
            // two that used to: 1.90 and 1.95 were exceptional when the dice
            // stopped at 2.07 and are merely above the middle now that they reach
            // 2.25. Level is standing with the best lord TaleWorlds ever wrote;
            // past it is the prodigy the dice are widened for.
            float best = AuthoredTalent.Floor(300, SkillGrowth.PeakNorm);

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;

                    float talent = HeroTalent.For(hero);
                    all.Add((int)(talent * 100));

                    if (talent < 0.70f) below070++;
                    if (talent > 1.90f)
                    {
                        above190++;
                        if (gifted.Count < 12)
                        {
                            gifted.Add(hero.Name + " (" + CultureIdOf(hero) + ", "
                                       + (int)hero.Age + ") " + (int)(talent * 100));
                        }
                    }
                    if (talent > 1.95f) above195++;
                    if (talent >= Talent.Level) atLevel++;
                    if (talent > best) pastTheBest++;
                }
                catch
                {
                    // One unreadable hero must not cost the spread.
                }
            }

            ModLog.Info("TALENT " + Percentiles(all));
            ModLog.Info("TALENT exceptional above190=" + above190
                        + " above195=" + above195
                        + " poor below070=" + below070
                        + " of " + all.Count);
            ModLog.Info("TALENT standing atLevelWithCaladog=" + atLevel
                        + " pastHim=" + pastTheBest
                        + " of " + all.Count
                        + " (level " + TwoPlaces(Talent.Level)
                        + ", his sheet asks " + TwoPlaces(best) + ")");

            for (int i = 0; i < gifted.Count; i++)
            {
                ModLog.Info("TALENT gifted " + gifted[i]);
            }
        }

        /// <summary>
        /// Who stands on the sheet TaleWorlds wrote him rather than on his own
        /// dice, which sheet it was, and what it came to -- with every reigning
        /// ruler alongside.
        ///
        /// Written to check the reading of TaleWorlds' sheets against figures
        /// worked out from the game's own files before any of it shipped. Of the
        /// 484 lords TaleWorlds wrote, 284 should stand on their sheet in some
        /// field -- combat 124, civil 223, naval 22 -- and 200 be left entirely
        /// to the dice. The census population adds the wanderers and every hero
        /// born in play, none of whom has a sheet of his own, so its share is
        /// lower and falls as a campaign goes on.
        ///
        /// The ten written for a station are the spot check, their figures being
        /// known: their sheets ask for Caladog 2.10 in combat, Halthdar 1.82,
        /// Derthert, Monchug and Raganvad 1.75, Unqid 1.61, Hurunag 1.54; in
        /// civil Unqid 1.75, Caladog, Garios, Lucon and Rhagaea 1.68, the other
        /// kings 1.61, Hurunag 1.54; at sea Halthdar 1.96. Three are dealt more
        /// than their sheet asks with a blade -- Lucon 1.93, Garios 1.80,
        /// Rhagaea 1.56 -- and keep it, because a sheet is a floor and not a
        /// wage. A king below the figure above means his sheet was not where it
        /// was expected.
        /// </summary>
        private static void ReportAuthoredTalent()
        {
            int combat = 0, civil = 0, naval = 0;
            List<Hero> growing = new List<Hero>();
            List<Hero> authored = new List<Hero>();
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligibleToGrow(hero)) continue;
                    growing.Add(hero);

                    if (HeroTalent.StandsOnSheet(hero, Talent.Combat)) combat++;
                    if (HeroTalent.StandsOnSheet(hero, Talent.Civil)) civil++;
                    if (HeroTalent.StandsOnSheet(hero, Talent.Naval)) naval++;
                    if (HeroTalent.IsAuthored(hero)) authored.Add(hero);
                }
                catch
                {
                    // One unreadable hero must not cost the count.
                }
            }

            ModLog.Info("TALENT sheets " + authored.Count + " of " + growing.Count
                        + " growing heroes stand on what TaleWorlds wrote in some field"
                        + " (combat=" + combat + " civil=" + civil + " naval=" + naval + ");"
                        + " the rest are left to the hash");
            ModLog.Info("TALENT floor a sheet's best skill in a field is what its man must reach:"
                        + " talent = best * " + TwoPlaces(1f + AuthoredTalent.Surplus)
                        + " / the norm of that field, so he is back on his sheet near 55 and "
                        + (int)(AuthoredTalent.Surplus * 100f) + "% above it at " + SkillGrowth.MatureAge);
            ModLog.Info("TALENT norms combat=" + SkillGrowth.PeakNorm
                        + " civil=" + SkillGrowth.CivilPeakNorm
                        + " naval=" + SkillGrowth.NavalPeakNorm
                        + ", each the most TaleWorlds wrote in that field over the talent that stands"
                        + " level with him (" + TwoPlaces(Talent.Level) + "): combat "
                        + Talent.TargetFor(SkillGrowth.PeakNorm, Talent.Level)
                        + " civil " + Talent.TargetFor(SkillGrowth.CivilPeakNorm, Talent.Level)
                        + " naval " + Talent.TargetFor(SkillGrowth.NavalPeakNorm, Talent.Level));
            ModLog.Info("TALENT prodigy the dice reach " + TwoPlaces(Talent.Maximum) + ", so the rarest hero"
                        + " finishes combat " + Talent.TargetFor(SkillGrowth.PeakNorm, Talent.Maximum)
                        + " civil " + Talent.TargetFor(SkillGrowth.CivilPeakNorm, Talent.Maximum)
                        + " naval " + Talent.TargetFor(SkillGrowth.NavalPeakNorm, Talent.Maximum)
                        + ", past the best written lord of each field and short of the "
                        + SkillGrowth.GameSkillMaximum + " the game can show");

            ReportAboveAuthored(growing);
            ReportSheetPromise(growing);

            List<Hero> shown = new List<Hero>(authored);
            foreach (Kingdom kingdom in Kingdom.All)
            {
                try
                {
                    Hero ruler = kingdom.Leader;
                    if (ruler != null && !shown.Contains(ruler)) shown.Add(ruler);
                }
                catch
                {
                    // Same reason as above.
                }
            }

            for (int i = 0; i < shown.Count; i++)
            {
                try
                {
                    Hero hero = shown[i];
                    string sheet = HeroTalent.SheetIdOf(hero);
                    string station = hero.IsKingdomLeader && hero.Clan != null && hero.Clan.Kingdom != null
                                   ? "ruler " + hero.Name + " (" + hero.Clan.Kingdom.Name
                                   : "lord " + hero.Name + " (" + (hero.Clan != null ? hero.Clan.Name.ToString() : "no clan");

                    ModLog.Info("TALENT " + station + ", " + (int)hero.Age + ")"
                                + " sheet=" + (sheet ?? "none")
                                + " written=" + HeroTalent.BestWritten(hero, Talent.Combat)
                                + "/" + HeroTalent.BestWritten(hero, Talent.Civil)
                                + "/" + HeroTalent.BestWritten(hero, Talent.Naval)
                                + " combat=" + TwoPlaces(HeroTalent.For(hero, Talent.Combat))
                                + " civil=" + TwoPlaces(HeroTalent.For(hero, Talent.Civil))
                                + " naval=" + TwoPlaces(HeroTalent.For(hero, Talent.Naval))
                                + " source=" + HeroTalent.SourceOf(hero));
                }
                catch
                {
                    // Same reason as above.
                }
            }
        }

        /// <summary>
        /// Whether the promise actually holds in this campaign: every lord
        /// TaleWorlds wrote reaches the best skill his sheet gives him, in every
        /// field, and passes it by AuthoredTalent.Surplus once he is grown.
        ///
        /// The arithmetic says it must, since the floor is derived from exactly
        /// that -- but the arithmetic runs on the sheets this installation
        /// actually has. A mod may write one above what the game can show, and
        /// such a sheet is held at the ceiling and falls short of itself. So
        /// short=0 is the expected answer and anything else names the men it
        /// failed, which is the whole reason to count it rather than assert it.
        ///
        /// The ratio is the other half of the picture. The median lord peaks far
        /// above his sheet because his dice were kinder than it -- 143% of his
        /// written best in combat on the vanilla roster, 109% in civil -- so this
        /// measures how much of the roster the floor is actually carrying, not
        /// how generous it is. The tenth percentile is the figure that moved:
        /// 82% before the sheets became a floor, 105% after.
        ///
        /// Measured at the peak rather than at his age, because the promise is
        /// about where he ends, and most of the roster is nowhere near sixty.
        /// </summary>
        private static void ReportSheetPromise(List<Hero> growing)
        {
            string[] fields = { Talent.Combat, Talent.Civil, Talent.Naval };
            for (int f = 0; f < fields.Length; f++)
            {
                List<int> ratios = new List<int>();
                List<string> named = new List<string>();
                int onSheet = 0, fellShort = 0;

                for (int i = 0; i < growing.Count; i++)
                {
                    try
                    {
                        Hero hero = growing[i];
                        int written = HeroTalent.BestWritten(hero, fields[f]);
                        if (written <= 0) continue;

                        int peak = Talent.TargetFor(SkillGrowth.PeakNormFor(fields[f]),
                                                    HeroTalent.For(hero, fields[f]));
                        ratios.Add(peak * 100 / written);
                        if (HeroTalent.StandsOnSheet(hero, fields[f])) onSheet++;
                        if (peak >= written) continue;

                        // Counted apart from the sample: three names are enough
                        // to chase a modded sheet down, and the count is the
                        // number that says whether the promise held at all.
                        fellShort++;
                        if (named.Count < 3) named.Add(hero.Name + " " + peak + " of " + written);
                    }
                    catch
                    {
                        // One unreadable hero must not cost the count.
                    }
                }

                ModLog.Info("TALENT promise " + fields[f] + " peak as a share of what he was written"
                            + " (standing on the sheet " + onSheet + "): " + Percentiles(ratios)
                            + " | short=" + fellShort
                            + (named.Count > 0 ? " " + string.Join("; ", named.ToArray()) : ""));
            }

            ReportWrittenLines(growing);
        }

        /// <summary>
        /// The other half of the promise: every single line of a sheet, not just
        /// the best of each field.
        ///
        /// Every line is aimed at where his own written profile puts it at his
        /// age (AuthoredTalent.WrittenAt). Below what he was written is expected
        /// and not counted: a young lord is meant to be, and grows into it.
        ///
        /// The count below the line is not expected to be nought, and the second
        /// census of the curve is what taught us to stop asking for that. A line
        /// that carries an age term rises every single day, while the skill
        /// chasing it moves in whole points as experience arrives, so a lord who
        /// stood exactly on his line in the morning stands a point under it a
        /// season later. Measured: the maturity ramp climbs 0.0107 a year, so a
        /// written 200 asks for about six tenths of a point more each season,
        /// and a first census that read 10 lines below read 794 three weeks
        /// later -- all of them a point or so short, none of them falling away.
        ///
        /// So the size of the gap is the figure that means something, and the
        /// three worst are named rather than the three found first. Points, not
        /// shares, because a share exaggerates at the bottom of the scale: a
        /// line of 25 that is one point short reads as 96%, the same as a line
        /// of 250 that is ten points short. A gap of a point or two is a hero
        /// mid-step. A gap of twenty is a line nobody is aiming at.
        ///
        /// The share is the other half: how far above his sheet the mod has
        /// carried each line. It was the civil middle that prompted this -- a
        /// census found trade at 60 against the 90 he wrote, medicine 62 against
        /// 90 -- so those two skills are the ones to watch, against his profile
        /// rather than against his raw figures, since a lord below his written
        /// trade at twenty-seven is on the curve rather than short of it.
        /// </summary>
        private static void ReportWrittenLines(List<Hero> growing)
        {
            List<int> ratios = new List<int>();
            List<int> gaps = new List<int>();
            int[] worst = new int[3];
            string[] named = new string[3];
            int written = 0, below = 0;

            for (int i = 0; i < growing.Count; i++)
            {
                try
                {
                    Hero hero = growing[i];
                    foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
                    {
                        if (skill == null) continue;

                        int floor = AuthoredTalent.WrittenAt(HeroTalent.WrittenIn(hero, skill), hero.Age);
                        if (floor <= 0) continue;

                        written++;
                        int has = hero.GetSkillValue(skill);
                        ratios.Add(has * 100 / floor);
                        if (has >= floor) continue;

                        below++;
                        int gap = floor - has;
                        gaps.Add(gap);

                        // The three widest, kept in order, so the sample names
                        // the lines that are actually being missed rather than
                        // whichever hero the roster happens to start with.
                        for (int w = 0; w < worst.Length; w++)
                        {
                            if (gap <= worst[w]) continue;

                            for (int m = worst.Length - 1; m > w; m--)
                            {
                                worst[m] = worst[m - 1];
                                named[m] = named[m - 1];
                            }
                            worst[w] = gap;
                            named[w] = hero.Name + " " + (skill.Name != null ? skill.Name.ToString() : "?")
                                     + " " + has + " of " + floor;
                            break;
                        }
                    }
                }
                catch
                {
                    // One unreadable hero must not cost the count.
                }
            }

            List<string> sample = new List<string>();
            for (int w = 0; w < named.Length; w++)
            {
                if (named[w] != null) sample.Add(named[w]);
            }

            ModLog.Info("TALENT written lines " + written + " chase a line; below it " + below
                        + (sample.Count > 0 ? " (widest: " + string.Join("; ", sample.ToArray()) + ")" : "")
                        + " | short by points " + Percentiles(gaps)
                        + " | value as a share of the line " + Percentiles(ratios));
        }

        /// <summary>
        /// How many heroes the dice favoured stand above every hero whose sheet
        /// decided that same field.
        ///
        /// What a written sheet does to the top of the world. A lord the dice
        /// favoured should be able to stand above the written ones, and in civil
        /// and at sea he does: over the lords TaleWorlds wrote, 46 stand above
        /// the strongest civil sheet, 1.75, and 8 above Halthdar's 1.96. In
        /// combat he no longer can. Caladog's 300 asks for 2.10, past anything
        /// the hash deals, so the count there is 0 -- by choice, taken when the
        /// sheets became a floor, and not by accident. Anything above 0 in
        /// combat means a modded roster wrote a lord higher still.
        ///
        /// Counted field by field rather than hero by hero, because a man can
        /// stand on his sheet with a ledger and on his dice with a sword, and the
        /// question is about the field.
        /// </summary>
        private static void ReportAboveAuthored(List<Hero> growing)
        {
            string[] fields = { Talent.Combat, Talent.Civil, Talent.Naval };
            float[] best = new float[fields.Length];
            Hero[] strongest = new Hero[fields.Length];
            List<float>[] dealt = new List<float>[fields.Length];
            for (int f = 0; f < fields.Length; f++) dealt[f] = new List<float>();

            for (int i = 0; i < growing.Count; i++)
            {
                try
                {
                    Hero hero = growing[i];
                    for (int f = 0; f < fields.Length; f++)
                    {
                        float talent = HeroTalent.For(hero, fields[f]);
                        if (!HeroTalent.StandsOnSheet(hero, fields[f]))
                        {
                            dealt[f].Add(talent);
                            continue;
                        }

                        if (talent <= best[f]) continue;
                        best[f] = talent;
                        strongest[f] = hero;
                    }
                }
                catch
                {
                    // One unreadable hero must not cost the comparison.
                }
            }

            for (int f = 0; f < fields.Length; f++)
            {
                int above = 0;
                for (int i = 0; i < dealt[f].Count; i++)
                {
                    if (dealt[f][i] > best[f]) above++;
                }

                ModLog.Info("TALENT above every " + fields[f] + " sheet (" + TwoPlaces(best[f])
                            + (strongest[f] != null ? ", " + strongest[f].Name : "") + "):"
                            + " " + above + " of " + dealt[f].Count + " left to the hash there");
            }
        }

        private static string TwoPlaces(float value)
        {
            return value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Every skill the game has, across the lords this mod acts on: how high
        /// they run, and how the AI has spent its focus on them.
        ///
        /// Combat was measured before anything was built for it and the numbers
        /// overturned two of my assumptions on the way. The rest of the sheet --
        /// Leadership, Steward, Medicine, Engineering, Trade, Charm, Roguery,
        /// Scouting, Tactics, Smithing, and the three War Sails skills -- has
        /// never been looked at at all, and there is no reason to think guessing
        /// would go better this time.
        ///
        /// Focus matters as much as level here. The plan for non-combat growth
        /// is to follow where the AI has already invested, since that is the
        /// game's own statement of what this lord is for; whether that
        /// investment is broad, narrow or absent decides whether the plan works.
        /// </summary>
        private static void ReportAllSkills()
        {
            Dictionary<string, List<int>> values = new Dictionary<string, List<int>>();
            Dictionary<string, List<int>> focus = new Dictionary<string, List<int>>();
            Dictionary<string, int> anyFocus = new Dictionary<string, int>();
            List<int> focusTotals = new List<int>();
            int lords = 0;

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;
                    if (hero.HeroDeveloper == null) continue;

                    lords++;
                    int totalFocus = 0;

                    foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
                    {
                        if (skill == null || skill.Name == null) continue;
                        string name = skill.Name.ToString();

                        List<int> vals;
                        if (!values.TryGetValue(name, out vals)) { vals = new List<int>(); values[name] = vals; }
                        vals.Add(hero.GetSkillValue(skill));

                        int f = hero.HeroDeveloper.GetFocus(skill);
                        totalFocus += f;

                        List<int> fs;
                        if (!focus.TryGetValue(name, out fs)) { fs = new List<int>(); focus[name] = fs; }
                        fs.Add(f);

                        if (f > 0) Bump(anyFocus, name);
                    }

                    focusTotals.Add(totalFocus);
                }
                catch
                {
                    // One unreadable hero must not cost the sheet.
                }
            }

            ModLog.Info("ALLSKILL lords=" + lords + " focusPointsPerLord " + Percentiles(focusTotals));

            // The envelope beside the values, because the question every one of
            // these lines raises is whether the mod has carried anybody past
            // what TaleWorlds wrote for that skill.
            ModLog.Info("WRITTEN highest TaleWorlds wrote per skill: " + WrittenSkills.Describe());

            foreach (KeyValuePair<string, List<int>> pair in values)
            {
                List<int> fs;
                focus.TryGetValue(pair.Key, out fs);

                int withFocus;
                anyFocus.TryGetValue(pair.Key, out withFocus);

                int pct = lords == 0 ? 0 : (withFocus * 100) / lords;

                ModLog.Info("ALLSKILL " + pair.Key
                            + " | value " + Percentiles(pair.Value)
                            + " | focus " + (fs == null ? "n=0" : Percentiles(fs))
                            + " | lordsWithFocus=" + withFocus + " (" + pct + "%)");
            }
        }

        /// <summary>
        /// How many lords the growth system currently has anything to do, and
        /// how far behind they are.
        ///
        /// A year of live campaign moved nothing at all, and there are two
        /// entirely different reasons that could happen: the system is broken,
        /// or there is nobody below their target to lift. Percentiles of skill
        /// cannot tell those apart. This can.
        ///
        /// It turned out to be the second -- in a fresh campaign TaleWorlds'
        /// authored lords sit at 175 to 200 against a target of 170, so the
        /// system correctly does nothing -- but that was a guess until it was
        /// counted, and the next time it will not be.
        /// </summary>
        private static void ReportGaps()
        {
            List<int> gaps = new List<int>();
            int behind = 0, atOrAbove = 0;
            int behindByTen = 0, behindByFifty = 0;

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;
                    if (hero.BattleEquipment == null) continue;

                    float talent = HeroTalent.For(hero, Talent.Combat);
                    int target = SkillGrowth.PrimaryTarget(hero.Age, talent);
                    if (target <= 0) continue;

                    int best = HeroAdapter.ReadSkills(hero).MaxCombatSkill;
                    int gap = target - best;

                    if (gap <= 0) { atOrAbove++; continue; }

                    behind++;
                    gaps.Add(gap);
                    if (gap >= 10) behindByTen++;
                    if (gap >= 50) behindByFifty++;
                }
                catch
                {
                    // One unreadable hero must not cost the count.
                }
            }

            ModLog.Info("GAP combat behind=" + behind + " atOrAbove=" + atOrAbove
                        + " behindBy10+=" + behindByTen + " behindBy50+=" + behindByFifty);
            ModLog.Info("GAP combat sizes " + Percentiles(gaps));

            ReportFocusGaps();
        }

        /// <summary>
        /// The same count for everything that is not a weapon, per skill.
        ///
        /// Two years of campaign proved the combat side works by showing its gap
        /// distribution shrink -- median shortfall 33 to 24 while the number of
        /// lords behind grew -- and proved nothing at all about the rest of the
        /// sheet, because percentiles of skill cannot separate "growing slowly"
        /// from "not growing". Leadership sat at 146 both times. This says
        /// whether there was anything to do.
        /// </summary>
        private static void ReportFocusGaps()
        {
            Dictionary<string, List<int>> gapsBySkill = new Dictionary<string, List<int>>();
            Dictionary<string, int> atOrAboveBySkill = new Dictionary<string, int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;
                    if (hero.HeroDeveloper == null) continue;

                    foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
                    {
                        if (skill == null || skill.Name == null) continue;

                        // The eight weapon and movement skills are grown by the
                        // equipment pass against a rank-based target, not by
                        // focus. Measuring them here reported a shortfall
                        // against a target the system does not use for them --
                        // a diagnostic describing something nobody runs, which
                        // is exactly the drift that made the old dry-run lie.
                        if (IsGrownByEquipment(skill)) continue;

                        int focus = hero.HeroDeveloper.GetFocus(skill);
                        if (focus <= 0) continue;

                        string domain = IsNaval(skill) ? Talent.Naval : Talent.Civil;
                        int target = FocusGrowth.TargetFor(hero.Age, HeroTalent.For(hero, domain),
                                                           focus, SkillGrowth.PeakNormFor(domain));
                        if (target <= 0) continue;

                        string name = skill.Name.ToString();
                        int gap = target - hero.GetSkillValue(skill);

                        if (gap <= 0) { Bump(atOrAboveBySkill, name); continue; }

                        List<int> list;
                        if (!gapsBySkill.TryGetValue(name, out list))
                        {
                            list = new List<int>();
                            gapsBySkill[name] = list;
                        }
                        list.Add(gap);
                    }
                }
                catch
                {
                    // One unreadable hero must not cost the sweep.
                }
            }

            foreach (KeyValuePair<string, List<int>> pair in gapsBySkill)
            {
                int atOrAbove;
                atOrAboveBySkill.TryGetValue(pair.Key, out atOrAbove);

                ModLog.Info("GAPFOCUS " + pair.Key
                            + " behind=" + pair.Value.Count
                            + " atOrAbove=" + atOrAbove
                            + " | " + Percentiles(pair.Value));
            }
        }

        /// <summary>
        /// The skills the equipment pass owns: six weapons plus the two the
        /// mount decision picks between. Mirrors SkillGrowthService.
        /// </summary>
        private static bool IsGrownByEquipment(SkillObject skill)
        {
            return skill == DefaultSkills.OneHanded
                   || skill == DefaultSkills.TwoHanded
                   || skill == DefaultSkills.Polearm
                   || skill == DefaultSkills.Bow
                   || skill == DefaultSkills.Crossbow
                   || skill == DefaultSkills.Throwing
                   || skill == DefaultSkills.Riding
                   || skill == DefaultSkills.Athletics;
        }

        /// <summary>
        /// War Sails skills, matched by string id so the report still runs for
        /// anyone without that DLC. Mirrors SkillGrowthService deliberately: if
        /// the two disagreed the diagnostic would describe a system nobody runs.
        /// </summary>
        private static bool IsNaval(SkillObject skill)
        {
            string id = skill.StringId;
            return id == "Mariner" || id == "Boatswain" || id == "Shipmaster";
        }

        /// <summary>
        /// The naval picture: how nautical each culture is by the game's own
        /// reckoning, how many lords actually command ships, and where their
        /// seamanship stands.
        ///
        /// The three War Sails skills currently grow the same way stewardship
        /// does -- by focus alone -- and barely move, because only six to
        /// seventeen percent of lords have any focus in them. That may be right
        /// or it may be a hole: their maxima of 260 to 280 are the highest
        /// figures anywhere on the sheet, so a handful of lords are outstanding
        /// sailors while the median has never touched a tiller.
        ///
        /// Combat is not decided by focus alone -- it follows what a lord
        /// actually carries. Two candidates exist for the naval equivalent and
        /// neither has been looked at: MobileParty.Ships says whether this lord
        /// commands a fleet at all, and CultureObject.NavalFactor is TaleWorlds'
        /// own statement of how seafaring a people is. Measure both before
        /// building anything on either.
        /// </summary>
        /// <summary>
        /// Whether the purchase engine has anything to do, measured before
        /// blaming it for doing nothing.
        ///
        /// Two numbers decide that. HEADROOM is how many tiers a lord's worn
        /// gear is below his own ceiling, slot by slot: zero headroom means the
        /// engine is right to stay quiet, and a large one means it should be
        /// firing. PURSE is what he could actually spend today. Reporting them
        /// together is what distinguishes "nothing to buy" from "cannot afford
        /// it" from "broken", and this project has already paid four times for
        /// not being able to tell those apart.
        /// </summary>
        private static void ReportHeadroom(float clanWeight, float skillWeight, int minimumTier)
        {
            List<int> headroom = new List<int>();
            List<int> purses = new List<int>();
            List<int> limits = new List<int>();
            List<int> ceilings = new List<int>();
            Dictionary<string, int> shortBySlot = new Dictionary<string, int>();

            int shoppers = 0, atCeiling = 0;
            BudgetService budget = new BudgetService();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligibleToShop(hero)) continue;
                    if (hero.BattleEquipment == null) continue;

                    shoppers++;

                    SkillProfile skills = HeroAdapter.ReadSkills(hero);
                    int ceiling = HeroAdapter.ReadCeiling(hero, skills, clanWeight, skillWeight, minimumTier);
                    ceilings.Add(ceiling);
                    purses.Add(budget.Wallet(hero));
                    limits.Add(budget.Available(hero));

                    int behind = 0;
                    for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
                    {
                        behind += SlotShortfall(hero, SlotMapping.WeaponSlot(i), ceiling, shortBySlot);
                    }
                    foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
                    {
                        behind += SlotShortfall(hero, slot, ceiling, shortBySlot);
                    }
                    behind += SlotShortfall(hero, EquipmentIndex.Horse, ceiling, shortBySlot);
                    behind += SlotShortfall(hero, EquipmentIndex.HorseHarness, ceiling, shortBySlot);

                    headroom.Add(behind);
                    if (behind == 0) atCeiling++;
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }

            ModLog.Info("HEADROOM shoppers=" + shoppers + " alreadyAtCeiling=" + atCeiling);
            ModLog.Info("HEADROOM tiersBehind (summed over slots) " + Percentiles(headroom));
            ModLog.Info("HEADROOM ceiling " + Percentiles(ceilings));
            ReportCatalogCap();
            ModLog.Info("HEADROOM wallet " + Percentiles(purses));
            ModLog.Info("HEADROOM perPurchaseLimit " + Percentiles(limits));

            StringBuilder bySlot = new StringBuilder("HEADROOM slotsBehind");
            foreach (KeyValuePair<string, int> pair in shortBySlot)
            {
                bySlot.Append(' ').Append(pair.Key).Append('=').Append(pair.Value);
            }
            ModLog.Info(bySlot.ToString());
        }

        /// <summary>
        /// Tiers this one slot is below what the hero could actually reach.
        ///
        /// Three things are excluded, and all of them were inflating the number.
        /// An empty slot contributes nothing, because the market only ever
        /// replaces what a hero already wears. An unranked item contributes
        /// nothing either: the engine deliberately refuses to touch one, so
        /// counting it as behind reports demand that will never be served by
        /// design. Nor does gear no merchant could have sold him, which the
        /// engine refuses to take off him for the same reason.
        ///
        /// The ceiling is narrowed to the best tier sold of this kind of item.
        /// All of it comes from ShoppingTrip.Shortfall, the number the armour
        /// order itself runs on, so the census cannot call a slot behind that
        /// the engine calls finished. His merit may say tier 6, but if nobody
        /// sells leg armour above tier 4 then he is not behind on boots, he is
        /// wearing the best boots there are.
        /// </summary>
        private static int SlotShortfall(Hero hero, EquipmentIndex slot, int ceiling,
                                         Dictionary<string, int> shortBySlot)
        {
            int behind = ShoppingTrip.Shortfall(hero, slot, ceiling);
            if (behind <= 0) return 0;

            Bump(shortBySlot, SlotMapping.NameOf(slot));
            return behind;
        }

        /// <summary>
        /// What every town has on its shelves, and how much of it a lord of that
        /// town's own culture could actually be sold.
        ///
        /// Three columns, because the first version of this report answered the
        /// wrong question. It measured a town's stock against the TOWN's own
        /// culture -- a local lord shopping at home -- and returned a reassuring
        /// 90%. Lords are hardly ever at home: they campaign, they follow
        /// armies, they garrison foreign towns. What a travelling lord can buy
        /// is only the culture-neutral part of the shelf, which is the third
        /// column, and that is the number the culture policy actually rests on.
        ///
        /// That policy is the one rule in the purchase engine chosen by argument
        /// rather than measurement: a lord may only buy his own culture's gear
        /// or gear with no culture at all. If it rejects most of a market, the
        /// engine looks broken while behaving exactly as written.
        /// </summary>
        /// <summary>
        /// What the houses of the map are actually worth, per clan and not per
        /// hero.
        ///
        /// HEADROOM's wallet column repeats a leader's purse once for every lord
        /// under him, which is right for "what can this hero spend" and wrong
        /// for "how rich are the clans". This is the line to carry between two
        /// saves of different ages: the spending share turns clan wealth into
        /// the gate on item tier, so how that wealth grows over a campaign is
        /// what decides whether the gate ever opens.
        /// </summary>
        /// <summary>
        /// Does clan standing predict what a lord actually wears, or does skill?
        ///
        /// TierCeiling blends the two -- clan tier at half weight, skill at full
        /// -- inherited from DynamicLordGear and never checked against this
        /// game. It is worth checking, because the two are not comparable
        /// currencies: a clan reaches tier 4 and may found a kingdom, while a
        /// lord reaching 240 combat skill takes a lifetime. If clan tier turns
        /// out not to separate the population at all, it is noise in the
        /// ceiling and a tier-4 royal house is being denied tier-6 armour for
        /// nothing.
        ///
        /// The test is simple and the population is the answer: the gear vanilla
        /// itself put on these lords. Bucket them by clan tier and by skill, and
        /// see which dimension the worn tier actually tracks. A flat column
        /// means that dimension knows nothing.
        /// </summary>
        private static void ReportGearVsClan()
        {
            int[] clanCount = new int[8];
            int[] clanWorn = new int[8];
            int[] skillCount = new int[SkillBands.Length];
            int[] skillWorn = new int[SkillBands.Length];

            List<int> clanTiers = new List<int>();
            List<int> wornTiers = new List<int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;
                    if (hero.BattleEquipment == null) continue;

                    int worn = WornGearTier(hero);
                    if (worn < 0) continue;

                    int clanTier = hero.Clan != null ? hero.Clan.Tier : 0;
                    if (clanTier < 0) clanTier = 0;
                    if (clanTier > 7) clanTier = 7;

                    clanCount[clanTier]++;
                    clanWorn[clanTier] += worn;

                    int band = BandOf(HeroAdapter.ReadSkills(hero).MaxCombatSkill);
                    skillCount[band]++;
                    skillWorn[band] += worn;

                    clanTiers.Add(clanTier);
                    wornTiers.Add(worn);
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }

            for (int t = 0; t < clanCount.Length; t++)
            {
                if (clanCount[t] == 0) continue;
                ModLog.Info("GEARTIER byClanTier t" + t + " lords=" + clanCount[t]
                            + " meanWornTier=" + Mean2(clanWorn[t], clanCount[t] * 100));
            }

            for (int b = 0; b < SkillBands.Length; b++)
            {
                if (skillCount[b] == 0) continue;
                ModLog.Info("GEARTIER bySkill " + SkillBands[b] + " lords=" + skillCount[b]
                            + " meanWornTier=" + Mean2(skillWorn[b], skillCount[b] * 100));
            }

            ModLog.Info("GEARTIER wornTier(x100) " + Percentiles(wornTiers));

            ReportTierBySlot();
            ModLog.Info("GEARTIER clanTier " + Percentiles(clanTiers));
        }

        /// <summary>Skill bands, wide enough that each holds a real sample.</summary>
        private static readonly string[] SkillBands = { "0-79", "80-119", "120-159", "160-199", "200+" };

        private static int BandOf(int skill)
        {
            if (skill < 80) return 0;
            if (skill < 120) return 1;
            if (skill < 160) return 2;
            if (skill < 200) return 3;
            return 4;
        }

        /// <summary>
        /// The mean tier of everything a hero is wearing, times a hundred so it
        /// stays an integer.
        ///
        /// Mean over occupied slots rather than the best piece: one splendid
        /// helmet on an otherwise shabby lord is not what "he is a tier 5 lord"
        /// should mean, and the maximum would say exactly that.
        /// </summary>
        private static int WornGearTier(Hero hero)
        {
            int total = 0, slots = 0;

            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                ItemObject item = hero.BattleEquipment[SlotMapping.WeaponSlot(i)].Item;
                if (item == null) continue;
                total += (int)item.Tier + 1;
                slots++;
            }
            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                ItemObject item = hero.BattleEquipment[slot].Item;
                if (item == null) continue;
                total += (int)item.Tier + 1;
                slots++;
            }

            if (slots == 0) return -1;
            return total * 100 / slots;
        }

        /// <summary>An integer hundredth printed as a decimal, without floats.</summary>
        private static string Mean2(int scaledTotal, int scaledCount)
        {
            if (scaledCount == 0) return "?";
            int whole = scaledTotal / scaledCount;
            int frac = (scaledTotal * 100 / scaledCount) % 100;
            return whole + "." + (frac < 10 ? "0" : "") + frac;
        }

        /// <summary>
        /// What tier a lord is actually wearing, slot by slot.
        ///
        /// The averages hide the thing that matters. "TaleWorlds equips its
        /// lords at tier 4 or 5" is true of the pieces anyone looks at and false
        /// of the rest: the census had 445 lords behind on legs and 359 on
        /// gloves against 18 on body armour and 2 on helmets. A tier-2 purchase
        /// by a lord in tier-5 armour looks wrong until you see that the slot he
        /// bought for held tier-1 boots.
        ///
        /// So this reports the distribution per slot rather than one number per
        /// lord, which is the only shape that can show a well-dressed lord in
        /// cheap boots.
        /// </summary>
        /// <summary>
        /// The best tier on sale for each armour slot, the mount and its
        /// harness, in any culture: the numbers the headroom report and the
        /// armour order both measure against (ItemCatalog.BestBuyableTier).
        ///
        /// Without them the headroom report counts demand nobody can ever serve.
        /// In the campaign this was measured on, nothing of leg or hand armour
        /// was sold above tier 4 while helmets ran to 6, so a lord with a
        /// ceiling of 6 would read as two tiers behind on his boots for ever --
        /// a number about the catalogue rather than about him. The same trap as
        /// always in this project: a metric that cannot tell "the engine is not
        /// working" from "there is nothing to do".
        /// </summary>
        private static void ReportCatalogCap()
        {
            ItemObject.ItemTypeEnum[] kinds =
            {
                ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.BodyArmor,
                ItemObject.ItemTypeEnum.LegArmor, ItemObject.ItemTypeEnum.HandArmor,
                ItemObject.ItemTypeEnum.Cape, ItemObject.ItemTypeEnum.Horse,
                ItemObject.ItemTypeEnum.HorseHarness
            };

            // Buyable, not merely existing: the cap excludes quest and
            // non-merchandise gear exactly as the market does. That is why
            // horses cap at 5 here while lords are seen riding tier 6 -- those
            // mounts are not for sale, to us or to anyone.
            StringBuilder text = new StringBuilder("CATALOGCAP bestBuyableTier");
            for (int i = 0; i < kinds.Length; i++)
            {
                text.Append(' ').Append(kinds[i]).Append('=').Append(ItemCatalog.BestBuyableTier(kinds[i]));
            }
            ModLog.Info(text.ToString());
        }

        /// <summary>
        /// The gear the engine refuses to take off a hero, and why.
        ///
        /// Reported by reason rather than as one total, because the first
        /// version of this guard was built on IsUniqueItem and a census found
        /// that flag false for every item in the game -- it was protecting
        /// nothing at all. Splitting the count is what would catch the same
        /// mistake again.
        /// </summary>
        private static void ReportUniqueGear()
        {
            int uniqueInCatalog = 0, unsellableInCatalog = 0, craftedInCatalog = 0;
            Dictionary<string, int> unsellableByType = new Dictionary<string, int>();
            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (item == null) continue;
                if (item.IsUniqueItem) uniqueInCatalog++;
                if (item.NotMerchandise)
                {
                    unsellableInCatalog++;
                    Bump(unsellableByType, item.ItemType.ToString());
                }
                if (item.IsCraftedByPlayer) craftedInCatalog++;
            }

            int wearers = 0, pieces = 0, shown = 0;
            List<int> protectedTiers = new List<int>();
            StringBuilder sample = new StringBuilder("UNIQUE sample");

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero) || hero.BattleEquipment == null) continue;

                    bool any = false;
                    for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
                    {
                        any |= NoteUnique(hero, SlotMapping.WeaponSlot(i), ref pieces, ref shown, sample, protectedTiers);
                    }
                    foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
                    {
                        any |= NoteUnique(hero, slot, ref pieces, ref shown, sample, protectedTiers);
                    }
                    any |= NoteUnique(hero, EquipmentIndex.Horse, ref pieces, ref shown, sample, protectedTiers);
                    any |= NoteUnique(hero, EquipmentIndex.HorseHarness, ref pieces, ref shown, sample, protectedTiers);

                    if (any) wearers++;
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }

            ModLog.Info("UNIQUE inCatalog unique=" + uniqueInCatalog
                        + " notMerchandise=" + unsellableInCatalog
                        + " playerCrafted=" + craftedInCatalog);
            // What the 283 unsellable items actually are. "Noble gear" is an
            // inference from a twelve-item sample; this is the population.
            ModLog.Info("UNIQUE " + Tally("notMerchandiseByType", unsellableByType));
            ModLog.Info("UNIQUE protectedLords=" + wearers + " protectedPieces=" + pieces);

            // The guard's one real failure mode: a lord frozen into something
            // bad. Refusing to replace a noble sword costs him nothing, but
            // refusing to replace a tier-1 rag would strand him in it forever.
            ModLog.Info("UNIQUE protectedPieceTier " + Percentiles(protectedTiers));
            if (shown > 0) ModLog.Info(sample.ToString());
        }

        private static bool NoteUnique(Hero hero, EquipmentIndex slot, ref int pieces, ref int shown,
                                       StringBuilder sample, List<int> protectedTiers)
        {
            ItemObject worn = hero.BattleEquipment[slot].Item;
            if (!ItemCatalog.IsIrreplaceable(worn)) return false;

            pieces++;
            protectedTiers.Add(MarketScanner.TierOf(worn));
            if (shown < 12)
            {
                shown++;
                sample.Append(' ').Append(hero.Name).Append('/').Append(worn.StringId);
            }
            return true;
        }

        /// <summary>
        /// How many lords hold a perk that favours one weapon type over another
        /// inside the same category.
        ///
        /// Of 164 weapon perks in the game, exactly two discriminate within a
        /// category: "one handed axes and maces" and "two handed axes and
        /// maces". Everything else says "one handed weapons" or "polearms" and
        /// is blind to which one a lord carries. Whether it is worth teaching
        /// the market about those two depends entirely on how many lords have
        /// them, which nothing has ever measured.
        ///
        /// A handful of other perks discriminate by weapon flag rather than
        /// type -- couchable lances, polearms that can knock down, swingable
        /// polearms -- and are counted alongside.
        /// </summary>
        /// <summary>
        /// Whether banners are a slot the market could ever serve.
        ///
        /// Three questions, and any one of them can end it. Do lords carry a
        /// banner at all, or is the slot empty across the map? Does the
        /// catalogue hold banners a hero could be sold -- the unsellable-gear
        /// census counted 52 banners and every one of them NotMerchandise, which
        /// if it is the whole population means no shop can stock one. And do
        /// town rosters actually carry any?
        ///
        /// Measured before building rather than after, because a purchase
        /// engine for a slot nothing stocks is a week spent on a feature that
        /// can never fire once.
        /// </summary>
        private static void ReportBanners()
        {
            int inCatalog = 0, unsellable = 0;
            List<int> catalogTiers = new List<int>();

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (item == null || item.ItemType != ItemObject.ItemTypeEnum.Banner) continue;

                inCatalog++;
                if (item.NotMerchandise) unsellable++;
                catalogTiers.Add(MarketScanner.TierOf(item));
            }

            ModLog.Info("BANNER inCatalog=" + inCatalog + " notMerchandise=" + unsellable
                        + " buyable=" + (inCatalog - unsellable));
            ModLog.Info("BANNER catalogTier " + Percentiles(catalogTiers));

            // What lords actually carry in the slot.
            int carrying = 0, empty = 0, shown = 0;
            List<int> wornTiers = new List<int>();
            StringBuilder sample = new StringBuilder("BANNER sample");

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero) || hero.BattleEquipment == null) continue;

                    ItemObject banner = hero.BattleEquipment[EquipmentIndex.ExtraWeaponSlot].Item;
                    if (banner == null) { empty++; continue; }

                    carrying++;
                    wornTiers.Add(MarketScanner.TierOf(banner));
                    if (shown < 8)
                    {
                        shown++;
                        sample.Append(' ').Append(hero.Name).Append('/').Append(banner.StringId);
                    }
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }

            ModLog.Info("BANNER lordsCarrying=" + carrying + " lordsWithout=" + empty);
            ModLog.Info("BANNER wornTier " + Percentiles(wornTiers));
            if (shown > 0) ModLog.Info(sample.ToString());

            // And whether any town has one on the shelf. Nothing else matters if
            // this is zero.
            int townsStocking = 0, bannersInStock = 0;
            foreach (Settlement settlement in Settlement.All)
            {
                try
                {
                    if (settlement == null || !settlement.IsTown) continue;

                    List<StockEntry> stock = MarketScanner.Stock(settlement);
                    int here = 0;
                    for (int i = 0; i < stock.Count; i++)
                    {
                        if (stock[i].Item.ItemType == ItemObject.ItemTypeEnum.Banner) here++;
                    }

                    if (here > 0) townsStocking++;
                    bannersInStock += here;
                }
                catch
                {
                    // A settlement mid-transition must not cost the survey.
                }
            }

            ModLog.Info("BANNER townsStocking=" + townsStocking + " bannersOnShelves=" + bannersInStock);
        }

        private static void ReportWeaponPerks()
        {
            PerkObject bluntOneHanded = FindPerk("SwiftStrike");
            PerkObject bluntTwoHanded = FindPerk("OnTheEdge");
            PerkObject swingablePolearm = FindPerk("SharpenTheTip");
            PerkObject couchedLance = FindPerk("Guards");

            int lords = 0, one = 0, two = 0, swing = 0, couch = 0;

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;
                    lords++;

                    if (bluntOneHanded != null && hero.GetPerkValue(bluntOneHanded)) one++;
                    if (bluntTwoHanded != null && hero.GetPerkValue(bluntTwoHanded)) two++;
                    if (swingablePolearm != null && hero.GetPerkValue(swingablePolearm)) swing++;
                    if (couchedLance != null && hero.GetPerkValue(couchedLance)) couch++;
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }

            ModLog.Info("PERK found1h=" + (bluntOneHanded != null) + " found2h=" + (bluntTwoHanded != null)
                        + " lords=" + lords);
            ModLog.Info("PERK axesAndMaces oneHanded=" + one + " twoHanded=" + two
                        + " swingablePolearm=" + swing + " couchedLance=" + couch);
        }

        /// <summary>A perk by a fragment of its id, or null.</summary>
        private static PerkObject FindPerk(string fragment)
        {
            MBReadOnlyList<PerkObject> all = PerkObject.All;
            if (all == null) return null;

            for (int i = 0; i < all.Count; i++)
            {
                PerkObject perk = all[i];
                if (perk == null || perk.StringId == null) continue;
                if (perk.StringId.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0) return perk;
            }
            return null;
        }

        /// <summary>
        /// How much room there is inside one integer tier.
        ///
        /// The market only ever buys a whole tier up, so a lord holding the
        /// worst tier-4 sword in the game will never trade it for the best one.
        /// ItemObject.Tierf carries the fractional tier the game computed before
        /// rounding, so this says whether that refusal costs anything real: a
        /// tight spread inside a tier means the coarse rule loses nothing, and a
        /// wide one means it does.
        /// </summary>
        private static void ReportTierSpread()
        {
            Dictionary<string, List<int>> byTier = new Dictionary<string, List<int>>();

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (item == null || item.NotMerchandise) continue;

                int tier = (int)item.Tier + 1;
                if (tier < 1) continue;

                string key = "t" + tier;
                List<int> list;
                if (!byTier.TryGetValue(key, out list))
                {
                    list = new List<int>();
                    byTier[key] = list;
                }
                list.Add((int)(item.Tierf * 100f));
            }

            foreach (KeyValuePair<string, List<int>> pair in byTier)
            {
                ModLog.Info("TIERF " + pair.Key + " tierfx100 " + Percentiles(pair.Value));
            }
        }

        private static void ReportWornBySlot()
        {
            Dictionary<string, List<int>> bySlot = new Dictionary<string, List<int>>();
            Dictionary<string, int> emptyBySlot = new Dictionary<string, int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;
                    if (hero.BattleEquipment == null) continue;

                    for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
                    {
                        RecordWorn(hero, SlotMapping.WeaponSlot(i), bySlot, emptyBySlot);
                    }
                    foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
                    {
                        RecordWorn(hero, slot, bySlot, emptyBySlot);
                    }
                    RecordWorn(hero, EquipmentIndex.Horse, bySlot, emptyBySlot);
                    RecordWorn(hero, EquipmentIndex.HorseHarness, bySlot, emptyBySlot);
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }

            foreach (KeyValuePair<string, List<int>> pair in bySlot)
            {
                int empty;
                emptyBySlot.TryGetValue(pair.Key, out empty);
                ModLog.Info("WORN " + pair.Key + " " + Percentiles(pair.Value) + " empty=" + empty);
            }
        }

        private static void RecordWorn(Hero hero, EquipmentIndex slot,
                                       Dictionary<string, List<int>> bySlot,
                                       Dictionary<string, int> emptyBySlot)
        {
            string name = SlotMapping.NameOf(slot);
            ItemObject worn = hero.BattleEquipment[slot].Item;

            if (worn == null)
            {
                Bump(emptyBySlot, name);
                return;
            }

            List<int> tiers;
            if (!bySlot.TryGetValue(name, out tiers))
            {
                tiers = new List<int>();
                bySlot[name] = tiers;
            }
            tiers.Add((int)worn.Tier + 1);
        }

        /// <summary>
        /// How many heroes the mod now looks after who belong to a mercenary
        /// company, and how many of those companies are represented.
        ///
        /// Exists because they used to be nobody. The clan filter refused every
        /// minor faction, so seventeen companies' worth of lords never grew a
        /// skill, never replaced a lost piece and could not be robbed when
        /// captured -- which is how the miss was found, from a player asking why
        /// a fully geared mercenary lord in his dungeon offered him nothing.
        ///
        /// Expected to read a non-zero managed count on any campaign. A zero
        /// means the clan filter has gone back to refusing them, and the only
        /// visible symptom of that would be the same unanswerable dialogue.
        /// </summary>
        private static void ReportMercenaryCompanies()
        {
            int managed = 0, growing = 0;
            Dictionary<string, int> companies = new Dictionary<string, int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (hero == null || hero.Clan == null) continue;
                    if (hero.Clan == Clan.PlayerClan) continue;
                    if (!hero.Clan.IsMinorFaction && !hero.Clan.IsOutlaw) continue;
                    if (hero.Clan.IsBanditFaction) continue;

                    bool gear = HeroFilter.IsEligible(hero);
                    bool grows = HeroFilter.IsEligibleToGrow(hero);
                    if (!gear && !grows) continue;

                    if (gear) managed++;
                    if (grows) growing++;

                    string name = hero.Clan.Name != null ? hero.Clan.Name.ToString() : hero.Clan.StringId;
                    int seen;
                    companies.TryGetValue(name, out seen);
                    companies[name] = seen + 1;
                }
                catch
                {
                    // One unreadable hero must not cost the count.
                }
            }

            ModLog.Info("MERCENARIES managedForGear=" + managed + " growing=" + growing
                        + " companies=" + companies.Count);

            foreach (KeyValuePair<string, int> pair in companies)
            {
                ModLog.Info("MERCENARIES " + pair.Key + " heroes=" + pair.Value);
            }
        }

        private static void ReportClanWealth()
        {
            List<int> gold = new List<int>();
            Dictionary<string, int> byTier = new Dictionary<string, int>();

            foreach (Clan clan in Clan.All)
            {
                try
                {
                    if (clan == null || clan.Leader == null) continue;
                    if (clan.IsEliminated) continue;
                    // Bandits only. A mercenary company's purse is now worth
                    // counting: its lords shop out of it like anybody else.
                    if (clan.IsBanditFaction) continue;
                    if (clan == Clan.PlayerClan) continue;

                    gold.Add(clan.Gold);
                    Bump(byTier, "t" + clan.Tier);
                }
                catch
                {
                    // A clan mid-collapse must not cost the survey.
                }
            }

            ModLog.Info("CLANGOLD " + Percentiles(gold));
            ModLog.Info("CLANGOLD " + Tally("clansByTier", byTier));

            // What the share actually buys, in the game's own value curve:
            // tier 3 ~2,080, tier 4 ~5,700, tier 5 ~15,700, tier 6 ~43,300.
            // Printed so the share can be judged against prices rather than
            // against a feeling.
            //
            // Read from Settings, which is the whole point of the line. It used
            // BudgetService.DefaultSpendingShare, a shipped constant of 0.10
            // that nothing else had read since the live default moved to 0.30 --
            // so this counted how many clans could afford tier 6 at a third of
            // the real allowance and printed atShare=0.1 beside it. Three times
            // pessimistic, in a figure written to be used for balancing, and
            // the same mistake the class comment on BudgetService records
            // fixing once before: a census must report the campaign the player
            // is running, not the one the mod shipped with.
            float share = Settings.SpendingShare;
            int t4 = 0, t5 = 0, t6 = 0;
            for (int i = 0; i < gold.Count; i++)
            {
                int limit = (int)(gold[i] * share);
                if (limit >= 5700) t4++;
                if (limit >= 15700) t5++;
                if (limit >= 43300) t6++;
            }
            ModLog.Info("CLANGOLD clansAffording n=" + gold.Count
                        + " tier4=" + t4 + " tier5=" + t5 + " tier6=" + t6
                        + " atShare=" + share);
        }

        /// <summary>
        /// How sound the gear is: what the towns have on their shelves, grade by
        /// grade, and what every lord and companion is wearing.
        ///
        /// Written when the purchase engine turned out to be buying damaged goods
        /// (see ItemGrade.IsDamaged). The shelf count is how much of it the engine
        /// now walks past. The worn count is how much is still on backs -- bought
        /// before the rule, or handed out by the game itself, which gives every
        /// companion it spawns a Worn, Rusty and Old kit
        /// (CompanionsCampaignBehavior.AdjustEquipmentModifiers), as do mods that
        /// copy that routine for the soldiers they promote.
        ///
        /// Counted apart for the player's own party, the rest of his clan and
        /// everyone else, because the mod never touches the first: a companion
        /// riding with the player wears what he was given. The heroes wearing
        /// damaged pieces are named with the pieces, the player's own first, so a
        /// report of rusty gear can be traced to where it came from.
        /// </summary>
        private static void ReportQuality()
        {
            int[] shelves = new int[ItemGrade.Count];
            List<int>[] armourValue = new List<int>[ItemGrade.Count];
            for (int g = 0; g < ItemGrade.Count; g++) armourValue[g] = new List<int>();

            foreach (Settlement settlement in Settlement.All)
            {
                try
                {
                    if (settlement == null || !settlement.IsTown || settlement.ItemRoster == null) continue;

                    ItemRoster roster = settlement.ItemRoster;
                    for (int i = 0; i < roster.Count; i++)
                    {
                        ItemRosterElement element = roster.GetElementCopyAtIndex(i);
                        if (element.Amount <= 0) continue;

                        ItemObject item = element.EquipmentElement.Item;
                        if (item == null) continue;
                        if (!item.HasArmorComponent && item.WeaponComponent == null && !item.HasHorseComponent) continue;

                        int grade = ItemGrade.Index(MarketScanner.GradeOf(element.EquipmentElement));
                        shelves[grade] += element.Amount;
                        if (item.HasArmorComponent && grade != ItemGrade.Common)
                        {
                            armourValue[grade].Add(MarketScanner.QualityValueOf(element.EquipmentElement));
                        }
                    }
                }
                catch
                {
                    // A settlement mid-transition must not cost the survey.
                }
            }
            ModLog.Info("QUALITY shelves " + ItemGrade.Describe(shelves) + " (units of gear across every town)");

            // What each grade of armour on sale is worth, in hundredths of a tier,
            // by the game's own armour formula and never a whole tier either way.
            // Everything else is worth a flat 30 a grade and needs no line.
            for (int g = 0; g < ItemGrade.Count; g++)
            {
                if (armourValue[g].Count == 0) continue;
                ModLog.Info("QUALITY armourValue " + ItemGrade.NameOf(g) + " " + Percentiles(armourValue[g]));
            }

            int[] yourParty = new int[ItemGrade.Count];
            int[] yourClan = new int[ItemGrade.Count];
            int[] elsewhere = new int[ItemGrade.Count];
            List<string> mine = new List<string>();
            List<string> theirs = new List<string>();
            int wearingDamaged = 0;

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (hero == null || hero == Hero.MainHero || hero.IsChild || hero.IsTemplate) continue;
                    if (!hero.IsLord && hero.CompanionOf == null) continue;
                    if (hero.BattleEquipment == null) continue;

                    bool inYourParty = hero.PartyBelongedTo != null && hero.PartyBelongedTo == MobileParty.MainParty;
                    bool inYourClan = hero.Clan != null && hero.Clan == Clan.PlayerClan;
                    int[] tally = inYourParty ? yourParty : inYourClan ? yourClan : elsewhere;

                    StringBuilder pieces = null;
                    for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot;
                         slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
                    {
                        EquipmentElement element = hero.BattleEquipment[slot];
                        if (element.Item == null) continue;

                        int grade = MarketScanner.GradeOf(element);
                        tally[ItemGrade.Index(grade)]++;
                        if (!ItemGrade.IsDamaged(grade)) continue;

                        if (pieces == null) pieces = new StringBuilder();
                        else pieces.Append(", ");
                        pieces.Append(element.GetModifiedItemName())
                              .Append(" (").Append(MarketScanner.QualityValueOf(element)).Append(')');
                    }

                    if (pieces == null) continue;
                    wearingDamaged++;

                    string line = hero.Name + " (" + (inYourParty ? "yourParty" : inYourClan ? "yourClan" : "elsewhere")
                                + ", " + (hero.Clan != null ? hero.Clan.Name.ToString() : "no clan") + "): " + pieces;
                    if (inYourParty || inYourClan) mine.Add(line);
                    else theirs.Add(line);
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }

            ModLog.Info("QUALITY worn yourParty " + ItemGrade.Describe(yourParty));
            ModLog.Info("QUALITY worn yourClan " + ItemGrade.Describe(yourClan));
            ModLog.Info("QUALITY worn elsewhere " + ItemGrade.Describe(elsewhere));
            ModLog.Info("QUALITY heroesWearingDamaged=" + wearingDamaged);

            const int Named = 15;
            for (int i = 0; i < mine.Count && i < Named; i++) ModLog.Info("QUALITY damaged " + mine[i]);
            for (int i = 0; i < theirs.Count && mine.Count + i < Named; i++) ModLog.Info("QUALITY damaged " + theirs[i]);
        }

        private static void ReportMarkets()
        {
            List<int> sizes = new List<int>();
            List<int> usable = new List<int>();
            List<int> neutral = new List<int>();
            int towns = 0;

            foreach (Settlement settlement in Settlement.All)
            {
                try
                {
                    if (settlement == null || !settlement.IsTown) continue;
                    towns++;

                    List<StockEntry> stock = MarketScanner.Stock(settlement);
                    sizes.Add(stock.Count);

                    CultureObject culture = settlement.Culture;
                    int fits = 0, anyone = 0;
                    for (int i = 0; i < stock.Count; i++)
                    {
                        ItemObject item = stock[i].Item;

                        // Tier 6 as the ceiling: this counts what the shelf
                        // could ever offer anyone, not what one lord may buy.
                        if (!ItemCatalog.PassesCommonFilters(item, culture, 6)) continue;
                        fits++;

                        // Gear belonging to no culture at all, which is now the
                        // only interesting culture split on a shelf. It used to
                        // be reported as "what a foreign lord can buy", and that
                        // stopped being true the moment culture became a
                        // preference: a foreigner can buy the whole shelf, he
                        // simply values his own people's work a tier higher.
                        // See MarketRules.CultureShare.
                        if (item.Culture == null) anyone++;
                    }
                    usable.Add(fits);
                    neutral.Add(anyone);
                }
                catch
                {
                    // A settlement mid-transition must not cost the survey.
                }
            }

            ModLog.Info("MARKET towns=" + towns);
            ModLog.Info("MARKET stockPerTown " + Percentiles(sizes));
            ModLog.Info("MARKET buyableByAnyone " + Percentiles(usable));
            ModLog.Info("MARKET ofNoCulture " + Percentiles(neutral));
        }

        /// <summary>
        /// Everything the purchase engine would consider for one hero in one
        /// town, slot by slot, and what it would do. The dry run for buying,
        /// exactly as DryRun is for repairing: it calls the real decision code
        /// rather than a copy, so a divergence between what is reported and what
        /// happens cannot open up. That has happened twice already.
        /// </summary>
        public static string MarketDryRun(Hero hero, Settlement settlement, float clanWeight,
                                          float skillWeight, int minimumTier)
        {
            if (hero == null) return "hev: no hero.";
            if (settlement == null) return "hev: no settlement.";
            if (hero.BattleEquipment == null) return "hev: " + hero.Name + " has no battle equipment.";

            StringBuilder report = new StringBuilder();
            SkillProfile skills = HeroAdapter.ReadSkills(hero);
            int ceiling = HeroAdapter.ReadCeiling(hero, skills, clanWeight, skillWeight, minimumTier);

            BudgetService budget = new BudgetService();
            Clan clan = hero.Clan;

            report.AppendLine("hero=" + hero.Name + " culture=" + (hero.Culture != null ? hero.Culture.StringId : "?")
                              + " ceiling=" + ceiling + " eligible=" + HeroFilter.IsEligibleToShop(hero));
            List<StockEntry> stock = MarketScanner.Stock(settlement);
            report.AppendLine("town=" + settlement.Name + " stock=" + stock.Count);
            report.AppendLine("wallet=" + budget.Wallet(hero)
                              + " perPurchaseLimit=" + budget.Available(hero)
                              + " own=" + hero.Gold
                              + " clanRoom=" + budget.ClanRoom(clan)
                              + " reserve=" + budget.Reserve(clan)
                              + " clanGold=" + (clan != null ? clan.Gold : 0)
                              + " leader=" + (clan != null && clan.Leader == hero));

            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                DescribeSlotOffers(report, hero, settlement, stock, SlotMapping.WeaponSlot(i), ceiling, skills);
            }
            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                DescribeSlotOffers(report, hero, settlement, stock, slot, ceiling, skills);
            }
            DescribeSlotOffers(report, hero, settlement, stock, EquipmentIndex.Horse, ceiling, skills);
            DescribeSlotOffers(report, hero, settlement, stock, EquipmentIndex.HorseHarness, ceiling, skills);

            ShoppingTrip.Candidate best = ShoppingTrip.Best(hero, settlement, ceiling, budget);
            if (best == null)
            {
                ShoppingTrip.Candidate ignoringMoney = ShoppingTrip.Best(hero, settlement, ceiling, null);

                // The pile overload skips the armour order, so anything it finds
                // that the town overload did not is armour held back behind a
                // poorer piece.
                ShoppingTrip.Candidate ignoringOrder = ShoppingTrip.Best(hero, settlement, stock, ceiling,
                                                                         int.MaxValue);

                if (ignoringMoney != null)
                {
                    report.AppendLine("would buy: nothing -- priced out, best on offer is "
                                      + ignoringMoney.Offer.Item.StringId + " at " + ignoringMoney.Offer.Price
                                      + " against a limit of " + budget.Available(hero));
                }
                else if (ignoringOrder != null)
                {
                    report.AppendLine("would buy: nothing -- everything on offer is armour held back"
                                      + " behind a poorer piece (see HELD BACK above)");
                }
                else
                {
                    report.AppendLine("would buy: nothing");
                }
            }
            else
            {
                int heroPart, clanPart;
                bool affordable = budget.TrySplit(hero, best.Offer.Price, out heroPart, out clanPart);
                report.AppendLine("would buy: " + SlotMapping.NameOf(best.Slot)
                                  + " " + best.Offer.Item.StringId
                                  + " tier=" + best.Offer.Tier + " (from " + best.WornTier + ")"
                                  + " price=" + best.Offer.Price
                                  + (affordable ? " paid hero=" + heroPart + " clan=" + clanPart
                                                : " UNAFFORDABLE"));
            }

            ModLog.Info("MARKETDRYRUN\n" + report.ToString());
            return report.ToString();
        }

        /// <summary>One slot's line in the market dry run.</summary>
        private static void DescribeSlotOffers(StringBuilder report, Hero hero, Settlement settlement,
                                               List<StockEntry> stock, EquipmentIndex slot,
                                               int ceiling, SkillProfile skills)
        {
            ItemObject worn = hero.BattleEquipment[slot].Item;
            string name = SlotMapping.NameOf(slot);

            if (worn == null)
            {
                report.AppendLine("  " + name + ": empty -- the market never fills a slot");
                return;
            }

            int wornTier = MarketScanner.TierOf(worn);
            int wornFine = MarketScanner.FineTierOf(hero.BattleEquipment[slot]);
            if (wornTier >= ceiling)
            {
                // Above is not the same as at, and reading "t6 at ceiling" on a
                // lord whose ceiling is 4 hides the real finding: plenty of
                // lords already wear better than their merit says they should,
                // and the engine correctly has nothing to do for them.
                report.AppendLine("  " + name + ": " + worn.StringId + " t" + wornTier
                                  + (wornTier > ceiling ? " ABOVE ceiling " + ceiling : " at ceiling"));
                return;
            }

            CultureObject culture = ShoppingTrip.PreferredCulture(hero);
            bool mounted = hero.BattleEquipment[EquipmentIndex.Horse].Item != null;

            List<MarketOffer> offers;
            if (slot == EquipmentIndex.Horse)
            {
                offers = MarketScanner.Mounts(stock, settlement, hero, culture, ceiling, wornFine,
                                              ItemCatalog.IsOwnCulture(worn, culture), skills);
            }
            else if (slot == EquipmentIndex.HorseHarness)
            {
                offers = MarketScanner.Harnesses(stock, settlement, hero,
                                                 hero.BattleEquipment[EquipmentIndex.Horse].Item,
                                                 culture, ceiling, wornFine,
                                                 ItemCatalog.IsOwnCulture(worn, culture));
            }
            else if (slot == EquipmentIndex.Head || slot == EquipmentIndex.Body || slot == EquipmentIndex.Leg
                     || slot == EquipmentIndex.Gloves || slot == EquipmentIndex.Cape)
            {
                offers = MarketScanner.Armor(stock, settlement, hero, worn.ItemType, culture, ceiling,
                                             wornFine, ItemCatalog.IsOwnCulture(worn, culture));
            }
            else
            {
                SlotSnapshot current = HeroAdapter.ReadEquipment(hero.BattleEquipment);
                WeaponCategory category = ItemClassifier.Classify(worn);
                WeaponCategory partner = CategoryRules.TwoHandedPartner(category);
                if (partner != WeaponCategory.None && !current.Contains(partner)) partner = WeaponCategory.None;

                offers = MarketScanner.Weapons(stock, settlement, hero, category, culture,
                                               ceiling, wornFine, ItemCatalog.IsOwnCulture(worn, culture),
                                               skills, mounted, partner,
                                               WeaponPerks.FavoursAxeOrMace(hero, category));
            }

            if (offers.Count == 0)
            {
                report.AppendLine("  " + name + ": " + worn.StringId + " t" + wornTier
                                  + " room to t" + ceiling + ", nothing in stock -- "
                                  + WhyNothing(hero, stock, worn, culture, ceiling, wornFine));
                return;
            }

            EquipmentIndex behind;
            int shortfall, worst;
            bool held = ShoppingTrip.IsHeldBack(hero, slot, ceiling, out behind, out shortfall, out worst);

            report.AppendLine("  " + name + ": " + worn.StringId + " t" + wornTier
                              + " -> " + offers[0].Item.StringId + " t" + offers[0].Tier
                              + " " + offers[0].Price + "d"
                              + (offers[0].OwnClass ? "" : " (different class)")
                              + " [" + offers.Count + " offers]"
                              + (held ? " HELD BACK: " + SlotMapping.NameOf(behind) + " is " + worst
                                        + " tiers behind, this piece " + shortfall : ""));
        }

        /// <summary>
        /// Which gate emptied a slot's offers, counted over the same stock.
        ///
        /// "Nothing in stock" is three different findings wearing one label, and
        /// they lead to opposite fixes. If the shelf holds no item of this kind
        /// at all, the catalogue is thin and nothing about the engine will
        /// help -- there are 108 leg armours in the whole game against 1125
        /// helmets. If it holds them but all at the wrong tier, the engine is
        /// right and the lord has to wait for a better town. If it holds them at
        /// the right tier and nothing fits his skills or usage, that is a policy
        /// decision showing its cost, and it is the one worth revisiting.
        ///
        /// The predicates are the scanner's own, called in the scanner's order,
        /// so this attributes the real refusal rather than a second opinion.
        /// </summary>
        private static string WhyNothing(Hero hero, List<StockEntry> stock, ItemObject worn,
                                         CultureObject culture, int ceiling, int wornFine)
        {
            int sameKind, rightTier, foreign;
            string reason = Blocker(hero, stock, worn, culture, ceiling, wornFine,
                                    out sameKind, out rightTier, out foreign);

            if (reason == "emptyShelf") return "the town stocks none of that kind at all";
            if (reason == "wrongTier") return sameKind + " of that kind, none in the tier band";
            if (reason == "mountFamily") return rightTier + " at the right tier, every one a beast his people do not ride";
            if (foreign > 0)
            {
                return rightTier + " at the right tier (" + foreign
                       + " of them foreign), rejected on skill or usage";
            }
            return rightTier + " at the right tier, rejected on skill or usage";
        }

        /// <summary>
        /// Which gate stopped a slot, as one short key, plus the counts behind
        /// it. Named keys rather than an enum because they are printed straight
        /// into the tally and read back out of the log.
        /// </summary>
        private static string Blocker(Hero hero, List<StockEntry> stock, ItemObject worn, CultureObject culture,
                                      int ceiling, int wornFine,
                                      out int sameKind, out int rightTier, out int foreign)
        {
            sameKind = 0;
            rightTier = 0;
            foreign = 0;
            int outsideFamily = 0;

            bool wornOwn = ItemCatalog.IsOwnCulture(worn, culture);

            for (int i = 0; i < stock.Count; i++)
            {
                ItemObject item = stock[i].Item;
                if (item.ItemType != worn.ItemType) continue;
                sameKind++;

                bool own = ItemCatalog.IsOwnCulture(item, culture);
                if (!MarketRules.IsUpgrade(wornFine, wornOwn,
                                           stock[i].FineTier, own,
                                           stock[i].Tier, ceiling)) continue;
                rightTier++;

                // Counted, not blamed. Culture stopped refusing anything when it
                // became a preference -- see MarketRules.CultureShare -- so
                // a foreign offer that got this far is eligible, merely less
                // attractive than a local one would have been. "culture" is no
                // longer a blocker this can return, and the census will stop
                // reporting it.
                if (!own) foreign++;

                // A mount of a family his people do not ride is refused by rule,
                // not by skill, and blaming skill would send someone hunting the
                // wrong policy. See MountFamilyRules.
                if (item.ItemType == ItemObject.ItemTypeEnum.Horse && !MarketScanner.MayRide(hero, item)) outsideFamily++;
            }

            if (sameKind == 0) return "emptyShelf";
            if (rightTier == 0) return "wrongTier";
            if (outsideFamily == rightTier) return "mountFamily";
            return "skillOrUsage";
        }

        /// <summary>
        /// What the engine would actually do, right now, for every lord who is
        /// standing in a town.
        ///
        /// This is the report that predicts the campaign. HEADROOM says whether
        /// there is anything to buy in principle and MARKET says what shelves
        /// hold; only this one puts a real lord in a real town and runs the real
        /// decision code. It is also the population the engine acts on -- lords
        /// entering towns -- rather than the whole map.
        ///
        /// The blocked tally is the point. Each key leads somewhere different:
        /// emptyShelf and wrongTier mean the engine is right and the lord waits
        /// for a better market; heldBackByArmourOrder means he is waiting on a
        /// poorer piece of armour, by design; and skillOrUsage means a policy
        /// is what is stopping him, and that is the number worth arguing about.
        /// </summary>
        private static void ReportShopping(float clanWeight, float skillWeight, int minimumTier)
        {
            int inTowns = 0, wouldBuy = 0, nothingWanted = 0, pricedOut = 0, pushedDown = 0;
            List<int> foregone = new List<int>();
            BudgetService budget = new BudgetService();

            // One roster read per town rather than one per lord. Lords cluster
            // in towns, so the same shelves were being measured dozens of times
            // over for a report that runs across the whole map.
            Dictionary<string, List<StockEntry>> stockByTown = new Dictionary<string, List<StockEntry>>();
            Dictionary<string, int> blockedBy = new Dictionary<string, int>();
            Dictionary<string, int> buySlots = new Dictionary<string, int>();
            List<int> prices = new List<int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligibleToShop(hero)) continue;
                    if (hero.BattleEquipment == null) continue;

                    Settlement settlement = hero.CurrentSettlement;
                    if (settlement == null || !settlement.IsTown) continue;

                    inTowns++;

                    SkillProfile skills = HeroAdapter.ReadSkills(hero);
                    int ceiling = HeroAdapter.ReadCeiling(hero, skills, clanWeight, skillWeight, minimumTier);

                    // Asked twice on purpose: once as the engine will run, and
                    // once with money no object. The difference between the two
                    // is the whole effect of the spending share.
                    ShoppingTrip.Candidate best = ShoppingTrip.Best(hero, settlement, ceiling, budget);
                    ShoppingTrip.Candidate rich = ShoppingTrip.Best(hero, settlement, ceiling, null);

                    if (best != null)
                    {
                        wouldBuy++;
                        Bump(buySlots, SlotMapping.NameOf(best.Slot));
                        prices.Add(best.Offer.Price);

                        // The share almost never leaves a lord with nothing --
                        // he drops to a cheaper upgrade instead, which is the
                        // behaviour we want and is invisible in a count of
                        // refusals. This is where the money actually shows.
                        if (rich != null && rich.Offer.Price > best.Offer.Price)
                        {
                            pushedDown++;
                            foregone.Add(rich.Offer.Price - best.Offer.Price);
                        }
                        continue;
                    }

                    // Nothing affordable is not the same as nothing on offer.
                    if (rich != null)
                    {
                        pricedOut++;
                        continue;
                    }

                    // Nothing on offer: say what stopped every slot that had
                    // room, so a quiet engine can be read.
                    CultureObject culture = ShoppingTrip.PreferredCulture(hero);

                    List<StockEntry> stock;
                    if (!stockByTown.TryGetValue(settlement.StringId, out stock))
                    {
                        stock = MarketScanner.Stock(settlement);
                        stockByTown[settlement.StringId] = stock;
                    }

                    bool anyRoom = false;

                    for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
                    {
                        anyRoom |= TallyBlock(hero, SlotMapping.WeaponSlot(i), stock, culture, ceiling, blockedBy);
                    }
                    foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
                    {
                        anyRoom |= TallyBlock(hero, slot, stock, culture, ceiling, blockedBy);
                    }
                    anyRoom |= TallyBlock(hero, EquipmentIndex.Horse, stock, culture, ceiling, blockedBy);
                    anyRoom |= TallyBlock(hero, EquipmentIndex.HorseHarness, stock, culture, ceiling, blockedBy);

                    if (!anyRoom) nothingWanted++;
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }

            ModLog.Info("SHOPPING lordsInTowns=" + inTowns
                        + " wouldBuyNow=" + wouldBuy
                        + " boughtCheaperBecauseOfShare=" + pushedDown
                        + " pricedOutByShare=" + pricedOut
                        + " alreadyAtCeilingEverywhere=" + nothingWanted);
            ModLog.Info("SHOPPING foregoneByShare " + Percentiles(foregone));
            ModLog.Info("SHOPPING " + Tally("blockedSlotsBy", blockedBy));
            ModLog.Info("SHOPPING " + Tally("wouldBuySlot", buySlots));
            ModLog.Info("SHOPPING price " + Percentiles(prices));
        }

        /// <summary>
        /// Records why one slot with room found nothing. Returns whether the
        /// slot had room at all, so the caller can tell "at his ceiling
        /// everywhere" from "blocked in every slot".
        /// </summary>
        private static bool TallyBlock(Hero hero, EquipmentIndex slot, List<StockEntry> stock,
                                       CultureObject culture, int ceiling, Dictionary<string, int> blockedBy)
        {
            ItemObject worn = hero.BattleEquipment[slot].Item;
            if (worn == null) return false;

            int wornTier = MarketScanner.TierOf(worn);
            if (wornTier >= ceiling) return false;

            int sameKind, rightTier, foreign;
            string reason = Blocker(hero, stock, worn, culture, ceiling, MarketScanner.FineTierOf(hero.BattleEquipment[slot]),
                                    out sameKind, out rightTier, out foreign);

            // Armour has no skill or usage to fail, so a piece that passed the
            // tier test and still found nothing is, short of an excluded item,
            // the armour order making him wait on a poorer piece. Named for what
            // it is, or the census blames a policy that never touched it.
            EquipmentIndex behind;
            int shortfall, worst;
            if (reason == "skillOrUsage"
                && ShoppingTrip.IsHeldBack(hero, slot, ceiling, out behind, out shortfall, out worst))
            {
                reason = "heldBackByArmourOrder";
            }

            Bump(blockedBy, reason);
            return true;
        }

        /// <summary>A dictionary as one readable log line.</summary>
        private static string Tally(string label, Dictionary<string, int> counts)
        {
            StringBuilder text = new StringBuilder(label);
            if (counts.Count == 0) return text.Append(" <none>").ToString();

            foreach (KeyValuePair<string, int> pair in counts)
            {
                text.Append(' ').Append(pair.Key).Append('=').Append(pair.Value);
            }
            return text.ToString();
        }

        private static void ReportNaval()
        {
            Dictionary<string, List<int>> marinerByCulture = new Dictionary<string, List<int>>();
            Dictionary<string, int> lordsByCulture = new Dictionary<string, int>();
            Dictionary<string, int> shipOwnersByCulture = new Dictionary<string, int>();
            Dictionary<string, float> navalFactor = new Dictionary<string, float>();

            List<int> withShips = new List<int>();
            List<int> withoutShips = new List<int>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligible(hero)) continue;

                    CultureObject culture = hero.Culture;
                    if (culture == null || culture.StringId == null) continue;

                    string key = culture.StringId;
                    Bump(lordsByCulture, key);
                    if (!navalFactor.ContainsKey(key)) navalFactor[key] = culture.NavalFactor;

                    SkillObject marinerSkill = FindSkill("Mariner");
                    int mariner = marinerSkill == null ? 0 : hero.GetSkillValue(marinerSkill);

                    List<int> list;
                    if (!marinerByCulture.TryGetValue(key, out list))
                    {
                        list = new List<int>();
                        marinerByCulture[key] = list;
                    }
                    list.Add(mariner);

                    int ships = 0;
                    if (hero.PartyBelongedTo != null && hero.PartyBelongedTo.Ships != null)
                    {
                        ships = hero.PartyBelongedTo.Ships.Count;
                    }

                    if (ships > 0)
                    {
                        Bump(shipOwnersByCulture, key);
                        withShips.Add(mariner);
                    }
                    else
                    {
                        withoutShips.Add(mariner);
                    }
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }

            // The question that decides the design: does commanding a ship go
            // with knowing how to sail one? If it does, ships are to seamanship
            // what a lance is to Polearm, and the naval skills should follow
            // them rather than focus alone.
            ModLog.Info("NAVAL withShips " + Percentiles(withShips));
            ModLog.Info("NAVAL withoutShips " + Percentiles(withoutShips));

            foreach (KeyValuePair<string, int> pair in lordsByCulture)
            {
                float factor;
                navalFactor.TryGetValue(pair.Key, out factor);

                int owners;
                shipOwnersByCulture.TryGetValue(pair.Key, out owners);

                List<int> mariners;
                marinerByCulture.TryGetValue(pair.Key, out mariners);

                ModLog.Info("NAVAL " + pair.Key
                            + " navalFactor=" + (int)(factor * 100) + "%"
                            + " lords=" + pair.Value
                            + " withShips=" + owners
                            + " | mariner " + (mariners == null ? "n=0" : Percentiles(mariners)));
            }
        }

        /// <summary>
        /// A skill by its string id, or null when this installation has no such
        /// skill. The War Sails skills are not members of DefaultSkills -- the
        /// build fails outright if you name them there -- so they can only be
        /// reached by walking the registry, which also makes the report work
        /// unchanged for anyone without the DLC.
        /// </summary>
        private static SkillObject FindSkill(string stringId)
        {
            foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
            {
                if (skill != null && skill.StringId == stringId) return skill;
            }
            return null;
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

            ReportMercenaryCompanies();

            for (int i = 0; i < broken.Count; i++)
            {
                Hero hero = broken[i];
                // Says why he is bare, because the two causes want opposite
                // responses and the census cannot be read without knowing
                // which it found. A hero the come-of-age bug left half-dressed
                // is a defect this mod exists to repair. A hero sitting in
                // somebody's dungeon with nothing on was robbed, most likely
                // yesterday, and TryRepair is skipping him on purpose until he
                // is released -- re-equipping a prisoner would undo the capture
                // within a day of it happening. The first live census reported
                // two of the second kind as though they were the first.
                ModLog.Info("BROKEN hero=" + hero.Name
                            + " age=" + (int)hero.Age
                            + " clan=" + (hero.Clan != null ? hero.Clan.Name.ToString() : "<none>")
                            + " culture=" + CultureIdOf(hero)
                            + (hero.IsPrisoner ? " cause=prisoner(stripped; repaired on release)"
                                               : " cause=defect"));
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
                    + " cultureMountsLords=" + resolved.CultureMountsLords
                    + " bowViable=" + resolved.Availability.BowViable
                    + " xbowViable=" + resolved.Availability.CrossbowViable
                    + " plannedWeapons=" + resolved.PlannedWeaponCount
                    + " wouldGrant=" + resolved.WouldGrantCount
                    + " needsGrant=" + GrantService.NeedsGrant(hero)
                    + " role=" + resolved.Role
                    + " formation=" + (hero.CharacterObject != null
                                            ? hero.CharacterObject.DefaultFormationClass.ToString() : "<none>")
                    + " charId=" + (hero.CharacterObject != null ? hero.CharacterObject.StringId : "<none>")
                    + " heroId=" + hero.StringId
                    + " talent=" + (int)(HeroTalent.For(hero) * 100)
                    + " | skills=" + RenderSkills(HeroAdapter.ReadSkills(hero))
                    + " | current=" + resolved.CurrentWeapons
                    + " | target=" + resolved.TargetWeapons
                    + " | placed=" + resolved.PlacedWeapons
                    + " | ifStripped=" + IfStripped(hero, resolved);

                ModLog.Info(header);
                echo.AppendLine(header);

                string growth = "DRY   growth " + SkillGrowthService.DescribeWeaponOrder(hero);
                ModLog.Info(growth);
                echo.AppendLine(growth);

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
                                                         Settings.DominanceMargin,
                                                         resolved.CultureMountsLords,
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
