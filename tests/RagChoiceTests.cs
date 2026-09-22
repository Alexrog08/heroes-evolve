using System.Collections.Generic;
using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    /// <summary>
    /// Which of several equally cheap things a scavenger came away with.
    /// </summary>
    public static class RagChoiceTests
    {
        public static void RunAll()
        {
            // Always a real index. A pick out of range would throw inside a
            // capture, which is the one place this must not do anything.
            for (int count = 1; count <= 40; count++)
            {
                int pick = RagChoice.Pick(count, "lord_1:3");
                Check.True(pick >= 0 && pick < count, "the pick is always in range");
            }

            // Nothing to pick from is not a pick.
            Check.Equal(-1, RagChoice.Pick(0, "lord_1:3"), "an empty shelf answers nothing");
            Check.Equal(-1, RagChoice.Pick(-4, "lord_1:3"), "and so does a nonsense count");

            // One candidate needs no draw, seed or not.
            Check.Equal(0, RagChoice.Pick(1, "lord_1:3"), "one candidate is the answer");
            Check.Equal(0, RagChoice.Pick(1, null), "even without a seed");
            Check.Equal(0, RagChoice.Pick(9, null), "and a missing seed takes the first");
            Check.Equal(0, RagChoice.Pick(9, ""), "an empty one too");

            // The same man robbed of the same slot scavenges the same thing.
            // This is what keeps hev.test_robbery replayable and what stops a
            // reload handing him something else.
            Check.Equal(RagChoice.Pick(7, "lord_1:3"), RagChoice.Pick(7, "lord_1:3"),
                        "the same seed always draws the same item");

            // And different men, or different slots, spread out. The whole
            // point: a census once showed seven lords of seven cultures all
            // holding the identical pitchfork.
            HashSet<int> spread = new HashSet<int>();
            for (int i = 0; i < 40; i++) spread.Add(RagChoice.Pick(6, "lord_" + i + ":3"));
            Check.True(spread.Count >= 4, "forty heroes do not all take the same one");

            HashSet<int> slots = new HashSet<int>();
            for (int slot = 0; slot < 11; slot++) slots.Add(RagChoice.Pick(6, "lord_1:" + slot));
            Check.True(slots.Count >= 3, "one hero's slots do not all draw alike");
        }
    }
}
