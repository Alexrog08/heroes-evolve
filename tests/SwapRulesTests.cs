using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class SwapRulesTests
    {
        public static void RunAll()
        {
            Check.Equal((int)SwapKind.SameClass,
                        (int)SwapRules.Classify(WeaponCategory.Spear, WeaponCategory.Spear, true),
                        "a better spear for a spear is the same class");
            Check.Equal((int)SwapKind.SameClass,
                        (int)SwapRules.Classify(WeaponCategory.Throwing, WeaponCategory.Throwing, true),
                        "throwing axes for javelins are the same class");

            Check.Equal((int)SwapKind.OtherClassSameFamily,
                        (int)SwapRules.Classify(WeaponCategory.OneHandedSword, WeaponCategory.OneHandedAxe, true),
                        "an axe for a sword stays in the family");
            Check.Equal((int)SwapKind.OtherClassSameFamily,
                        (int)SwapRules.Classify(WeaponCategory.Polearm, WeaponCategory.Spear, true),
                        "a lance for a pike stays in the family");

            // A bastard sword files itself as one-handed. Replacing a two-hander
            // with one is by design only because it can be held two-handed.
            Check.Equal((int)SwapKind.ThroughSecondaryUsage,
                        (int)SwapRules.Classify(WeaponCategory.TwoHandedSword, WeaponCategory.OneHandedSword, true),
                        "a bastard sword for a two-hander is reached through its second grip");
            Check.Equal((int)SwapKind.OtherFamily,
                        (int)SwapRules.Classify(WeaponCategory.TwoHandedSword, WeaponCategory.OneHandedSword, false),
                        "a one-hander that cannot be held two-handed is another family");
            Check.Equal((int)SwapKind.OtherFamily,
                        (int)SwapRules.Classify(WeaponCategory.Bow, WeaponCategory.Crossbow, false),
                        "a crossbow for a bow is another family");

            // The bug this exists to catch: decided before the usage walk, so a
            // javelin's melee mode cannot talk its way past it.
            Check.Equal((int)SwapKind.ThrownLineCrossed,
                        (int)SwapRules.Classify(WeaponCategory.Spear, WeaponCategory.Throwing, true),
                        "javelins for a spear cross the line even though they can be jabbed with");
            Check.Equal((int)SwapKind.ThrownLineCrossed,
                        (int)SwapRules.Classify(WeaponCategory.Throwing, WeaponCategory.Spear, true),
                        "a spear for javelins crosses it the other way");

            Check.False(SwapRules.IsNeverByDesign(SwapKind.SameClass), "the same class is by design");
            Check.False(SwapRules.IsNeverByDesign(SwapKind.OtherClassSameFamily), "another class of the family is by design");
            Check.False(SwapRules.IsNeverByDesign(SwapKind.ThroughSecondaryUsage), "a bastard sword is by design");
            Check.True(SwapRules.IsNeverByDesign(SwapKind.OtherFamily), "another family is never by design");
            Check.True(SwapRules.IsNeverByDesign(SwapKind.ThrownLineCrossed), "crossing the thrown line is never by design");
        }
    }
}
