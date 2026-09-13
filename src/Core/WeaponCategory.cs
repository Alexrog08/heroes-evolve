namespace HeroesEvolve.Core
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
        OneHandedMace,
        TwoHandedMace,
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
                case WeaponCategory.OneHandedMace:
                case WeaponCategory.TwoHandedMace:
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

        /// <summary>
        /// True for the classes the game's two "axes and maces" perks reward.
        ///
        /// Of 164 weapon perks, exactly two look at which weapon of a category a
        /// hero carries rather than the category itself: Swift Strike gives
        /// damage with one-handed axes and maces, On The Edge the same for two
        /// handed. Everything else says "one handed weapons" or "polearms" and
        /// cannot tell a sword from an axe.
        ///
        /// Both handednesses of both weapons, since the caller already knows
        /// which handedness it is asking about.
        /// </summary>
        public static bool IsAxeOrMace(WeaponCategory category)
        {
            return category == WeaponCategory.OneHandedAxe
                || category == WeaponCategory.TwoHandedAxe
                || category == WeaponCategory.OneHandedMace
                || category == WeaponCategory.TwoHandedMace;
        }

        /// <summary>
        /// The two-handed category a one-handed request must not be answered
        /// with, or None.
        ///
        /// A bastard sword is a fine answer to a one-handed request, but not to
        /// a hero already carrying a two-handed sword: the one-handed slot
        /// exists to leave the shield hand free, and a second two-hander adds
        /// nothing. Axes and maces have their own bastard variants and behave
        /// the same way.
        /// </summary>
        public static WeaponCategory TwoHandedPartner(WeaponCategory wanted)
        {
            if (wanted == WeaponCategory.OneHandedSword) return WeaponCategory.TwoHandedSword;
            if (wanted == WeaponCategory.OneHandedAxe) return WeaponCategory.TwoHandedAxe;
            if (wanted == WeaponCategory.OneHandedMace) return WeaponCategory.TwoHandedMace;
            return WeaponCategory.None;
        }

        /// <summary>The ammunition a ranged weapon consumes, or None.</summary>
        public static WeaponCategory AmmoFor(WeaponCategory ranged)
        {
            if (ranged == WeaponCategory.Bow) return WeaponCategory.Arrows;
            if (ranged == WeaponCategory.Crossbow) return WeaponCategory.Bolts;
            return WeaponCategory.None;
        }

        /// <summary>
        /// True when a skill that governs `planned` would also let a hero
        /// use `candidate` -- a family of weapons, not one exact WeaponClass.
        /// A skill trains a whole class of arms in Bannerlord (One-Handed
        /// covers swords, axes and maces alike), so the catalogue must not
        /// reject an axe or a mace just because the planner's shorthand for
        /// the category happens to spell out "OneHandedSword".
        ///
        /// Families:
        ///   one-handed melee: OneHandedSword, OneHandedAxe, OneHandedMace
        ///   two-handed melee: TwoHandedSword, TwoHandedAxe, TwoHandedMace
        ///   polearm:          Spear, Polearm
        /// Bow, Crossbow, Throwing, Shield, Arrows and Bolts each stand
        /// alone -- notably Arrows and Bolts are NOT a family: arrows cannot
        /// feed a crossbow (see the WeaponCategory doc comment above).
        /// Everything else (None, Other) falls through to plain equality.
        ///
        /// Membership is checked once per family and the same membership
        /// function is used for both arguments, so the result is
        /// symmetric: SameFamily(a, b) == SameFamily(b, a) always.
        /// </summary>
        public static bool SameFamily(WeaponCategory planned, WeaponCategory candidate)
        {
            if (IsOneHandedMeleeFamily(planned)) return IsOneHandedMeleeFamily(candidate);
            if (IsTwoHandedMeleeFamily(planned)) return IsTwoHandedMeleeFamily(candidate);
            if (IsPolearmFamily(planned)) return IsPolearmFamily(candidate);
            return planned == candidate;
        }

        /// <summary>
        /// True when answering a request for <paramref name="wanted"/> with an
        /// item whose own identity is <paramref name="primary"/> would cross
        /// between thrown weapons and everything else, in either direction.
        ///
        /// A family says which weapons are interchangeable; this says which
        /// never are, whatever else an item can do. Thrown weapons mostly carry
        /// a melee mode as well -- a javelin can be jabbed with, a throwing axe
        /// swung -- and some spears carry a throw, so a test that only asks
        /// "can it be wielded this way" lets a stack of javelins stand in for a
        /// spear. That is a different soldier, not a better spear.
        /// </summary>
        public static bool CrossesThrowingLine(WeaponCategory wanted, WeaponCategory primary)
        {
            return (wanted == WeaponCategory.Throwing) != (primary == WeaponCategory.Throwing);
        }

        private static bool IsOneHandedMeleeFamily(WeaponCategory category)
        {
            return category == WeaponCategory.OneHandedSword
                || category == WeaponCategory.OneHandedAxe
                || category == WeaponCategory.OneHandedMace;
        }

        private static bool IsTwoHandedMeleeFamily(WeaponCategory category)
        {
            return category == WeaponCategory.TwoHandedSword
                || category == WeaponCategory.TwoHandedAxe
                || category == WeaponCategory.TwoHandedMace;
        }

        private static bool IsPolearmFamily(WeaponCategory category)
        {
            return category == WeaponCategory.Spear
                || category == WeaponCategory.Polearm;
        }
    }
}
