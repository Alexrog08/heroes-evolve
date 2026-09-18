using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class StartingCurveTests
    {
        public static void RunAll()
        {
            // A skill the mod develops starts exactly where the curve puts it,
            // whichever way that is.
            Check.Equal(188, StartingCurve.Settle(250, 188, 94, true), "a developed skill above the curve comes down to it");
            Check.Equal(120, StartingCurve.Settle(80, 120, 60, true), "a developed skill below the curve goes up to it");

            // A skill it does not develop is never raised, and is lowered only
            // past its cap.
            Check.Equal(40, StartingCurve.Settle(40, 0, 94, false), "an undeveloped skill under its cap is left alone");
            Check.Equal(94, StartingCurve.Settle(200, 0, 94, false), "an undeveloped skill over its cap comes down to it");

            // The case the cap exists for: a king written with Two Handed 200 who
            // fights with a polearm aimed at 210 must not keep the unused weapon
            // standing almost level with his real one.
            int primary = 210;
            int unused = StartingCurve.Settle(200, 0, StartingCurve.UnusedCombatCap(primary), false);
            Check.True(unused <= primary / 2, "an unused weapon keeps at most half of the real one");

            Check.Equal(94, StartingCurve.UnusedCombatCap(188), "the unused-weapon cap is half the main weapon's target");
            Check.Equal(0, StartingCurve.UnusedCombatCap(0), "no target, no cap");
            Check.Equal(FocusGrowth.TargetFor(40f, 1.2f, 1, FocusGrowth.NoCeiling),
                        StartingCurve.UnusedCivilCap(40f, 1.2f),
                        "a civil skill with no focus keeps at most what one focus point would give");

            // Inside what the game can show.
            Check.Equal(SkillGrowth.GameSkillMaximum, StartingCurve.Settle(0, 400, 0, true), "never past the game's maximum");
            Check.Equal(0, StartingCurve.Settle(-5, -1, 0, true), "never below zero");
        }
    }
}
