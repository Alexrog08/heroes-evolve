using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class SkillGrowthTests
    {
        /// <summary>Campaign years for a hero to close a gap of this size.</summary>
        private static int YearsToClose(int gap, float talent)
        {
            float current = 0f;
            int target = gap;

            for (int week = 0; week < (int)SkillGrowth.CyclesPerYear * 40; week++)
            {
                float step = SkillGrowth.PointsStep((int)current, target, talent);
                if (step <= 0f) return week / (int)SkillGrowth.CyclesPerYear;
                current += step;
            }
            return 40;
        }

        public static void RunAll()
        {
            // Maturity: a hero starts part-formed and peaks in old age.
            Check.True(SkillGrowth.Maturity(18) == SkillGrowth.StartMaturity, "a youth is part formed");
            Check.True(SkillGrowth.Maturity(10) == SkillGrowth.StartMaturity, "younger than 18 clamps");
            Check.True(SkillGrowth.Maturity(60) == 1f, "fully developed at 60");
            Check.True(SkillGrowth.Maturity(90) == 1f, "and no further");
            Check.True(SkillGrowth.Maturity(40) > SkillGrowth.Maturity(25), "maturity rises with age");

            int previousTarget = 0;
            bool monotonic = true;
            for (int age = 18; age <= 90; age++)
            {
                int target = SkillGrowth.PrimaryTarget(age, 1.3f);
                if (target < previousTarget) monotonic = false;
                previousTarget = target;
            }
            Check.True(monotonic, "a hero's target never falls with age");

            // The peak belongs to old men. This is the mistake the first model
            // made: it scaled a nearly flat age curve by talent, aiming a gifted
            // twenty-year-old at 230 on his first day.
            int giftedYouth = SkillGrowth.PrimaryTarget(20, Talent.Maximum);
            int giftedOld = SkillGrowth.PrimaryTarget(60, Talent.Maximum);
            Check.True(giftedYouth < 160, "a gifted youth is promising, not a veteran");
            Check.True(giftedOld > giftedYouth + 80, "his peak arrives decades later");

            // And that peak stays below a founder played for thirty years (288)
            // while sitting above what the campaign already grows unaided (230).
            Check.True(giftedOld < 288, "even the most gifted lord stays under a played founder");
            Check.True(giftedOld > 230, "...but above what the campaign already grows unaided");

            // The average lord ends meaningfully above today's stagnant 134.
            Check.True(SkillGrowth.PrimaryTarget(60, 1.275f) > 150, "the median lord stops stagnating");

            int poor = SkillGrowth.PrimaryTarget(60, Talent.Minimum);
            Check.True(poor < SkillGrowth.PeakNorm, "a slow learner falls short of the norm");

            // The shape comes from real lords: 100/79/50/9, then nothing.
            int primary = 200;
            Check.Equal(200, SkillGrowth.TargetForRank(primary, 0), "the primary is the target itself");
            Check.Equal(158, SkillGrowth.TargetForRank(primary, 1), "second skill at 79%");
            Check.Equal(100, SkillGrowth.TargetForRank(primary, 2), "third at 50%");
            Check.Equal(18, SkillGrowth.TargetForRank(primary, 3), "fourth barely registers");
            Check.Equal(0, SkillGrowth.TargetForRank(primary, 4), "there is no fifth");
            Check.Equal(0, SkillGrowth.TargetForRank(0, 0), "no target means no ranks");

            // Growth stops on arrival, so a properly developed lord is untouched.
            Check.True(SkillGrowth.PointsStep(200, 200, 1f) == 0f, "an arrived skill gains nothing");
            Check.True(SkillGrowth.PointsStep(250, 200, 1f) == 0f, "a skill past its target gains nothing");
            Check.True(SkillGrowth.PointsStep(0, 0, 1f) == 0f, "no target, no growth");

            // A wide gap moves faster than a narrow one.
            float wide = SkillGrowth.PointsStep(0, 200, 1f);
            float narrow = SkillGrowth.PointsStep(190, 200, 1f);
            Check.True(wide > narrow, "the further behind, the faster the catch-up");
            Check.True(narrow > 0f, "the last few points still land");

            // Talent changes the rate, not just the destination.
            Check.True(SkillGrowth.PointsStep(0, 200, Talent.Maximum)
                       > SkillGrowth.PointsStep(0, 200, Talent.Minimum),
                       "a talented hero closes the same gap faster");

            // No single cycle is allowed to be dramatic.
            Check.True(SkillGrowth.PointsStep(0, 330, Talent.Maximum)
                       <= SkillGrowth.MaximumBasePointsPerCycle * Talent.Maximum,
                       "one cycle never moves more than the cap");
            Check.True(SkillGrowth.PointsStep(0, 330, Talent.Maximum)
                       > SkillGrowth.PointsStep(0, 330, Talent.Minimum),
                       "talent still separates them at the cap");

            // The rate has to be big enough to matter. Under the first version
            // eight weekly passes over a live campaign moved the population by
            // nothing at all, so the timescale is asserted rather than assumed.
            //
            // The realistic case first: a lord who has simply aged past his
            // target carries a gap of ten or twenty points, and that should
            // close comfortably within a few years.
            // Bounds taken from tracing the model, not from a wish. A twenty
            // point gap closes in about five years for average talent, which
            // keeps pace with a target that itself climbs roughly two points a
            // year as the hero matures.
            Check.True(YearsToClose(20, 1f) <= 6, "an ordinary gap closes within a few years");
            Check.True(YearsToClose(10, 1f) <= 4, "a small one sooner");

            // And the pathological case, which in practice only a hero the mod
            // has not yet repaired can have -- seeding closes those at once.
            // The per-cycle cap deliberately slows this: nobody should watch a
            // skill bar climb.
            Check.True(YearsToClose(100, 1f) <= 15, "even a hopeless case is not hopeless forever");
            Check.True(YearsToClose(100, Talent.Maximum) < YearsToClose(100, Talent.Minimum),
                       "and the gifted get there first");
        }
    }
}
