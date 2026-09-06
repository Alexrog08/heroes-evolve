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

        /// <summary>
        /// The most talented hero learns at this multiple of the norm.
        ///
        /// Calibrated against a real campaign rather than chosen. The founder,
        /// played to eighty-one, topped out at Bow 288; the median campaign-born
        /// lord past fifty-five sits at 134, and the strongest the game produces
        /// on its own reached 230. At 2.0 the most gifted lord this system can
        /// make lands at 268 -- ninety-three percent of a character played for
        /// thirty years, and only a little past what the campaign already grows
        /// unaided.
        ///
        /// So an exceptional lord can stand nearly level with a played hero,
        /// which is the point, while the triangular distribution keeps him rare:
        /// reaching this multiplier at all takes both halves of the hash landing
        /// at their extreme.
        /// </summary>
        public const float Maximum = 2.0f;

        /// <summary>
        /// The three separate aptitudes a hero has. A man good with a lance is
        /// not thereby good with a ledger, and a single talent figure would have
        /// made every prodigy a prodigy at everything.
        /// </summary>
        public const string Combat = "combat";
        public const string Civil = "civil";
        public const string Naval = "naval";

        /// <summary>Combat aptitude, kept as the bare overload it has always been.</summary>
        public static float For(string heroId)
        {
            return For(heroId, Combat);
        }

        /// <summary>
        /// A stable multiplier in [Minimum, Maximum] for this hero in one
        /// domain.
        ///
        /// Distribution is deliberately centre-weighted rather than flat: two
        /// independent draws are averaged, which piles most heroes near the norm
        /// and makes the extremes rare. A map where one lord in six is
        /// exceptional is a map where none of them is.
        ///
        /// The domains are independent because they are salted into the hash
        /// separately, so a lord's sword arm says nothing about his stewardship.
        /// Being gifted in all three at once is possible and duly rare -- with
        /// three independent draws it happens about as often as one lord in
        /// seventy -- which is the point: it should exist, and it should be
        /// remarkable.
        /// </summary>
        public static float For(string heroId, string domain)
        {
            if (string.IsNullOrEmpty(heroId)) return 1f;

            uint hash = Hash(domain + ":" + heroId);

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
