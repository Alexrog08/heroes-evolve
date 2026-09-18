using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class FocusGrowthTests
    {
        public static void RunAll()
        {
            // No focus, no target. Deliberate rather than a gap: 72% of lords
            // have never put a point into Smithing and they should stay at zero.
            Check.Equal(0, FocusGrowth.TargetFor(40f, 1.27f, 0, SkillGrowth.CivilPeakNorm),
                        "an uninvested skill has no target");
            Check.Equal(0, FocusGrowth.TargetFor(40f, 1.27f, -1, SkillGrowth.CivilPeakNorm),
                        "nor does a negative one");

            // Three focus is the median lord, so it earns the plain
            // age-and-talent target -- the same figure a weapon skill would get
            // if weapons were measured on this field's norm.
            int typical = FocusGrowth.TargetFor(40f, 1.27f, (int)FocusGrowth.TypicalFocus, SkillGrowth.PeakNorm);
            int combat = SkillGrowth.PrimaryTarget(40f, 1.27f);
            Check.True(typical >= combat - 2 && typical <= combat + 2,
                       "median focus earns what a primary weapon earns");

            // Beyond that focus adds nothing: talent says how far a man can go,
            // focus says in what. Less than the median aims lower.
            Check.Equal(typical, FocusGrowth.TargetFor(40f, 1.27f, 5, SkillGrowth.PeakNorm),
                        "heavy investment arrives at his own ceiling and stops");
            Check.True(FocusGrowth.TargetFor(40f, 1.27f, 1, SkillGrowth.PeakNorm) < typical,
                       "a token point aims lower");

            // Each field on its own scale, so the same hero aims lower in the
            // ledger than with a sword -- as TaleWorlds wrote them.
            Check.True(FocusGrowth.TargetFor(40f, 1.27f, 3, SkillGrowth.CivilPeakNorm) < typical,
                       "the civil norm is the smaller one");

            // A hero who poured everything into one skill must not chase a
            // number the game would never show, and the scale sees to it without
            // anything holding him down: the most gifted hero the dice can deal
            // lands exactly on the most TaleWorlds ever wrote in that field.
            int hoarded = FocusGrowth.TargetFor(60f, Talent.Maximum, 100, SkillGrowth.CivilPeakNorm);
            Check.Equal(250, hoarded, "which in the ledger is Pharon's 250 in trade");
            Check.Equal(300, FocusGrowth.TargetFor(60f, Talent.Maximum, 100, SkillGrowth.PeakNorm),
                        "and with a weapon, Caladog's 300");
            Check.Equal(279, FocusGrowth.TargetFor(60f, Talent.Maximum, 100, SkillGrowth.NavalPeakNorm),
                        "and at sea, Halthdar's 280");
            Check.True(hoarded < SkillGrowth.GameSkillMaximum,
                       "so nothing has to be clamped at what the game can show");

            // Age matters here as it does everywhere: the peak belongs to old
            // men, in the ledger as much as on the field.
            Check.True(FocusGrowth.TargetFor(60f, 1.27f, 3, SkillGrowth.CivilPeakNorm)
                       > FocusGrowth.TargetFor(25f, 1.27f, 3, SkillGrowth.CivilPeakNorm),
                       "a veteran administrator outranks a young one");

            // And the domains are genuinely separate, so a lord can be gifted
            // with a lance and indifferent with a ledger.
            string id = "lord_4_1";
            Check.True(Talent.For(id, Talent.Combat) != Talent.For(id, Talent.Civil),
                       "combat and civil aptitude differ");
            Check.True(Talent.For(id, Talent.Naval) != Talent.For(id, Talent.Civil),
                       "naval and civil aptitude differ");
        }
    }
}
