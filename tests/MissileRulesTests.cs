using System.Collections.Generic;
using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    /// <summary>
    /// Ammunition built for something other than a battle.
    /// </summary>
    public static class MissileRulesTests
    {
        public static void RunAll()
        {
            // The case that wrote the rule: the NavalDLC's Whisper Arrows fly
            // at 5 where every other arrow and bolt in the game flies at 10.
            Check.True(MissileRules.IsSlow(5, 10), "a whisper arrow is too slow for a battle");
            Check.False(MissileRules.IsSlow(10, 10), "an ordinary arrow is not");

            // Just above the line is kept. A heavy war arrow that is a little
            // slower than the rest is a real choice, not a stealth item.
            Check.False(MissileRules.IsSlow(6, 10), "a little slower is still a battle arrow");
            Check.False(MissileRules.IsSlow(9, 10), "and so is nearly ordinary");

            // Faster than ordinary is never refused.
            Check.False(MissileRules.IsSlow(15, 10), "faster is never refused");

            // An item that states no speed says nothing either way, and neither
            // does a catalogue with no ammunition in it.
            Check.False(MissileRules.IsSlow(0, 10), "no stated speed is not slow");
            Check.False(MissileRules.IsSlow(5, 0), "no ordinary speed to judge against");

            // The scale does not matter, which is the point of measuring
            // against what is installed: a ballistics overhaul that doubles
            // every speed still leaves the stealth arrow at half.
            Check.True(MissileRules.IsSlow(10, 20), "half is half at any scale");
            Check.False(MissileRules.IsSlow(20, 20), "and ordinary is ordinary");

            // The ordinary speed is the median, so the one special arrow cannot
            // pull down the line it is measured against. Vanilla plus the
            // NavalDLC: every ammunition item at 10 but one at 5.
            List<int> vanilla = new List<int>();
            for (int i = 0; i < 26; i++) vanilla.Add(10);
            vanilla.Add(5);
            Check.Equal(10, MissileRules.Ordinary(vanilla), "the median ignores the one odd arrow");

            // Unstated speeds are left out rather than counted as zero.
            Check.Equal(10, MissileRules.Ordinary(new int[] { 0, 0, 10, 10, 10 }),
                        "items with no stated speed do not drag the line down");
            Check.Equal(0, MissileRules.Ordinary(new int[0]), "nothing installed, nothing ordinary");
            Check.Equal(0, MissileRules.Ordinary(null), "and nothing asked about");
        }
    }
}
