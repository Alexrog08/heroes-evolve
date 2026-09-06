namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// How far a lord's combat skills should have come by a given age, and how
    /// much to push them when they have fallen behind.
    ///
    /// Bannerlord barely develops its AI lords. Measured over a sixty-one year
    /// campaign, the median campaign-born lord goes from 115 at twenty to 129 at
    /// fifty: fourteen points for thirty years of war. Secondary weapons never
    /// move at all -- the median lord's fourth-best weapon skill sits at nine
    /// percent of his best, and his fifth and sixth at zero -- so a lord who
    /// carries a spear has One Handed at zero for life. Three lords in the save
    /// reached their mid-forties carrying swords with One Handed at zero, and
    /// two of those had nothing above zero anywhere.
    ///
    /// That is the gap this closes: a hero who has fought for decades should not
    /// be weaker than the troops he leads.
    ///
    /// Nothing here reduces a skill. Growth is catch-up toward a target and
    /// stops on arrival, so a lord the game developed properly is never touched.
    /// </summary>
    public static class SkillGrowth
    {
        /// <summary>
        /// The median best combat skill of a campaign-born lord at each age,
        /// measured rather than chosen: 18-24 -> 115, 25-34 -> 122, 35-44 -> 126,
        /// 45-54 -> 129, 55+ -> 134.
        ///
        /// The measurement deliberately excludes lords present when the campaign
        /// began. Those were authored as veterans on day one and their spread
        /// (p90 of 231 against 153 for the campaign-born) describes TaleWorlds'
        /// starting roster, not what a campaign grows.
        /// </summary>
        public static int AgeNorm(int age)
        {
            if (age < 25) return 115;
            if (age < 35) return 122;
            if (age < 45) return 126;
            if (age < 55) return 129;
            return 134;
        }

        /// <summary>
        /// Where this hero's best combat skill should be sitting, given his age
        /// and how quickly he learns.
        /// </summary>
        public static int PrimaryTarget(int age, float talent)
        {
            return Talent.TargetFor(AgeNorm(age), talent);
        }

        /// <summary>
        /// The share of the primary target a hero's Nth-best skill should reach.
        ///
        /// Taken from the shape healthy lords actually have: the median lord's
        /// ranked weapon skills fall at 100%, 79%, 50%, 9%, 0%, 0% of his best.
        /// Seeding a single skill would produce a hero unlike any in the game --
        /// three meaningful weapons is what a lord looks like.
        ///
        /// Rank is zero-based: rank 0 is the primary.
        /// </summary>
        public static int TargetForRank(int primaryTarget, int rank)
        {
            if (primaryTarget <= 0) return 0;

            int percent;
            switch (rank)
            {
                case 0: percent = 100; break;
                case 1: percent = 79; break;
                case 2: percent = 50; break;
                case 3: percent = 9; break;
                default: return 0;
            }

            return (primaryTarget * percent) / 100;
        }

        /// <summary>
        /// Experience to grant a skill this cycle. Zero once the skill has
        /// arrived, so a properly developed lord is left alone entirely.
        ///
        /// The step is proportional to the distance remaining, which makes a
        /// badly broken hero catch up quickly at first and then ease in, rather
        /// than crawling for decades or arriving in one jump. The floor stops
        /// the tail from taking forever; the cap stops a hero from gaining a
        /// visible chunk of a skill in a single day.
        /// </summary>
        public static int XpStep(int current, int target, float talent)
        {
            if (target <= 0) return 0;
            if (current >= target) return 0;

            int gap = target - current;

            // XpPerPoint is a rough conversion: Bannerlord's own curve costs far
            // more per point at high levels, so this understates the tail on
            // purpose. Undershooting means a slow arrival; overshooting would
            // mean a lord vaulting past his target between two ticks.
            float step = gap * CatchUpFraction * talent * XpPerPoint;

            if (step < MinimumStep) step = MinimumStep;
            if (step > MaximumStep) step = MaximumStep;

            return (int)step;
        }

        /// <summary>Fraction of the remaining gap aimed at per cycle.</summary>
        public const float CatchUpFraction = 0.02f;

        /// <summary>Rough experience per skill point, for turning a gap into XP.</summary>
        public const float XpPerPoint = 12f;

        /// <summary>Never grant less than this, or the last few points never land.</summary>
        public const float MinimumStep = 5f;

        /// <summary>Never grant more than this in one go.</summary>
        public const float MaximumStep = 400f;
    }
}
