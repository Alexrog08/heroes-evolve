namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// What kind of thing occupies a weapon slot. Ammunition is split by kind
    /// because arrows cannot feed a crossbow.
    /// </summary>
    public enum WeaponCategory
    {
        None = 0,
        OneHandedSword,
        TwoHandedSword,
        OneHandedAxe,
        TwoHandedAxe,
        Mace,
        Spear,
        Polearm,
        Bow,
        Crossbow,
        Throwing,
        Shield,
        Arrows,
        Bolts,
        Other
    }

    /// <summary>Slot arithmetic and category predicates. Pure.</summary>
    public static class CategoryRules
    {
        /// <summary>
        /// How many of the four weapon slots a category consumes when planned.
        /// Bows and crossbows cost two because they need their ammunition alongside.
        /// </summary>
        public static int SlotCost(WeaponCategory category)
        {
            if (category == WeaponCategory.Bow || category == WeaponCategory.Crossbow) return 2;
            if (category == WeaponCategory.None) return 0;
            return 1;
        }

        /// <summary>True for weapons that need a separate ammunition slot.</summary>
        public static bool IsRanged(WeaponCategory category)
        {
            return category == WeaponCategory.Bow || category == WeaponCategory.Crossbow;
        }

        public static bool IsAmmo(WeaponCategory category)
        {
            return category == WeaponCategory.Arrows || category == WeaponCategory.Bolts;
        }

        public static bool IsMelee(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.OneHandedSword:
                case WeaponCategory.TwoHandedSword:
                case WeaponCategory.OneHandedAxe:
                case WeaponCategory.TwoHandedAxe:
                case WeaponCategory.Mace:
                case WeaponCategory.Spear:
                case WeaponCategory.Polearm:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>True for weapons that cannot be used together with a shield.</summary>
        public static bool IsTwoHanded(WeaponCategory category)
        {
            return category == WeaponCategory.TwoHandedSword
                || category == WeaponCategory.TwoHandedAxe;
        }

        /// <summary>The ammunition a ranged weapon consumes, or None.</summary>
        public static WeaponCategory AmmoFor(WeaponCategory ranged)
        {
            if (ranged == WeaponCategory.Bow) return WeaponCategory.Arrows;
            if (ranged == WeaponCategory.Crossbow) return WeaponCategory.Bolts;
            return WeaponCategory.None;
        }
    }
}
