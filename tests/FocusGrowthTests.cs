using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class FocusGrowthTests
    {
        public static void RunAll()
        {
            // No focus, no target. Deliberate rather than a gap: 72% of lords
            // have never put a point into Smithing and they should stay at zero.
            Check.Equal(0, FocusGrowth.TargetFor(40f, 1.27f, 0), "an uninvested skill has no target");
            Check.Equal(0, FocusGrowth.TargetFor(40f, 1.27f, -1), "nor does a negative one");

            // Three focus is the median lord, so it earns the plain
            // age-and-talent target -- the same figure a weapon skill would get.
            int typical = FocusGrowth.TargetFor(40f, 1.27f, (int)FocusGrowth.TypicalFocus);
            int combat = SkillGrowth.PrimaryTarget(40f, 1.27f);
            Check.True(typical >= combat - 2 && typical <= combat + 2,
                       "median focus earns what a primary weapon earns");

            // More focus aims higher, less aims lower.
            Check.True(FocusGrowth.TargetFor(40f, 1.27f, 5) > typical, "heavy investment aims higher");
            Check.True(FocusGrowth.TargetFor(40f, 1.27f, 1) < typical, "a token point aims lower");

            // But not without limit: a hero who poured everything into one skill
            // must not chase a number the game would never show.
            int hoarded = FocusGrowth.TargetFor(60f, Talent.Maximum, 100);
            Check.True(hoarded <= (int)(SkillGrowth.PeakNorm * Talent.Maximum
                                        * FocusGrowth.MaximumFocusFactor) + 1,
                       "the focus multiplier is capped");
            Check.True(hoarded > 0, "...but still produces a target");

            // Age matters here as it does everywhere: the peak belongs to old
            // men, in the ledger as much as on the field.
            Check.True(FocusGrowth.TargetFor(60f, 1.27f, 3) > FocusGrowth.TargetFor(25f, 1.27f, 3),
                       "a veteran administrator outranks a young one");

            // And the domains are genuinely separate, so a lord can be gifted
            // with a lance and indifferent with a ledger.
            string id = "lord_4_1";
            Check.True(Talent.For(id, Talent.Combat) != Talent.For(id, Talent.Civil),
                       "combat and civil aptitude differ");
            Check.True(Talent.For(id, Talent.Naval) != Talent.For(id, Talent.Civil),
                       "naval and civil aptitude differ");
        }
    }
}
