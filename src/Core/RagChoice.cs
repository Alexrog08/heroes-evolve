namespace HeroesEvolve.Core
{
    /// <summary>
    /// Which of several equally cheap things a scavenger came away with.
    ///
    /// The rag search takes the lowest tier it can find, and several items
    /// usually sit at that tier. Taking the first of them meant taking the
    /// first the object manager happened to list, which is the same item every
    /// time: a census of seven cultures came back with seven lords holding the
    /// identical pitchfork, the identical old shield and the identical bandit
    /// saddle. Armour varied only because armour carries a culture and cheap
    /// weapons do not.
    ///
    /// Scavenging is luck, so this makes it luck -- but the reproducible kind.
    /// The same hero robbed of the same slot always comes away with the same
    /// thing, which keeps a robbery replayable from a test command and keeps
    /// the answer stable if the mod is asked twice. FNV-1a rather than
    /// string.GetHashCode for the reason Talent gives: .NET randomises that
    /// per process, so a saved campaign would hand a man different rags after
    /// every restart.
    ///
    /// No randomness that anybody has to store: the seed is the hero and the
    /// slot, both of which the game already knows.
    /// </summary>
    public static class RagChoice
    {
        /// <summary>
        /// An index into a list of equally cheap candidates, or -1 when there
        /// are none. Always in range for a positive count.
        /// </summary>
        public static int Pick(int count, string seed)
        {
            if (count <= 0) return -1;
            if (count == 1 || string.IsNullOrEmpty(seed)) return 0;

            return (int)(Hash(seed) % (uint)count);
        }

        /// <summary>FNV-1a, as everywhere else in this mod that needs a stable hash.</summary>
        private static uint Hash(string text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
