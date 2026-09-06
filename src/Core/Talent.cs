namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// How quickly a given hero learns, and how far he can get.
    ///
    /// Without this every lord converges on the same curve and the map fills
    /// with interchangeable soldiers. Real rosters do not look like that: some
    /// people are simply better at fighting than others who trained just as
    /// long. Talent turns the measured age curve from a destination into an
    /// average, and lets the occasional lord genuinely stand out.
    ///
    /// Derived from the hero's own id rather than rolled, so a hero has the same
    /// talent in every session, across saves and reloads, forever. Nothing is
    /// stored: the id is the seed.
    /// </summary>
    public static class Talent
    {
        /// <summary>The least talented hero learns at this fraction of the norm.</summary>
        public const float Minimum = 0.55f;

        /// <summary>The most talented hero learns at this multiple of the norm.</summary>
        public const float Maximum = 1.65f;

        /// <summary>
        /// A stable multiplier in [Minimum, Maximum] for this hero.
        ///
        /// Distribution is deliberately centre-weighted rather than flat: two
        /// independent draws are averaged, which piles most heroes near the norm
        /// and makes the extremes rare. A map where one lord in six is
        /// exceptional is a map where none of them is.
        /// </summary>
        public static float For(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return 1f;

            uint hash = Hash(heroId);

            // Two 10-bit draws from opposite ends of the hash. Averaging them
            // gives a triangular distribution: the middle is reached many ways,
            // the extremes only one.
            float a = (hash & 0x3FF) / 1023f;
            float b = ((hash >> 20) & 0x3FF) / 1023f;
            float centred = (a + b) * 0.5f;

            return Minimum + (Maximum - Minimum) * centred;
        }

        /// <summary>
        /// The skill level this hero should be heading for, given what his
        /// healthy contemporaries reach and how talented he is.
        /// </summary>
        public static int TargetFor(int ageNormSkill, float talent)
        {
            if (ageNormSkill <= 0) return 0;

            float target = ageNormSkill * talent;
            if (target < 0f) target = 0f;

            // Bannerlord's own skills run to 330; nothing here should invent a
            // number the game would never show.
            if (target > 330f) target = 330f;

            return (int)target;
        }

        /// <summary>
        /// FNV-1a. Chosen over string.GetHashCode because .NET randomises that
        /// per process since Core 2.1 -- a hero's talent would change every time
        /// the game restarted, which is precisely what this must not do.
        /// </summary>
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
