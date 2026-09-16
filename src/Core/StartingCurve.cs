namespace HeroesEvolve.Core
{
    /// <summary>
    /// Where a lord's skills stand on the first day of a campaign started with
    /// the curve switched on.
    ///
    /// The generator hands every lord a finished sheet, so a twenty-year-old can
    /// open a campaign already fighting like a veteran, and nothing on the map
    /// grows into its strength -- it arrives with it. Starting everyone on the
    /// curve makes the first day look like the rest of the campaign: young lords
    /// young, veterans seasoned, each headed where his talent says.
    ///
    /// Skills this mod develops go exactly where the curve puts them, up or
    /// down. Skills it does not develop -- weapons he does not carry, the
    /// movement he does not use, civil skills without focus -- are never raised,
    /// and come down only past a modest cap. Without the cap a generator's figure
    /// in an unused weapon would stand above his real one, and his level, which
    /// counts every skill, would stay as high as the generator left it.
    /// </summary>
    public static class StartingCurve
    {
        /// <summary>
        /// The share of his main weapon's target that a weapon he does not carry,
        /// or the movement he does not use, may keep: half.
        /// </summary>
        public const int UnusedCombatPercent = 50;

        /// <summary>
        /// A civil or naval skill he has put no focus into keeps at most what one
        /// point of focus would have earned it.
        /// </summary>
        public const int UnusedCivilFocus = 1;

        public static int UnusedCombatCap(int primaryTarget)
        {
            if (primaryTarget <= 0) return 0;
            return primaryTarget * UnusedCombatPercent / 100;
        }

        public static int UnusedCivilCap(float age, float talent)
        {
            return FocusGrowth.TargetFor(age, talent, UnusedCivilFocus);
        }

        /// <summary>
        /// Where one skill should start. A developed skill goes exactly to its
        /// target; an undeveloped one keeps what it has up to the cap and never
        /// gains. Either way it stays inside what the game can show.
        /// </summary>
        public static int Settle(int current, int target, int cap, bool developed)
        {
            int value = developed ? target : (current < cap ? current : cap);
            if (value < 0) value = 0;
            if (value > SkillGrowth.GameSkillMaximum) value = SkillGrowth.GameSkillMaximum;
            return value;
        }
    }
}
