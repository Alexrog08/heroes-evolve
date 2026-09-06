using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class SkillGrowthTests
    {
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
            Check.Equal(0, SkillGrowth.XpStep(200, 200, 1f), "an arrived skill gains nothing");
            Check.Equal(0, SkillGrowth.XpStep(250, 200, 1f), "a skill past its target gains nothing");
            Check.Equal(0, SkillGrowth.XpStep(0, 0, 1f), "no target, no growth");

            // A wide gap moves faster than a narrow one.
            int wide = SkillGrowth.XpStep(0, 200, 1f);
            int narrow = SkillGrowth.XpStep(190, 200, 1f);
            Check.True(wide > narrow, "the further behind, the faster the catch-up");
            Check.True(narrow > 0, "the last few points still land");

            // Talent changes the rate, not just the destination.
            Check.True(SkillGrowth.XpStep(0, 200, Talent.Maximum) > SkillGrowth.XpStep(0, 200, Talent.Minimum),
                       "a talented hero closes the same gap faster");

            // No single step is allowed to be dramatic.
            Check.True(SkillGrowth.XpStep(0, 330, Talent.Maximum) <= (int)SkillGrowth.MaximumStep,
                       "one cycle never grants more than the cap");
        }
    }
}
