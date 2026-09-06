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
        /// The peak a lord of average talent should reach by the end of his
        /// life.
        ///
        /// Set from the lords TaleWorlds authored, not from the ones a campaign
        /// produces. The first version used 134, the median best combat skill of
        /// campaign-born lords past fifty-five -- but that is the degraded
        /// population this mod exists to repair, and aiming at it meant the map
        /// drifted down as the authored generation died out. A fresh campaign's
        /// own lords sit at 175 to 200, which is what the game considers a noble
        /// worth the name.
        ///
        /// At 150 a lord of average talent finishes around 190, level with the
        /// roster TaleWorlds shipped, so a generation born in play replaces the
        /// one it buries rather than diminishing it.
        /// </summary>
        public const int PeakNorm = 150;

        /// <summary>Age at which a hero is considered fully developed.</summary>
        public const int MatureAge = 60;

        /// <summary>Age at which growth begins.</summary>
        public const int StartAge = 18;

        /// <summary>Share of his peak a hero has reached on coming of age.</summary>
        public const float StartMaturity = 0.55f;

        /// <summary>
        /// How much of his lifetime peak a hero has reached at a given age.
        ///
        /// This exists because the first version of this model was wrong in a
        /// way worth recording. It scaled a measured age curve by talent, but
        /// that curve runs from 115 at twenty to 134 at fifty-five -- nearly
        /// flat, because it describes exactly the stagnation being fixed. With
        /// talent multiplying at every age, a gifted twenty-year-old was aimed
        /// at 230 from his first day: a boy with a veteran's arm.
        ///
        /// Talent decides how far a hero can go; age decides how far along he
        /// is. The peak belongs to old men.
        /// </summary>
        public static float Maturity(float age)
        {
            if (age <= StartAge) return StartMaturity;
            if (age >= MatureAge) return 1f;

            float progress = (age - StartAge) / (MatureAge - StartAge);
            return StartMaturity + (1f - StartMaturity) * progress;
        }

        /// <summary>
        /// Where this hero's best combat skill should be sitting: as far along
        /// his own ceiling as his years have carried him.
        ///
        /// Age is taken as a fraction of a year, not whole years. With whole
        /// years the target stood still for twelve cycles and then jumped, so a
        /// hero sprinted to it, stopped dead, and waited for his next birthday
        /// to unlock the next stretch -- growth in steps rather than growth.
        /// Fractional age makes the target climb every week, which is what the
        /// hero is then chasing.
        /// </summary>
        public static int PrimaryTarget(float age, float talent)
        {
            int peak = Talent.TargetFor(PeakNorm, talent);
            return (int)(peak * Maturity(age));
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
        /// Skill points to move this cycle. Zero once the skill has arrived, so
        /// a properly developed lord is left alone entirely.
        ///
        /// Returned in points rather than experience on purpose. The first
        /// version returned experience through a flat guess of twelve per point,
        /// and eight weekly passes over a live campaign moved the population by
        /// nothing at all: Bannerlord's curve costs hundreds to thousands of
        /// experience per point at the levels lords actually sit at, so a
        /// fifty-point gap was being fed fifteen experience a week. Points are a
        /// unit this class can reason about; the conversion belongs where the
        /// game's own curve can be asked.
        ///
        /// The step is proportional to the distance remaining, so a badly broken
        /// hero catches up quickly and then eases in rather than crawling for
        /// decades or arriving in one jump.
        /// </summary>
        public static float PointsStep(int current, int target, float talent, float cyclesPerYear)
        {
            if (target <= 0) return 0f;
            if (current >= target) return 0f;
            if (cyclesPerYear <= 0f) cyclesPerYear = DefaultCyclesPerYear;

            int gap = target - current;

            // Clamped per YEAR and divided afterwards, not clamped per cycle.
            // Per-cycle limits smuggled the calendar back in through the side
            // door: FastMode runs four cycles a year against twelve, so a
            // ceiling of a third of a point per cycle allowed 1.4 points a year
            // there and 4.2 in stock. Reading DaysInYear was not enough on its
            // own -- every rate in this method has to be annual.
            float perYear = gap * CatchUpPerYear;

            // A floor as well as a ceiling. Closing a fixed share of what
            // remains is geometric, so the last few points shrink toward nothing
            // and never actually land -- a hero would sit forever at
            // ninety-something percent of his target. The floor makes the tail
            // finite.
            if (perYear < MinimumPointsPerYear) perYear = MinimumPointsPerYear;
            if (perYear > MaximumPointsPerYear) perYear = MaximumPointsPerYear;

            // Talent applies AFTER the clamp, not before. Clamping the final
            // figure let a prodigy and a dullard with the same large gap both
            // sit on the limit and advance identically, which erases the one
            // thing talent exists to express.
            return (perYear / cyclesPerYear) * talent;
        }

        /// <summary>
        /// Share of the remaining gap a hero of average talent closes in a year.
        ///
        /// Deliberately small. At a third, a lord caught his target within a few
        /// years and then sat pinned to it exactly, advancing only as fast as
        /// the target itself -- correct arithmetic, but it reads as a sprint
        /// followed by a lifetime of tracking a line. At an eighth he settles
        /// some ten points short and climbs steadily for the rest of his life,
        /// never arriving. A man should always have further to go.
        /// </summary>
        public const float CatchUpPerYear = 0.12f;

        /// <summary>
        /// Weekly cycles in a campaign year, for callers with no campaign to
        /// ask. Stock Bannerlord runs four seasons of three weeks, so twelve.
        ///
        /// A default rather than a fact, because the calendar is not fixed:
        /// CampaignTime builds DaysInYear out of WeeksInSeason, DaysInWeek and
        /// SeasonsInYear, all static fields a mod may change. FastMode shortens
        /// a season to a single week, which makes a year four weekly ticks
        /// instead of twelve -- so a rate written against the stock calendar
        /// runs at a third speed there. The real figure is read from the game
        /// and passed in.
        /// </summary>
        public const float DefaultCyclesPerYear = 12f;

        /// <summary>
        /// Ceiling on the pre-talent annual gain, so no hero visibly rockets
        /// when the gap is enormous. Seeding exists for that case and applies at
        /// once, deliberately.
        ///
        /// Talent multiplies afterwards, so the true ceiling is this times
        /// Talent.Maximum -- eight points a year for a prodigy who is badly
        /// behind, a quarter of that for a slow learner in the same hole.
        /// </summary>
        public const float MaximumPointsPerYear = 4.2f;

        /// <summary>
        /// Floor on the pre-talent annual gain. Small: with the target climbing
        /// every week a hero's gap no longer shrinks toward nothing, so this
        /// guards against stalling rather than driving the tail. Left in because
        /// a hero already at his ceiling -- past sixty, where maturity stops --
        /// would otherwise have no gap at all.
        /// </summary>
        public const float MinimumPointsPerYear = 0.24f;
    }
}
