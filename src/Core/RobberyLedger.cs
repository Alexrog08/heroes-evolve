using System.Collections.Generic;

namespace HeroesEvolve.Core
{
    /// <summary>
    /// Who was robbed during the spell in the cells he is in now.
    ///
    /// It exists because one reader of the robbery message cannot read it. A
    /// robbery announces itself the instant it happens, and that works for
    /// every hero on the map except the one whose capture took the map away:
    /// the player sits through his own captivity with no campaign screen to
    /// print to, so the one robbery he most wants to hear about is the one he
    /// never sees. The answer is to say it again when he is let out, which
    /// needs knowing whether it happened.
    ///
    /// Reading his kit at that moment is not an answer. Rags and the gear a
    /// poor man starts the game in sit at the same tier, so a pauper released
    /// without being touched would be told he had scavenged.
    ///
    /// Keyed on the episode rather than the man, so being freed and taken
    /// again is a fresh question rather than a stale yes. Consuming the entry
    /// on release keeps this to the heroes currently held and robbed, which is
    /// a handful. Nothing here is written to the save: a reload loses the
    /// message and nothing else, which is the right thing for it to lose.
    /// </summary>
    public static class RobberyLedger
    {
        private static readonly HashSet<string> Robbed = new HashSet<string>();

        /// <summary>How many robbed captives are currently on the books.</summary>
        public static int Count
        {
            get { return Robbed.Count; }
        }

        public static void Clear()
        {
            Robbed.Clear();
        }

        /// <summary>
        /// Notes a robbery in the captivity that began at this hour.
        /// </summary>
        public static void Record(string heroId, long episode)
        {
            if (string.IsNullOrEmpty(heroId)) return;
            Robbed.Add(KeyFor(heroId, episode));
        }

        /// <summary>
        /// Whether this captivity had a robbery in it, forgetting it either
        /// way so the same release can never be announced twice.
        /// </summary>
        public static bool Consume(string heroId, long episode)
        {
            if (string.IsNullOrEmpty(heroId)) return false;
            return Robbed.Remove(KeyFor(heroId, episode));
        }

        private static string KeyFor(string heroId, long episode)
        {
            return heroId + "@" + episode;
        }
    }
}
