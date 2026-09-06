using System.Collections.Generic;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class TalentTests
    {
        public static void RunAll()
        {
            // Stability is the whole point: a hero's talent must survive a
            // reload, a new session and a different machine. Nothing is stored,
            // so the id has to be the only input.
            float first = Talent.For("lord_4_1");
            Check.True(first == Talent.For("lord_4_1"), "talent is stable for the same id");
            Check.True(Talent.For("lord_4_1") != Talent.For("lord_4_2"),
                       "different heroes get different talent");

            // A missing id must not throw or produce a wild multiplier.
            Check.True(Talent.For(null) == 1f, "a null id is average, not exceptional");
            Check.True(Talent.For("") == 1f, "an empty id is average too");

            // Bounds hold across a wide sample of realistic ids.
            List<float> sample = new List<float>();
            bool allInBounds = true;
            for (int i = 0; i < 4000; i++)
            {
                float t = Talent.For("CharacterObject_" + i);
                if (t < Talent.Minimum || t > Talent.Maximum) allInBounds = false;
                sample.Add(t);
            }
            // Asserted once, not four thousand times: a per-iteration check
            // drowns the suite's pass count and tells you nothing extra.
            Check.True(allInBounds, "talent stays inside its bounds across 4000 ids");

            // Centre-weighted, not flat. With a triangular distribution the
            // middle third should hold well over a third of the population, and
            // the extremes should stay rare -- a map where exceptional lords are
            // common has no exceptional lords.
            float span = Talent.Maximum - Talent.Minimum;
            float lowEdge = Talent.Minimum + span / 3f;
            float highEdge = Talent.Maximum - span / 3f;

            int middle = 0, extremes = 0;
            for (int i = 0; i < sample.Count; i++)
            {
                if (sample[i] > lowEdge && sample[i] < highEdge) middle++;
                if (sample[i] < Talent.Minimum + span * 0.1f
                    || sample[i] > Talent.Maximum - span * 0.1f) extremes++;
            }

            Check.True(middle > sample.Count / 2, "most heroes sit near the norm");
            Check.True(extremes * 10 < sample.Count, "fewer than one in ten is extreme");

            // The target scales with talent and is clamped to what the game can
            // actually show.
            Check.Equal(120, Talent.TargetFor(120, 1f), "average talent reaches the age norm");
            Check.True(Talent.TargetFor(120, Talent.Maximum) > 120, "talent lifts the target");
            Check.True(Talent.TargetFor(120, Talent.Minimum) < 120, "poor talent lowers it");
            Check.Equal(0, Talent.TargetFor(0, Talent.Maximum), "no norm means no target");
            Check.Equal(330, Talent.TargetFor(300, 2f), "the target never exceeds the game's own ceiling");
        }
    }
}
