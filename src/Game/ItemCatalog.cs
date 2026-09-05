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
        /// The filters every catalogue lookup shares: not a quest or crafted
        /// item, within the tier ceiling, and either the hero's culture or
        /// unassigned. One policy, so armour and weapons cannot drift apart.
        /// </summary>
        private static bool PassesCommonFilters(ItemObject item, CultureObject culture, int maxTier)
        {
            if (item == null) return false;
            if (item.NotMerchandise) return false;
            if (item.IsCraftedByPlayer) return false;
            // TierCeiling speaks 1-based tiers (1..6); ItemObject.Tier is the 0-based
            // ItemTiers enum (Tier1 = 0 .. Tier6 = 5). Convert rather than letting the
            // two vocabularies meet raw.
            if ((int)item.Tier + 1 > maxTier) return false;
            if (item.Culture != null && culture != null && item.Culture.StringId != culture.StringId) return false;
            return true;
        }

        private static bool IsEligible(ItemObject item, WeaponCategory category, CultureObject culture,
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
            if (!ItemClassifier.Supports(item, category)) return false;
            if (!ItemClassifier.MeetsDifficulty(item, skills)) return false;
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
                if (!PassesCommonFilters(item, culture, maxTier)) continue;
                if (!ItemClassifier.MeetsDifficulty(item, skills)) continue;

                int tier = (int)item.Tier;
                if (tier > bestTier) { bestTier = tier; best = item; }
            }

            return best;
        }

        /// <summary>
        /// The best harness compatible with a specific mount: within the
        /// tier ceiling, culture-appropriate, and matching the mount's
        /// family (a harness modelled for a horse cannot dress a camel).
        /// Confirmed by reflecting the installed TaleWorlds.Core.dll: both
        /// ItemObject.ArmorComponent.FamilyType and
        /// ItemObject.HorseComponent.Monster.FamilyType exist exactly as
        /// named (both System.Int32) -- see the fix report. Like armour,
        /// a harness has no difficulty gate, so only the common filters and
        /// the family match apply. Returns null for a null mount or when
        /// nothing qualifies; callers must tolerate that.
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
