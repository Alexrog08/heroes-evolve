using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    /// <summary>
    /// CategoryRules.SameFamily: a skill governs a family of weapons, not one
    /// exact WeaponClass. Before this, ItemCatalog.IsEligible matched
    /// ItemClassifier.Classify(item) != category exactly, so a hero whose
    /// culture had, say, only axes under the tier ceiling for the category
    /// the planner wanted (OneHandedSword) got an empty slot instead of the
    /// axe that was actually available -- and no hero could ever receive an
    /// axe, a mace, or a two-handed polearm at all, since CategoryForSkill
    /// never plans those as the wanted category in the first place.
    /// </summary>
    public static class WeaponCategoryTests
    {
        public static void RunAll()
        {
            // One-handed melee family: OneHandedSword, OneHandedAxe, OneHandedMace.
            // Every pair within the family matches, in both directions --
            // SameFamily must not favor whichever argument happens to be
            // the "planned" one.
            Check.True(CategoryRules.SameFamily(WeaponCategory.OneHandedSword, WeaponCategory.OneHandedSword), "one-handed sword matches itself");
            Check.True(CategoryRules.SameFamily(WeaponCategory.OneHandedSword, WeaponCategory.OneHandedAxe), "one-handed sword accepts an axe");
            Check.True(CategoryRules.SameFamily(WeaponCategory.OneHandedAxe, WeaponCategory.OneHandedSword), "...and the reverse direction agrees");
            Check.True(CategoryRules.SameFamily(WeaponCategory.OneHandedSword, WeaponCategory.OneHandedMace), "one-handed sword accepts a one-handed mace");
            Check.True(CategoryRules.SameFamily(WeaponCategory.OneHandedMace, WeaponCategory.OneHandedSword), "...and the reverse direction agrees");
            Check.True(CategoryRules.SameFamily(WeaponCategory.OneHandedAxe, WeaponCategory.OneHandedMace), "axe accepts a one-handed mace");
            Check.True(CategoryRules.SameFamily(WeaponCategory.OneHandedMace, WeaponCategory.OneHandedAxe), "...and the reverse direction agrees");

            // One-handed melee family rejects everything outside it.
            Check.False(CategoryRules.SameFamily(WeaponCategory.OneHandedSword, WeaponCategory.TwoHandedSword), "one-handed sword rejects a two-hander");
            Check.False(CategoryRules.SameFamily(WeaponCategory.OneHandedSword, WeaponCategory.Spear), "one-handed sword rejects a spear");
            Check.False(CategoryRules.SameFamily(WeaponCategory.OneHandedMace, WeaponCategory.TwoHandedAxe), "a one-handed mace rejects a two-handed axe");

            // The split earns its keep here. While both maces shared one value,
            // a two-handed mace read as one-handed and could be sold into a
            // sidearm slot beside a shield -- the same hole the bastard swords
            // opened, entering by a door the enum could not close.
            Check.False(CategoryRules.SameFamily(WeaponCategory.OneHandedSword, WeaponCategory.TwoHandedMace), "a one-handed slot rejects a two-handed mace");
            Check.False(CategoryRules.SameFamily(WeaponCategory.OneHandedMace, WeaponCategory.TwoHandedMace), "and so does a one-handed mace");

            // Two-handed melee family: TwoHandedSword, TwoHandedAxe, TwoHandedMace.
            Check.True(CategoryRules.SameFamily(WeaponCategory.TwoHandedSword, WeaponCategory.TwoHandedSword), "two-handed sword matches itself");
            Check.True(CategoryRules.SameFamily(WeaponCategory.TwoHandedSword, WeaponCategory.TwoHandedAxe), "two-handed sword accepts a two-handed axe");
            Check.True(CategoryRules.SameFamily(WeaponCategory.TwoHandedAxe, WeaponCategory.TwoHandedSword), "...and the reverse direction agrees");
            Check.True(CategoryRules.SameFamily(WeaponCategory.TwoHandedSword, WeaponCategory.TwoHandedMace), "two-handed sword accepts a two-handed mace");
            Check.True(CategoryRules.SameFamily(WeaponCategory.TwoHandedMace, WeaponCategory.TwoHandedAxe), "...and a two-handed mace accepts an axe");

            // Two-handed melee family rejects everything outside it,
            // including the one-handed family it otherwise resembles.
            Check.False(CategoryRules.SameFamily(WeaponCategory.TwoHandedSword, WeaponCategory.OneHandedSword), "two-handed sword rejects a one-hander");
            Check.False(CategoryRules.SameFamily(WeaponCategory.TwoHandedAxe, WeaponCategory.OneHandedMace), "two-handed axe rejects a one-handed mace");
            Check.False(CategoryRules.SameFamily(WeaponCategory.TwoHandedSword, WeaponCategory.Polearm), "two-handed sword rejects a polearm");

            // Polearm family: Spear (one-handed polearm), Polearm (two-handed
            // / low-grip polearm) -- see ItemClassifier.Classify, both
            // WeaponClass.OneHandedPolearm and TwoHandedPolearm/LowGripPolearm
            // feed this pair.
            Check.True(CategoryRules.SameFamily(WeaponCategory.Spear, WeaponCategory.Spear), "spear matches itself");
            Check.True(CategoryRules.SameFamily(WeaponCategory.Spear, WeaponCategory.Polearm), "spear accepts a two-handed polearm");
            Check.True(CategoryRules.SameFamily(WeaponCategory.Polearm, WeaponCategory.Spear), "...and the reverse direction agrees");

            // Polearm family rejects everything outside it.
            Check.False(CategoryRules.SameFamily(WeaponCategory.Spear, WeaponCategory.OneHandedSword), "spear rejects a one-handed sword");
            Check.False(CategoryRules.SameFamily(WeaponCategory.Polearm, WeaponCategory.TwoHandedSword), "polearm rejects a two-handed sword");
            Check.False(CategoryRules.SameFamily(WeaponCategory.Spear, WeaponCategory.Throwing), "spear rejects throwing weapons");

            // Bow, Crossbow, Throwing, Shield, Arrows, Bolts each stand
            // alone: they match only themselves, including against their
            // most plausible-looking neighbor.
            Check.True(CategoryRules.SameFamily(WeaponCategory.Bow, WeaponCategory.Bow), "bow matches itself");
            Check.False(CategoryRules.SameFamily(WeaponCategory.Bow, WeaponCategory.Crossbow), "bow rejects crossbow");
            Check.False(CategoryRules.SameFamily(WeaponCategory.Crossbow, WeaponCategory.Bow), "...and the reverse direction agrees");

            Check.True(CategoryRules.SameFamily(WeaponCategory.Crossbow, WeaponCategory.Crossbow), "crossbow matches itself");

            Check.True(CategoryRules.SameFamily(WeaponCategory.Throwing, WeaponCategory.Throwing), "throwing matches itself");
            Check.False(CategoryRules.SameFamily(WeaponCategory.Throwing, WeaponCategory.Bow), "throwing rejects bow");

            Check.True(CategoryRules.SameFamily(WeaponCategory.Shield, WeaponCategory.Shield), "shield matches itself");
            Check.False(CategoryRules.SameFamily(WeaponCategory.Shield, WeaponCategory.OneHandedSword), "shield rejects a one-handed sword");

            // Arrows/Bolts must not be treated as a family: arrows cannot
            // feed a crossbow (see the enum doc comment on WeaponCategory).
            Check.True(CategoryRules.SameFamily(WeaponCategory.Arrows, WeaponCategory.Arrows), "arrows match themselves");
            Check.True(CategoryRules.SameFamily(WeaponCategory.Bolts, WeaponCategory.Bolts), "bolts match themselves");
            Check.False(CategoryRules.SameFamily(WeaponCategory.Arrows, WeaponCategory.Bolts), "arrows do not match bolts");
            Check.False(CategoryRules.SameFamily(WeaponCategory.Bolts, WeaponCategory.Arrows), "...and the reverse direction agrees");

            // Anything else (None, Other) compares by plain equality.
            Check.True(CategoryRules.SameFamily(WeaponCategory.None, WeaponCategory.None), "none matches none");
            Check.True(CategoryRules.SameFamily(WeaponCategory.Other, WeaponCategory.Other), "other matches other");
            Check.False(CategoryRules.SameFamily(WeaponCategory.None, WeaponCategory.Other), "none does not match other");
            Check.False(CategoryRules.SameFamily(WeaponCategory.Other, WeaponCategory.OneHandedSword), "other does not match a real category");
        }
    }
}
