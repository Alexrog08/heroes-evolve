using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class SkillGrowthTests
    {
        public static void RunAll()
        {
            // The curve is the campaign's own, measured on lords born in play.
            Check.Equal(115, SkillGrowth.AgeNorm(18), "a young lord's norm");
            Check.Equal(115, SkillGrowth.AgeNorm(24), "still young at 24");
            Check.Equal(122, SkillGrowth.AgeNorm(25), "the norm steps up at 25");
            Check.Equal(126, SkillGrowth.AgeNorm(40), "mid career");
            Check.Equal(129, SkillGrowth.AgeNorm(50), "late career");
            Check.Equal(134, SkillGrowth.AgeNorm(60), "veteran");
            Check.Equal(134, SkillGrowth.AgeNorm(90), "the curve does not keep climbing");

            // It only ever rises with age -- a lord must never be told to shrink.
            int previous = 0;
            bool monotonic = true;
            for (int age = 18; age <= 90; age++)
            {
                int norm = SkillGrowth.AgeNorm(age);
                if (norm < previous) monotonic = false;
                previous = norm;
            }
            Check.True(monotonic, "the age curve never falls");

            // Talent scales the destination, and the gifted lord this system can
            // produce stays below a character played for thirty years (Bow 288).
            int gifted = SkillGrowth.PrimaryTarget(60, Talent.Maximum);
            Check.True(gifted < 288, "even the most gifted lord stays under a played founder");
            Check.True(gifted > 230, "...but above what the campaign already grows unaided");

            int poor = SkillGrowth.PrimaryTarget(60, Talent.Minimum);
            Check.True(poor < SkillGrowth.AgeNorm(60), "a slow learner falls short of the norm");

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
