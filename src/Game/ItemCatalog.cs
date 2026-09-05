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
            if (category == WeaponCategory.None) return null;

            ItemObject best = null;
            int bestTier = -1;

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (!IsEligible(item, category, culture, maxTier, skills, hero, mounted)) continue;

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
            if ((int)item.Tier > maxTier) return false;
            if (item.Culture != null && culture != null && item.Culture.StringId != culture.StringId) return false;
            return true;
        }

        private static bool IsEligible(ItemObject item, WeaponCategory category, CultureObject culture,
                                       int maxTier, SkillProfile skills, Hero hero, bool mounted)
        {
            if (!PassesCommonFilters(item, culture, maxTier)) return false;
            if (ItemClassifier.Classify(item) != category) return false;
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
