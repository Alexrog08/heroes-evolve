using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
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
        /// The filters every catalogue lookup shares: not a quest or crafted
        /// item, within the tier ceiling, and either the hero's culture or
        /// unassigned. One policy, so armour and weapons cannot drift apart --
        /// and public, so the market shares it too. A lord who may not be given
        /// a Khuzait lamellar may not buy one either; letting the shop keep its
        /// own copy of this rule is how the two would quietly diverge.
        /// </summary>
        public static bool PassesCommonFilters(ItemObject item, CultureObject culture, int maxTier)
        {
            if (item == null) return false;
            if (item.NotMerchandise) return false;
            if (item.IsCraftedByPlayer) return false;

            // A unique item belongs to whoever TaleWorlds gave it to. Caladog's
            // gilded armour and horned helm are the whole of how that character
            // reads on a battlefield, and a mod that hands them to a passing
            // Vlandian because a shop had one has destroyed something it cannot
            // put back.
            if (item.IsUniqueItem) return false;
            // TierCeiling speaks 1-based tiers (1..6); ItemObject.Tier is the 0-based
            // ItemTiers enum (Tier1 = 0 .. Tier6 = 5). Convert rather than letting the
            // two vocabularies meet raw.
            if ((int)item.Tier + 1 > maxTier) return false;
            if (item.Culture != null && culture != null && item.Culture.StringId != culture.StringId) return false;
            return true;
        }

        /// <summary>
        /// Whether one item can fill one weapon slot for one hero. Public for
        /// the same reason as PassesCommonFilters: the market asks the identical
        /// question of a town's stock.
        /// </summary>
        public static bool IsEligible(ItemObject item, WeaponCategory category, CultureObject culture,
                                      int maxTier, SkillProfile skills, Hero hero, bool mounted)
        {
            if (!PassesCommonFilters(item, culture, maxTier)) return false;
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
