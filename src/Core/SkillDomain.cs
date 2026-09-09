namespace HeroesEvolve.Core
{
    /// <summary>
    /// Which aptitude governs a skill. Three, because a man good with a lance is
    /// not thereby good with a ledger or a tiller, and one talent figure would
    /// have made every prodigy a prodigy at everything.
    /// </summary>
    public enum SkillDomain
    {
        Combat,
        Civil,
        Naval
    }

    /// <summary>
    /// How far a non-combat skill should have come, given how much the hero's
    /// own focus says he cares about it.
    ///
    /// Combat skills are chosen by what a lord carries. Everything else is
    /// chosen by where the game has already spent his focus, which is its own
    /// statement of what this lord is for -- and the campaign shows that
    /// statement is real. Every one of 507 lords holds between 26 and 40 focus
    /// points, and they are spread rather than piled: 81% have some in Scouting,
    /// 28% in Smithing, 6% in Shipmaster. There is a per-lord fingerprint there
    /// to follow.
    ///
    /// The relationship between focus and level turns out to be the same on both
    /// sides of the sheet, which is why one norm serves all of them: at the
    /// median, three focus goes with a skill around 108 in One Handed and around
    /// 107 in Leadership.
    /// </summary>
    public static class FocusGrowth
    {
        /// <summary>
        /// Focus held by the median lord in a skill he actually invests in.
        /// This is the amount that earns the full age-and-talent target; less
        /// earns proportionally less, more earns proportionally more.
        /// </summary>
        public const float TypicalFocus = 3f;

        /// <summary>
        /// Ceiling on the focus multiplier, so a hero who has poured everything
        /// into one skill does not chase a target the game would never show.
        /// </summary>
        public const float MaximumFocusFactor = 1.8f;

        /// <summary>
        /// Where a skill should be heading for a hero of this age and aptitude
        /// who has invested this much focus in it.
        ///
        /// Zero focus means zero target, and that is deliberate rather than a
        /// gap: 72% of lords have never put a point into Smithing and they
        /// should stay at zero. The purpose is to develop what a lord has chosen
        /// to be, not to make every lord a blacksmith.
        /// </summary>
        public static int TargetFor(float age, float talent, int focus)
        {
            if (focus <= 0) return 0;

            float factor = focus / TypicalFocus;
            if (factor > MaximumFocusFactor) factor = MaximumFocusFactor;

            int peak = Talent.TargetFor(SkillGrowth.PeakNorm, talent);
            return (int)(peak * SkillGrowth.Maturity(age) * factor);
        }
    }
}
