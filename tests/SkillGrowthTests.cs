using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class SkillGrowthTests
    {
        public static void RunAll()
        {
            // Maturity: a hero starts part-formed and peaks in old age.
            Check.True(SkillGrowth.Maturity(18f) == SkillGrowth.StartMaturity, "a youth is part formed");
            Check.True(SkillGrowth.Maturity(10f) == SkillGrowth.StartMaturity, "younger than 18 clamps");
            Check.True(SkillGrowth.Maturity(60f) == 1f, "fully developed at 60");
            Check.True(SkillGrowth.Maturity(90f) == 1f, "and no further");
                        Check.True(SkillGrowth.Maturity(40f) > SkillGrowth.Maturity(25f), "maturity rises with age");

            // Fractional, not annual. This is the whole reason growth used to
            // come in bursts: a target that stands still for a year lets a hero
            // arrive and then wait for his birthday.
            Check.True(SkillGrowth.Maturity(40.5f) > SkillGrowth.Maturity(40f),
                       "the target climbs between birthdays too");

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

            // Sustained rather than bursty: a hero should settle short of his
            // target and keep climbing, not sprint to it and stop. Ten points
            // behind, an average lord gains about a point a year less than his
            // target rises, which is what keeps him chasing.
            Check.True(SkillGrowth.PointsStep(160, 170, 1f) * SkillGrowth.CyclesPerYear < 4f,
                       "a lord near his target advances gently, not in a rush");
            Check.True(SkillGrowth.PointsStep(0, 330, Talent.Maximum)
                       > SkillGrowth.PointsStep(0, 330, Talent.Minimum),
                       "talent still separates them at the cap");

            // Sustained growth is the requirement, so it is simulated rather
            // than asserted at a point. A lord is followed from thirty-five to
            // sixty with his age advancing weekly, and three things must hold:
            // he climbs every single year, he never overtakes his target, and he
            // stays within reach of it. The failure this replaces measured a
            // static gap, which the system no longer produces -- the target
            // moves, so the hero is always chasing.
            float skill = 120f;
            float careerAge = 35f;
            float lastYear = skill;
            bool climbedEveryYear = true;
            bool everOvertook = false;

            for (int week = 0; week < (int)SkillGrowth.CyclesPerYear * 25; week++)
            {
                int target = SkillGrowth.PrimaryTarget(careerAge, 1.27f);
                if (skill > target + 1) everOvertook = true;

                skill += SkillGrowth.PointsStep((int)skill, target, 1.27f);
                careerAge += 1f / SkillGrowth.CyclesPerYear;

                if (week > 0 && week % (int)SkillGrowth.CyclesPerYear == 0)
                {
                    // Compared as a float: the first year gains only six tenths
                    // of a point, which an integer comparison would read as no
                    // progress at all.
                    if (skill <= lastYear) climbedEveryYear = false;
                    lastYear = skill;
                }
            }

            Check.True(climbedEveryYear, "a lord gains ground every year of his career");
            Check.True(!everOvertook, "and never overtakes what his years entitle him to");

            int finalTarget = SkillGrowth.PrimaryTarget(60f, 1.27f);
            Check.True(skill > finalTarget - 25, "he ends within reach of his ceiling");
            Check.True(skill < finalTarget, "but short of it -- there is always further to go");

            // Talent still separates them over a career.
            Check.True(SkillGrowth.PointsStep(120, 170, Talent.Maximum)
                       > SkillGrowth.PointsStep(120, 170, Talent.Minimum),
                       "and the gifted get there first");
        }
    }
}
