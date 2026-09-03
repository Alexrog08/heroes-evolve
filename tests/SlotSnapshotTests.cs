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
        }
    }
}
