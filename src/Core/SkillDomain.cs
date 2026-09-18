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
        /// Passed as highestWritten by a caller with no roster to ask. A token
        /// point of focus aims at a third of the peak and could not reach an
        /// envelope anyway, so the answer is the same either way.
        /// </summary>
        public const int NoCeiling = 0;

        /// <summary>
        /// Where a skill should be heading for a hero of this age and aptitude
        /// who has invested this much focus in it, given the most TaleWorlds
        /// ever wrote anyone in that skill.
        ///
        /// Zero focus means zero target, and that is deliberate rather than a
        /// gap: 72% of lords have never put a point into Smithing and they
        /// should stay at zero. The purpose is to develop what a lord has chosen
        /// to be, not to make every lord a blacksmith.
        ///
        /// Focus may carry a man past what his years and aptitude alone give
        /// him, but not past the highest figure TaleWorlds wrote in that skill.
        /// Two multipliers were compounding here -- the peak norm times his
        /// talent, then that times his focus -- and a well-invested steward came
        /// out at 380 where no lord was written above 240 in stewardship and no
        /// hero above 250 in anything civil. Clamping at the game's display
        /// maximum of 330 was not enough: 330 is what the engine can show, not
        /// what the game was balanced for, and stewardship feeds armies.
        ///
        /// His own aptitude is never capped by somebody else's sheet, though. A
        /// prodigy whose years and talent alone aim him above the envelope keeps
        /// that figure, which is how the rarest heroes still end up beyond
        /// anything TaleWorlds wrote. Only the part focus adds is held.
        /// </summary>
        public static int TargetFor(float age, float talent, int focus, int highestWritten)
        {
            if (focus <= 0) return 0;

            float factor = focus / TypicalFocus;
            if (factor > MaximumFocusFactor) factor = MaximumFocusFactor;

            int peak = Talent.TargetFor(SkillGrowth.PeakNorm, talent);
            int alone = (int)(peak * SkillGrowth.Maturity(age));
            int target = (int)(alone * factor);

            int ceiling = highestWritten > alone ? highestWritten : alone;
            if (ceiling > SkillGrowth.GameSkillMaximum) ceiling = SkillGrowth.GameSkillMaximum;

            return target < ceiling ? target : ceiling;
        }
    }
}
