using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class FocusGrowthTests
    {
        public static void RunAll()
        {
            // No focus, no target. Deliberate rather than a gap: 72% of lords
            // have never put a point into Smithing and they should stay at zero.
            Check.Equal(0, FocusGrowth.TargetFor(40f, 1.27f, 0, 250), "an uninvested skill has no target");
            Check.Equal(0, FocusGrowth.TargetFor(40f, 1.27f, -1, 250), "nor does a negative one");

            // Three focus is the median lord, so it earns the plain
            // age-and-talent target -- the same figure a weapon skill would get.
            int typical = FocusGrowth.TargetFor(40f, 1.27f, (int)FocusGrowth.TypicalFocus, 250);
            int combat = SkillGrowth.PrimaryTarget(40f, 1.27f);
            Check.True(typical >= combat - 2 && typical <= combat + 2,
                       "median focus earns what a primary weapon earns");

            // More focus aims higher, less aims lower.
            Check.True(FocusGrowth.TargetFor(40f, 1.27f, 5, 250) > typical, "heavy investment aims higher");
            Check.True(FocusGrowth.TargetFor(40f, 1.27f, 1, 250) < typical, "a token point aims lower");

            // But not without limit. A hero who poured everything into one skill
            // must not chase a number the game would never show, and nor should
            // focus carry him past the most TaleWorlds ever wrote anyone in it.
            int hoarded = FocusGrowth.TargetFor(60f, Talent.Maximum, 100, 250);
            Check.True(hoarded <= (int)(SkillGrowth.PeakNorm * Talent.Maximum
                                        * FocusGrowth.MaximumFocusFactor) + 1,
                       "the focus multiplier is capped");
            Check.True(hoarded > 0, "...but still produces a target");

            // His own aptitude is never capped by another man's sheet: at this
            // talent his years alone aim him above the envelope, and he keeps it.
            int alone = SkillGrowth.PrimaryTarget(60f, Talent.Maximum);
            Check.Equal(alone, hoarded, "a prodigy keeps what his own talent gives him");
            Check.True(alone > 250, "which here is above the envelope");

            // The ordinary case is the one the envelope is for: five focus on a
            // middling talent aims a third past the peak, and stops at what the
            // best steward TaleWorlds wrote holds.
            Check.Equal(240, FocusGrowth.TargetFor(60f, 1.6f, 5, 240),
                        "heavy focus stops at the highest stewardship ever written");
            Check.True(FocusGrowth.TargetFor(60f, 1.6f, 5, SkillGrowth.GameSkillMaximum) > 240,
                       "and it was aiming well past it");
            Check.Equal(SkillGrowth.PrimaryTarget(60f, 1.6f),
                        FocusGrowth.TargetFor(60f, 1.6f, 5, FocusGrowth.NoCeiling),
                        "a skill nobody was ever written aims no further than his own talent");

            // Age matters here as it does everywhere: the peak belongs to old
            // men, in the ledger as much as on the field.
            Check.True(FocusGrowth.TargetFor(60f, 1.27f, 3, 250) > FocusGrowth.TargetFor(25f, 1.27f, 3, 250),
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
