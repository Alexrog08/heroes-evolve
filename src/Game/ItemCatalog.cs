using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>Finds concrete items for a planned category.</summary>
    public static class ItemCatalog
    {
        /// <summary>
        /// The best item of a category the hero may have: within the tier ceiling,
        /// usable given their skills, culture-appropriate, and mount-compatible.
        /// Returns null when nothing qualifies; callers must tolerate that.
        /// </summary>
        public static ItemObject FindBest(WeaponCategory category, CultureObject culture,
                                          int maxTier, SkillProfile skills, Hero hero, bool mounted)
        {
            return FindBest(category, culture, maxTier, skills, hero, mounted, WeaponCategory.None);
        }

        /// <summary>
        /// As above, but refusing items that also serve <paramref name="avoidAlsoServing"/>.
        ///
        /// A bastard sword answers a one-handed request legitimately -- the game
        /// itself lists OneHandedBastardSword as its primary usage. But a hero
        /// who already carries a two-handed sword gains nothing from filling his
        /// one-handed slot with a second weapon that is also a two-handed sword;
        /// the point of that slot is the shield hand. Pass the two-handed
        /// category already present and such items are excluded.
        /// </summary>
        public static ItemObject FindBest(WeaponCategory category, CultureObject culture,
                                          int maxTier, SkillProfile skills, Hero hero, bool mounted,
                                          WeaponCategory avoidAlsoServing)
        {
            if (category == WeaponCategory.None) return null;

            ItemObject best = null;
            int bestTier = -1;

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (!IsEligible(item, category, culture, maxTier, skills, hero, mounted)) continue;
                if (avoidAlsoServing != WeaponCategory.None
                    && ItemClassifier.AlsoServesTwoHanded(item, avoidAlsoServing)) continue;

                int tier = (int)item.Tier;
                if (tier > bestTier)
                {
                    bestTier = tier;
                    best = item;
                }
            }

            return best;
        }

        /// <summary>
        /// An item for the free repair: one of those acceptable in the grant
        /// band, not the best in the catalogue.
        ///
        /// FindBest exists to answer "what is the finest thing this lord could
        /// own", which is the right question for a purchase cap and the wrong
        /// one for clothing a naked noble. Granting the best left the purchase
        /// engine nothing to sell, and issued every lord of a culture and tier
        /// the identical sword because the scan is deterministic.
        ///
        /// Falls back to FindBest when the band is empty. A culture with nothing
        /// at tier 2 or 3 should still dress its lords.
        /// </summary>
        public static ItemObject FindForGrant(WeaponCategory category, CultureObject culture,
                                              int ceiling, SkillProfile skills, Hero hero, bool mounted,
                                              WeaponCategory avoidAlsoServing, string slotKey)
        {
            if (category == WeaponCategory.None) return null;

            int lowest, highest;
            GrantTier.Band(ceiling, out lowest, out highest);

            List<ItemObject> candidates = new List<ItemObject>();

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (!IsEligible(item, category, culture, highest, skills, hero, mounted)) continue;
                if ((int)item.Tier + 1 < lowest) continue;
                if (avoidAlsoServing != WeaponCategory.None
                    && ItemClassifier.AlsoServesTwoHanded(item, avoidAlsoServing)) continue;

                candidates.Add(item);
            }

            if (candidates.Count == 0)
            {
                return FindBest(category, culture, ceiling, skills, hero, mounted, avoidAlsoServing);
            }

            return candidates[GrantTier.Choose(HeroIdOf(hero), slotKey, candidates.Count)];
        }

        /// <summary>Armour for the free repair, chosen the same way.</summary>
        public static ItemObject FindArmorForGrant(ItemObject.ItemTypeEnum wanted, CultureObject culture,
                                                   int ceiling, Hero hero, string slotKey)
        {
            int lowest, highest;
            GrantTier.Band(ceiling, out lowest, out highest);

            List<ItemObject> candidates = new List<ItemObject>();

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (item == null || item.ItemType != wanted) continue;
                if (!PassesCommonFilters(item, culture, highest)) continue;
                if ((int)item.Tier + 1 < lowest) continue;

                candidates.Add(item);
            }

            if (candidates.Count == 0) return FindBestArmor(wanted, culture, ceiling);

            return candidates[GrantTier.Choose(HeroIdOf(hero), slotKey, candidates.Count)];
        }

        /// <summary>
        /// A mount for the free repair, chosen the same way.
        ///
        /// This matters more than the weapon: a top-tier warhorse is among the
        /// costliest things in the game, so granting the best undoes T0 through
        /// the one slot it did not cover. The difficulty gate stays -- every war
        /// mount asks Riding 10, band or no band.
        /// </summary>
        public static ItemObject FindMountForGrant(CultureObject culture, int ceiling,
                                                   SkillProfile skills, Hero hero, string slotKey)
        {
            int lowest, highest;
            GrantTier.Band(ceiling, out lowest, out highest);

            List<ItemObject> candidates = new List<ItemObject>();

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (item == null || item.ItemType != ItemObject.ItemTypeEnum.Horse) continue;
                if (!IsWarMount(item)) continue;
                if (!PassesCommonFilters(item, culture, highest)) continue;
                if ((int)item.Tier + 1 < lowest) continue;
                if (!ItemClassifier.MeetsDifficulty(item, skills)) continue;

                candidates.Add(item);
            }

            if (candidates.Count == 0) return FindBestMount(culture, ceiling, skills);

            return candidates[GrantTier.Choose(HeroIdOf(hero), slotKey, candidates.Count)];
        }

        /// <summary>A harness for the free repair, matching the granted mount.</summary>
        public static ItemObject FindHarnessForGrant(ItemObject mount, CultureObject culture,
                                                     int ceiling, Hero hero, string slotKey)
        {
            if (mount == null || !mount.HasHorseComponent || mount.HorseComponent.Monster == null) return null;
            int mountFamily = mount.HorseComponent.Monster.FamilyType;

            int lowest, highest;
            GrantTier.Band(ceiling, out lowest, out highest);

            List<ItemObject> candidates = new List<ItemObject>();

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (item == null || item.ItemType != ItemObject.ItemTypeEnum.HorseHarness) continue;
                if (!item.HasArmorComponent || item.ArmorComponent.FamilyType != mountFamily) continue;
                if (!PassesCommonFilters(item, culture, highest)) continue;
                if ((int)item.Tier + 1 < lowest) continue;

                candidates.Add(item);
            }

            if (candidates.Count == 0) return FindBestHarness(mount, culture, ceiling);

            return candidates[GrantTier.Choose(HeroIdOf(hero), slotKey, candidates.Count)];
        }

        private static string HeroIdOf(Hero hero)
        {
            return hero == null ? null : hero.StringId;
        }

        /// <summary>
        /// Gear the market must never take off a hero, whatever the shelves
        /// hold.
        ///
        /// The test is not "is it special" but "could we have sold it to him".
        ///
        /// The loss is not only the hero's. Selling gear back is the one way
        /// this mod ever puts an item on a shelf, and an item the game marks
        /// unsellable is one vanilla never lets reach a shop at all. Let a lord
        /// trade in his noble sword and it is in Praven's market the same
        /// afternoon, for anyone to buy -- so the exclusivity of noble arms,
        /// armour and horses would be dismantled one purchase at a time, for the
        /// whole campaign, by a mod nobody installed for that.
        ///
        /// It is also irreversible for the hero: no merchant stocks a
        /// replacement, so what he sells he can never buy again. That covers
        /// what TaleWorlds hung on a named character -- Caladog's gilded armour
        /// and horned helm are the whole of how he reads on a battlefield -- and
        /// the sword a player forged for a companion, by the same argument from
        /// the other direction.
        ///
        /// IsUniqueItem is checked as well and is, at least in this install,
        /// dead weight: a census found it false for every item in the game. It
        /// stays because it costs nothing and is the flag that ought to mean
        /// this, not because it currently catches anything.
        /// </summary>
        public static bool IsIrreplaceable(ItemObject item)
        {
            if (item == null) return false;
            return item.NotMerchandise || item.IsCraftedByPlayer || item.IsUniqueItem;
        }

        /// <summary>
        /// The best whole tier of this kind of item on sale anywhere, in any
        /// culture's colours. Zero when nothing of the kind is sold.
        ///
        /// What a slot can reach is the lower of this and the hero's ceiling:
        /// his merit may say tier 6, but if nobody sells leg armour above tier
        /// 4 he is not behind on boots, he is wearing the best boots there are.
        /// The shopping order and the census's headroom both ask it, so it is
        /// answered in one place -- it used to be built inside the census
        /// alone, where the engine could not reach it.
        ///
        /// Any culture, because the market sells across cultures and only
        /// prefers a man's own (see MarketRules.CultureShare): what he
        /// could be sold is what anyone sells.
        ///
        /// Scanned once per campaign load and kept. The catalogue does not
        /// change while a campaign runs, and the shopping order asks this for
        /// every armour slot on every pass of every trip.
        /// </summary>
        public static int BestBuyableTier(ItemObject.ItemTypeEnum type)
        {
            if (_bestBuyable == null) _bestBuyable = ScanBestBuyable();

            int tier;
            return _bestBuyable != null && _bestBuyable.TryGetValue(type, out tier) ? tier : 0;
        }

        /// <summary>
        /// Forgets the scan: on every campaign load, and whenever the exclusion
        /// list may have changed, since the scan leaves refused items out.
        /// </summary>
        public static void ResetSession()
        {
            _bestBuyable = null;
        }

        private static Dictionary<ItemObject.ItemTypeEnum, int> _bestBuyable;

        private static Dictionary<ItemObject.ItemTypeEnum, int> ScanBestBuyable()
        {
            // No item list yet is not a catalogue with nothing in it. Left
            // unscanned, so the first question asked in a campaign scans.
            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance != null
                ? MBObjectManager.Instance.GetObjectTypeList<ItemObject>() : null;
            if (all == null || all.Count == 0) return null;

            Dictionary<ItemObject.ItemTypeEnum, int> best = new Dictionary<ItemObject.ItemTypeEnum, int>();
            for (int i = 0; i < all.Count; i++)
            {
                // Buyable, not merely existing: gear no merchant could ever
                // sell sets no standard a lord could be behind on. It is why
                // horses top out at 5 while lords are seen riding 6.
                ItemObject item = all[i];
                if (item == null || IsIrreplaceable(item)) continue;

                // Nor anything the market refuses outright. The exclusion list
                // exists for mod outliers, and an outlier is exactly what sets a
                // best: exclude the only tier-6 cape, leave the cap at 6, and a
                // lord in a tier-3 cape waits for ever on a piece nobody will
                // sell him, with the rest of his armour held back behind it.
                if (IsRefused(item)) continue;

                int tier = (int)item.Tier + 1;
                if (tier < 1) continue;

                int current;
                if (best.TryGetValue(item.ItemType, out current) && current >= tier) continue;
                best[item.ItemType] = tier;
            }

            return best;
        }

        /// <summary>
        /// The filters every catalogue lookup shares: not a quest or crafted
        /// item, within the tier ceiling, and either the hero's culture or
        /// unassigned. One policy, so armour and weapons cannot drift apart --
        /// and public, so the market shares it too. A lord who may not be given
        /// a Khuzait lamellar may not buy one either; letting the shop keep its
        /// own copy of this rule is how the two would quietly diverge.
        /// </summary>
        public static bool PassesCommonFilters(ItemObject item, CultureObject culture, int maxTier)
        {
            // Provenance, and only the free grant asks about it. Conjuring a
            // hero a crown out of the catalogue invents something the game never
            // put in circulation. Finding one on a shelf does not.
            if (item == null) return false;
            if (item.NotMerchandise) return false;
            if (item.IsCraftedByPlayer) return false;
            if (item.IsUniqueItem) return false;

            return PassesMarketFilters(item, culture, maxTier);
        }

        /// <summary>
        /// Tier, and nothing about where the item came from or whose colours
        /// it wears.
        ///
        /// Culture used to be refused here and is now a preference inside
        /// MarketRules instead -- see CultureShare for why the wall had to
        /// come down. The grant still refuses across cultures, and should: that
        /// is a lord's own people handing him his first kit, not a shelf.
        ///
        /// What is on a shelf is for sale, whatever it is. An item only reaches
        /// a town roster because somebody put it there, so once a gilded helm
        /// has been taken off its owner and sold on, buying it is an ordinary
        /// transaction -- for the lord it was stolen from, for a lord of another
        /// house, or for whoever walks into that town first.
        ///
        /// Refusing here would strand that gear in whatever inventory it landed
        /// in, which is the opposite of what taking it was for.
        /// </summary>
        public static bool PassesMarketFilters(ItemObject item, CultureObject culture, int maxTier)
        {
            if (item == null) return false;

            // Clothing is not kit, whoever is selling it. See KitTier: the rack
            // below tier 2 is where every dress in the game lives, and a lord
            // has no business in one whether he was handed it, bought it or
            // stripped it off a prisoner.
            if (IsArmorSlot(item.ItemType) && KitTier.IsClothing((int)item.Tier + 1)) return false;

            // TierCeiling speaks 1-based tiers (1..6); ItemObject.Tier is the 0-based
            // ItemTiers enum (Tier1 = 0 .. Tier6 = 5). Convert rather than letting the
            // two vocabularies meet raw.
            if ((int)item.Tier + 1 > maxTier) return false;
            if (IsRefused(item)) return false;
            return true;
        }

        /// <summary>
        /// Whether an item goes in one of the hero's armour slots. Mounts and
        /// harnesses are deliberately not here: the game scores a riding horse
        /// below tier 2 as readily as a tunic, and a poor horse is still a horse.
        /// </summary>
        public static bool IsArmorSlot(ItemObject.ItemTypeEnum type)
        {
            return type == ItemObject.ItemTypeEnum.HeadArmor
                || type == ItemObject.ItemTypeEnum.BodyArmor
                || type == ItemObject.ItemTypeEnum.LegArmor
                || type == ItemObject.ItemTypeEnum.HandArmor
                || type == ItemObject.ItemTypeEnum.Cape;
        }

        /// <summary>
        /// Whether a weapon is one the game itself keeps off the battlefield.
        ///
        /// Read off the game's own data rather than a list of names, because
        /// the data says it unambiguously. Every item across the shipped
        /// catalogue and the installed mods that carries an incendiary physics
        /// material -- siege pots, burning ballista bolts, mangonel pots, and
        /// the cheirosiphon's own ammunition -- is marked is_merchandise=false
        /// and cannot reach a shelf. Fourteen of them; every one off the market.
        ///
        /// Except one. Open Source Weaponry's "Calradic Fire Pots"
        /// (AR_naptha_pot) carries physics_material="burning_jar", 200 blunt
        /// damage in stacks of five, and IS merchandise. The same author marked
        /// eight of his own items unsellable, the cheirosiphon among them, so
        /// this is a choice rather than an oversight -- but it is a choice about
        /// what a player may buy, and this mod decides what five hundred AI
        /// lords will hunt for. Measured against the census: difficulty 120
        /// falls between the 75th and 90th percentile of lords' Throwing, so
        /// about a fifth of the map clears it, and the 36,842-denar price is a
        /// third of the median lord's per-purchase allowance. They would buy it,
        /// and in a previous campaign under another mod they did.
        ///
        /// Naming the material rather than the item is what makes this a rule.
        /// The next mod that adds a firebomb is covered without anyone editing
        /// anything.
        ///
        /// Every usage is checked, not only the primary: an item can be wielded
        /// more than one way and only one of them need be the ugly one.
        /// </summary>
        /// <summary>
        /// Says which excluded ids name no item in this installation.
        ///
        /// Written after watching the exclusion list fail in the quietest way
        /// it can. The setting is a free-text box matched by exact string, so a
        /// typo, a stale id from a mod since removed, or -- as actually
        /// happened -- the example id copied out of the settings.xml comment,
        /// all behave identically to an empty box: the mod goes on buying the
        /// thing and nothing anywhere says why. Every other setting in this mod
        /// is a switch or a bounded number and cannot be wrong in that way.
        ///
        /// Reported rather than corrected. An id this installation does not
        /// know may still be right -- the player may be about to enable the mod
        /// that defines it -- so the list is left exactly as entered.
        /// </summary>
        public static void ReportUnknownExclusions()
        {
            try
            {
                string[] ids = Settings.ExcludedIds();
                if (ids.Length == 0) return;

                // The screen can be opened from the main menu, where no item
                // list exists yet. Nothing to check against is not a complaint.
                MBReadOnlyList<ItemObject> all = MBObjectManager.Instance != null
                    ? MBObjectManager.Instance.GetObjectTypeList<ItemObject>() : null;
                if (all == null || all.Count == 0) return;

                for (int i = 0; i < ids.Length; i++)
                {
                    bool found = false;
                    for (int j = 0; j < all.Count; j++)
                    {
                        ItemObject item = all[j];
                        if (item == null) continue;
                        if (string.Equals(item.StringId, ids[i],
                                          System.StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }

                    ModLog.Info("EXCLUDED " + ids[i] + (found ? " matches an item" : " MATCHES NOTHING"));
                }
            }
            catch
            {
                // A diagnostic must never be the thing that takes a campaign down.
            }
        }

        public static bool IsIncendiary(ItemObject item)
        {
            if (item == null || !item.HasWeaponComponent) return false;

            for (int i = 0; i < item.Weapons.Count; i++)
            {
                string material = item.Weapons[i].PhysicsMaterial;
                if (string.IsNullOrEmpty(material)) continue;
                if (material.IndexOf("burning", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Everything this mod will not put in a lord's hands, whatever its
        /// tier and whatever he can afford.
        ///
        /// Two layers answering two different questions. IsIncendiary is a rule
        /// about the game's own data and needs no maintenance. Settings.
        /// IsExcluded is the player's own list, because this mod shops in a
        /// catalogue it does not control: any mod can add an outlier, and the
        /// honest tool for "not in MY campaign" is a line in settings.xml, not
        /// a judgement baked in here.
        ///
        /// Consulted from PassesMarketFilters, which PassesCommonFilters calls
        /// in turn, so a refused item cannot arrive through the back door of a
        /// free grant either.
        /// </summary>
        public static bool IsRefused(ItemObject item)
        {
            if (item == null) return true;
            if (IsIncendiary(item)) return true;
            return Settings.IsExcluded(item.StringId);
        }

        /// <summary>
        /// Whether this item is dressed in the hero's own colours.
        ///
        /// An item with no culture is not: it belongs to nobody in particular
        /// and suits everybody equally, which is exactly the neutral case the
        /// preference should leave alone.
        /// </summary>
        public static bool IsOwnCulture(ItemObject item, CultureObject culture)
        {
            if (item == null || item.Culture == null || culture == null) return false;
            return item.Culture.StringId == culture.StringId;
        }

        /// <summary>
        /// Whether an item is his own people's work, or nobody's.
        ///
        /// What the hard culture filters ask, where the preference asks
        /// IsOwnCulture. The difference is the unassigned pieces: they carry no
        /// colours to clash with anybody's, so a wall has no reason to stop
        /// them, while a preference has no reason to pay them a bonus. With no
        /// culture to favour at all -- a lord under "no preference" -- there is
        /// nothing to enforce and everything passes.
        /// </summary>
        public static bool IsOwnOrNeutral(ItemObject item, CultureObject culture)
        {
            if (item == null) return false;
            if (culture == null || item.Culture == null) return true;

            return item.Culture.StringId == culture.StringId;
        }

        /// <summary>
        /// Whether one item can fill one weapon slot for one hero. Public for
        /// the same reason as PassesCommonFilters: the market asks the identical
        /// question of a town's stock.
        /// </summary>
        public static bool IsEligible(ItemObject item, WeaponCategory category, CultureObject culture,
                                      int maxTier, SkillProfile skills, Hero hero, bool mounted)
        {
            return IsEligible(item, category, culture, maxTier, skills, hero, mounted, true);
        }

        /// <summary>
        /// As above, but the caller says whether provenance matters. The grant
        /// asks, because it conjures items out of the catalogue; the market does
        /// not, because it is reading a real shelf.
        /// </summary>
        public static bool IsEligible(ItemObject item, WeaponCategory category, CultureObject culture,
                                      int maxTier, SkillProfile skills, Hero hero, bool mounted,
                                      bool requireMerchandise)
        {
            bool passes = requireMerchandise
                ? PassesCommonFilters(item, culture, maxTier)
                : PassesMarketFilters(item, culture, maxTier);
            if (!passes) return false;
            // A skill governs a family of weapons, not one exact WeaponClass
            // (see CategoryRules.SameFamily): an exact match here meant no
            // hero could ever receive an axe, a mace or a two-handed
            // polearm, and a culture with no item of the precise class the
            // planner named got an empty slot even when a perfectly good
            // family member was in the catalogue.
            // Supports, not SameFamily(Classify(...)): an item can be wielded in
            // more than one way and Classify only reports the first. Matching on
            // the primary usage alone left the two-handed pool at 8 items out of
            // 3500, because every crafted bastard sword files itself as
            // one-handed (see ItemClassifier.AllUsages).
            // MeetsDifficulty first: it returns immediately for the many items
            // with no difficulty at all, while Supports allocates an iterator
            // and walks every usage. Both must pass, so the order is ours to
            // choose and the cheap test belongs in front.
            if (!ItemClassifier.MeetsDifficulty(item, skills, category)) return false;
            if (!ItemClassifier.Supports(item, category)) return false;
            if (mounted && !ItemClassifier.IsUsableMounted(item, hero)) return false;
            return true;
        }

        /// <summary>
        /// The best armour of a given slot type within the ceiling. Armour has
        /// no difficulty gate and no mounted restriction, so it needs only the
        /// common filters.
        /// </summary>
        public static ItemObject FindBestArmor(ItemObject.ItemTypeEnum wanted, CultureObject culture, int maxTier)
        {
            ItemObject best = null;
            int bestTier = -1;

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (item == null || item.ItemType != wanted) continue;
                if (!PassesCommonFilters(item, culture, maxTier)) continue;

                int tier = (int)item.Tier;
                if (tier > bestTier) { bestTier = tier; best = item; }
            }

            return best;
        }

        /// <summary>
        /// The best mount the hero may have: within the tier ceiling,
        /// culture-appropriate, and usable given their Riding skill. Mounts
        /// do carry a difficulty, gated on Riding -- ItemClassifier.MeetsDifficulty
        /// already special-cases ItemTypeEnum.Horse to route there instead of
        /// the per-category skill map, so it is reused as-is rather than
        /// duplicating that routing here. Returns null when nothing
        /// qualifies; callers must tolerate that (an empty catalogue for
        /// this hero's culture/tier/skill combination is a real outcome,
        /// not a bug).
        /// </summary>
        public static ItemObject FindBestMount(CultureObject culture, int maxTier, SkillProfile skills)
        {
            ItemObject best = null;
            int bestTier = -1;

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (item == null || item.ItemType != ItemObject.ItemTypeEnum.Horse) continue;
                if (!IsWarMount(item)) continue;
                if (!PassesCommonFilters(item, culture, maxTier)) continue;
                if (!ItemClassifier.MeetsDifficulty(item, skills)) continue;

                int tier = (int)item.Tier;
                if (tier > bestTier) { bestTier = tier; best = item; }
            }

            return best;
        }

        /// <summary>
        /// A mount fit to fight from, as opposed to a beast of burden.
        ///
        /// Mules, sumpter horses and pack camels are all ItemTypeEnum.Horse and
        /// all is_mountable="true", so the item type alone lets them through.
        /// What separates them is is_pack_animal, and the reason it matters is
        /// that every real war horse in the game requires Riding 10 or more
        /// while the pack animals require nothing at all: a hero with Riding 0
        /// fails the difficulty gate on every horse and qualifies for exactly
        /// the mules. That is not a hypothetical -- the heroes this mod repairs
        /// are the ones whose skills never generated, and two of them in a live
        /// campaign had 0 in all seven skills. Without this check they would
        /// have ridden to war on a mule with 3 charge damage.
        /// </summary>
        public static bool IsWarMount(ItemObject item)
        {
            if (!item.HasHorseComponent) return false;

            HorseComponent horse = item.HorseComponent;
            if (horse == null) return false;

            return horse.IsMount && !horse.IsPackAnimal;
        }

        /// <summary>
        /// The best harness compatible with a specific mount: within the tier
        /// ceiling, culture-appropriate, and matching the mount's family (a
        /// harness modelled for a horse cannot dress a camel). Like armour, a
        /// harness has no difficulty gate, so only the common filters and the
        /// family match apply. Returns null for a null mount or when nothing
        /// qualifies; callers must tolerate that.
        /// </summary>
        public static ItemObject FindBestHarness(ItemObject mount, CultureObject culture, int maxTier)
        {
            if (mount == null || !mount.HasHorseComponent || mount.HorseComponent.Monster == null) return null;
            int mountFamily = mount.HorseComponent.Monster.FamilyType;

            ItemObject best = null;
            int bestTier = -1;

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (item == null || item.ItemType != ItemObject.ItemTypeEnum.HorseHarness) continue;
                if (!item.HasArmorComponent || item.ArmorComponent.FamilyType != mountFamily) continue;
                if (!PassesCommonFilters(item, culture, maxTier)) continue;

                int tier = (int)item.Tier;
                if (tier > bestTier) { bestTier = tier; best = item; }
            }

            return best;
        }

        /// <summary>
        /// Whether a bow or crossbow this hero could use mounted exists at all.
        /// Feeds the planner so an unusable category falls through to the next
        /// skill instead of leaving an empty slot.
        /// </summary>
        public static MountedRangedAvailability RangedAvailability(Hero hero, CultureObject culture, int maxTier)
        {
            SkillProfile skills = HeroAdapter.ReadSkills(hero);
            bool bow = FindBest(WeaponCategory.Bow, culture, maxTier, skills, hero, true) != null;
            bool crossbow = FindBest(WeaponCategory.Crossbow, culture, maxTier, skills, hero, true) != null;
            return new MountedRangedAvailability(bow, crossbow);
        }
    }
}
