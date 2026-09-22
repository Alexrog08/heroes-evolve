using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    /// <summary>
    /// What a released man is told, and what he is not.
    /// </summary>
    public static class RobberyLedgerTests
    {
        public static void RunAll()
        {
            RobberyLedger.Clear();

            // The plain case: robbed on the way in, told on the way out.
            RobberyLedger.Record("lord_1", 100L);
            Check.True(RobberyLedger.Consume("lord_1", 100L),
                       "a man robbed in this captivity is told so when released");

            // And only once. A release cannot announce itself twice, whatever
            // fires the event -- EndCaptivityAction raises it from more than
            // one place in the same call.
            Check.False(RobberyLedger.Consume("lord_1", 100L),
                        "the same release is never announced a second time");

            // Nobody else is told anything.
            RobberyLedger.Clear();
            RobberyLedger.Record("lord_1", 100L);
            Check.False(RobberyLedger.Consume("lord_2", 100L),
                        "one man's robbery says nothing about another's");

            // The reason the key carries the hour at all: a man taken, freed
            // and taken again is a fresh question. Without the episode he
            // would walk out of an untouched captivity being told he had
            // scavenged, on the strength of a robbery months earlier.
            RobberyLedger.Clear();
            RobberyLedger.Record("lord_1", 100L);
            Check.False(RobberyLedger.Consume("lord_1", 5000L),
                        "a later captivity is a fresh question, not a stale yes");

            // A man never robbed is never told, which is the whole reason this
            // is kept rather than read off his kit at release: rags and a poor
            // man's starting gear sit at the same tier.
            RobberyLedger.Clear();
            Check.False(RobberyLedger.Consume("lord_3", 100L),
                        "a man who was not robbed hears nothing");

            // Nothing accumulates. Entries are consumed on release, so the
            // ledger holds the currently held and robbed and no one else.
            RobberyLedger.Clear();
            RobberyLedger.Record("lord_1", 100L);
            RobberyLedger.Record("lord_2", 100L);
            Check.Equal(2, RobberyLedger.Count, "two captives on the books");
            RobberyLedger.Consume("lord_1", 100L);
            RobberyLedger.Consume("lord_2", 100L);
            Check.Equal(0, RobberyLedger.Count, "and none once both walk out");

            // Robbed twice in one captivity is still one release and one
            // message: the field robbery and a second one in the cells.
            RobberyLedger.Clear();
            RobberyLedger.Record("lord_1", 100L);
            RobberyLedger.Record("lord_1", 100L);
            Check.Equal(1, RobberyLedger.Count, "two robberies in one captivity are one entry");

            // A hero the game cannot name is not tracked, rather than tracked
            // under an empty key where he would collide with every other.
            RobberyLedger.Clear();
            RobberyLedger.Record(null, 100L);
            RobberyLedger.Record("", 100L);
            Check.Equal(0, RobberyLedger.Count, "a nameless hero is not on the books");
            Check.False(RobberyLedger.Consume(null, 100L), "and asking about him is a no");

            RobberyLedger.Clear();
        }
    }
}
