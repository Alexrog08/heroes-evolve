namespace HeroesEvolve.Core
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
        /// The peak a lord of average talent reaches by the end of his life,
        /// with a weapon. One norm per field, because TaleWorlds did not write
        /// the three alike -- see CivilPeakNorm and NavalPeakNorm.
        ///
        /// Set from the lords TaleWorlds authored, not from the ones a campaign
        /// produces. The first version used 134, the median best combat skill of
        /// campaign-born lords past fifty-five -- but that is the degraded
        /// population this mod exists to repair, and aiming at it meant the map
        /// drifted down as the authored generation died out. A fresh campaign's
        /// own lords sit at 175 to 200, which is what the game considers a noble
        /// worth the name.
        ///
        /// The figure is his own ceiling over Talent.Level. The highest anyone
        /// was ever written with a weapon is Caladog's 300, so a hero dealt that
        /// much talent finishes exactly level with him, and a lord of median
        /// talent finishes around 203. Nothing needs to hold the top down: the
        /// scale is TaleWorlds' own, and the dice reach a little past it so a
        /// prodigy can pass him (see Talent.Maximum).
        /// </summary>
        public const int PeakNorm = 145;

        /// <summary>
        /// The same for everything that is neither a weapon nor a sail: 250 over
        /// Talent.Level.
        ///
        /// Lower than the combat norm because TaleWorlds wrote the fields on
        /// different scales. A sword reaches 300 on his sheets; the eleven civil
        /// skills all top out between 230 and 250, the highest being Pharon's 250
        /// in trade. Lending the ledger a norm measured on swords was worth a
        /// quarter of a field, and a census of a fresh campaign caught it: lords
        /// aimed at 302 in scouting, where the best scout he ever wrote holds
        /// 230. The scale was the error; a cap would only have hidden it.
        /// </summary>
        public const int CivilPeakNorm = 121;

        /// <summary>
        /// And at sea: Halthdar's 280 in Shipmaster over Talent.Level. The War
        /// Sails skills are written above the civil ones and below a sword.
        /// </summary>
        public const int NavalPeakNorm = 135;

        /// <summary>
        /// The norm one field is measured against. Combat for anything the
        /// caller cannot name, which is how the weapon passes ask for it.
        /// </summary>
        public static int PeakNormFor(string domain)
        {
            if (domain == Talent.Civil) return CivilPeakNorm;
            if (domain == Talent.Naval) return NavalPeakNorm;
            return PeakNorm;
        }

        /// <summary>
        /// The highest skill value Bannerlord shows. Nothing here should invent
        /// a number the game would never display.
        /// </summary>
        public const int GameSkillMaximum = 330;

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
        /// Talent multiplies afterwards, so a prodigy badly behind climbs at
        /// this times Talent.Maximum and a slow learner in the same hole at a
        /// quarter of that.
        ///
        /// And then the game multiplies again, which this comment used to
        /// ignore. Hero.AddSkillXp routes through HeroDeveloper, which scales
        /// the grant by GenericXpModel.GetXpMultiplier and by the hero's focus
        /// factor before any of it lands. So this bounds the request, not the
        /// delivery. Measured over a live campaign, weekly, across five hundred
        /// lords: delivered ran 1.5 to 2.0 times asked, and the ratio fell
        /// steadily as gaps closed.
        ///
        /// Left where it is, because the outcome is the one the design wanted
        /// and it took the measurement to know that. At these numbers the
        /// median lord gains between one and three points a year in a given
        /// skill -- something like a hundred and ten over a forty-two-year
        /// career, against a peak of a hundred and fifty times his talent. He
        /// climbs all his life and never quite arrives, which is exactly what
        /// CatchUpPerYear was tuned for. What was wrong was the arithmetic in
        /// this comment, not the number.
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
