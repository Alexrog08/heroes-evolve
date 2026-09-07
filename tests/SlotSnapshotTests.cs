using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class SlotSnapshotTests
    {
        public static void RunAll()
        {
            Check.Equal(1, CategoryRules.SlotCost(WeaponCategory.OneHandedSword), "melee costs one slot");
            Check.Equal(1, CategoryRules.SlotCost(WeaponCategory.Throwing), "throwing costs one slot");
            Check.Equal(2, CategoryRules.SlotCost(WeaponCategory.Bow), "bow costs two slots");
            Check.Equal(2, CategoryRules.SlotCost(WeaponCategory.Crossbow), "crossbow costs two slots");
            Check.Equal(1, CategoryRules.SlotCost(WeaponCategory.Shield), "shield costs one slot");

            Check.True(CategoryRules.IsRanged(WeaponCategory.Bow), "bow is ranged");
            Check.False(CategoryRules.IsRanged(WeaponCategory.Throwing), "throwing is not ranged for slot purposes");
            Check.True(CategoryRules.IsAmmo(WeaponCategory.Arrows), "arrows are ammo");
            Check.True(CategoryRules.IsTwoHanded(WeaponCategory.TwoHandedAxe), "two-handed axe is two-handed");
            Check.False(CategoryRules.IsTwoHanded(WeaponCategory.Spear), "spear is not two-handed");

            Check.Equal((int)WeaponCategory.Arrows, (int)CategoryRules.AmmoFor(WeaponCategory.Bow), "bow takes arrows");
            Check.Equal((int)WeaponCategory.Bolts, (int)CategoryRules.AmmoFor(WeaponCategory.Crossbow), "crossbow takes bolts");

            // Shield + spear + one-hander, one free slot.
            SlotSnapshot s = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.Spear, WeaponCategory.Shield, WeaponCategory.OneHandedSword, WeaponCategory.None },
                false, false, true, true, true, false, false);

            Check.Equal(1, s.EmptyWeaponSlots, "one empty weapon slot");
            Check.True(s.Contains(WeaponCategory.Shield), "contains shield");
            Check.False(s.Contains(WeaponCategory.Bow), "does not contain bow");
            Check.False(s.HasMount, "no mount");
            Check.False(s.HasTwoHandedEquipped, "no two-handed weapon equipped");
            Check.Equal((int)WeaponCategory.None, (int)s.WeaponAt(3), "fourth slot empty");

            SlotSnapshot naked = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.None, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                false, false, false, false, false, false, false);
            Check.Equal(4, naked.EmptyWeaponSlots, "naked hero has four empty slots");

            SlotSnapshot greatsword = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.TwoHandedSword, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                true, true, false, false, false, false, false);
            Check.True(greatsword.HasTwoHandedEquipped, "two-handed sword detected");
            Check.True(greatsword.HasMount, "mount detected");

            // IsMelee had no coverage at all: walk both sides of the boundary.
            Check.True(CategoryRules.IsMelee(WeaponCategory.OneHandedSword), "one-handed sword is melee");
            Check.True(CategoryRules.IsMelee(WeaponCategory.TwoHandedAxe), "two-handed axe is melee");
            Check.True(CategoryRules.IsMelee(WeaponCategory.OneHandedMace), "a one-handed mace is melee");
            Check.True(CategoryRules.IsMelee(WeaponCategory.Spear), "spear is melee");
            Check.True(CategoryRules.IsMelee(WeaponCategory.Polearm), "polearm is melee");
            Check.False(CategoryRules.IsMelee(WeaponCategory.Bow), "bow is not melee");
            Check.False(CategoryRules.IsMelee(WeaponCategory.Shield), "shield is not melee");
            Check.False(CategoryRules.IsMelee(WeaponCategory.Throwing), "throwing is not melee");
            Check.False(CategoryRules.IsMelee(WeaponCategory.None), "none is not melee");

            // The Arrows/Bolts split matters: arrows cannot feed a crossbow.
            Check.True(CategoryRules.IsAmmo(WeaponCategory.Bolts), "bolts are ammo");
            Check.False(CategoryRules.IsAmmo(WeaponCategory.Bow), "a bow is not ammo");
            Check.Equal((int)WeaponCategory.None, (int)CategoryRules.AmmoFor(WeaponCategory.OneHandedSword), "a sword takes no ammo");
            Check.Equal(0, CategoryRules.SlotCost(WeaponCategory.None), "an empty slot costs nothing");

            // All four slots full is the boundary the planner uses to bail out early.
            SlotSnapshot loaded = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.Bow, WeaponCategory.Arrows, WeaponCategory.Arrows, WeaponCategory.OneHandedSword },
                true, true, true, true, true, true, true);
            Check.Equal(0, loaded.EmptyWeaponSlots, "a full loadout has no empty slots");
            Check.Equal(0, loaded.EmptySlotIndices().Length, "and names no empty indices");

            // EmptySlotIndices must agree with EmptyWeaponSlots and name the right slots.
            int[] gaps = s.EmptySlotIndices();
            Check.Equal(s.EmptyWeaponSlots, gaps.Length, "empty count agrees with empty indices");
            Check.Equal(3, gaps[0], "the one free slot is the fourth");
            int[] nakedGaps = naked.EmptySlotIndices();
            Check.Equal(4, nakedGaps.Length, "a naked hero has four free slots");
            Check.Equal(0, nakedGaps[0], "first free slot is index zero");
            Check.Equal(3, nakedGaps[3], "last free slot is index three");

            // Alternating values so a flag transposed with a sibling cannot hide.
            SlotSnapshot flags = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.None, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                true, false, true, false, true, false, true);
            Check.True(flags.HasMount, "mount flag round-trips");
            Check.False(flags.HasHarness, "harness flag round-trips");
            Check.True(flags.HasHelmet, "helmet flag round-trips");
            Check.False(flags.HasBody, "body flag round-trips");
            Check.True(flags.HasLegs, "legs flag round-trips");
            Check.False(flags.HasGloves, "gloves flag round-trips");
            Check.True(flags.HasCape, "cape flag round-trips");
        }
    }
}
