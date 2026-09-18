namespace HeroesEvolve.Core
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
        /// The talent that stands exactly level with the best lord TaleWorlds
        /// ever wrote in a field.
        ///
        /// What each field's norm is measured against -- see
        /// SkillGrowth.PeakNormFor -- so a hero dealt this much finishes on that
        /// field's ceiling: 300 with a sword, 250 in a ledger, 279 at sea. It is
        /// the anchor of the whole scale, and Maximum sits a little above it on
        /// purpose.
        /// </summary>
        public const float Level = 2.07f;

        /// <summary>
        /// The most talent the dice ever deal.
        ///
        /// A little past Level, and that gap is the whole point. A sheet is
        /// honoured in full, surplus included, so the best-written lord of a
        /// field finishes five percent above its ceiling -- Caladog at 315 --
        /// and with the dice stopping at Level nobody could ever pass him, nor
        /// Phenoria in the ledger, nor Halthdar at sea. Three men no campaign
        /// could ever produce the equal of is not a world with luck in it.
        ///
        /// At 2.25 the rarest hero finishes 326 with a sword, 272 in a ledger,
        /// 303 at sea: past the best written man in his field by three or four
        /// percent, and short of the 330 the game can display, which only a
        /// played hero approaches. That last margin is deliberate. A prodigy
        /// should be remarkable and still leave the player the longest road.
        ///
        /// Rare by construction rather than by a rule: the two draws For
        /// averages make a triangular distribution, so about one hero in two
        /// hundred passes the best sheet in a given field -- three of the 484
        /// lords TaleWorlds wrote, and as many again among the wanderers a
        /// campaign hires and the heroes it bears. The luckiest man alive may
        /// perfectly well be a companion nobody expected.
        ///
        /// Higher was tried on paper: at 2.30 seven of the written lords are
        /// passed, but the dice reach 333 and the game can only show 330, so
        /// the top of the world would be a wall rather than a rarity.
        /// </summary>
        public const float Maximum = 2.25f;

        /// <summary>
        /// The talent the middle hero is dealt. The two draws For averages are
        /// symmetric about the centre of the bounds, so half of all heroes fall
        /// either side of the midpoint -- 1.40, not the 1.0 a multiplier
        /// suggests, and a lord of median talent peaks near 203 with a sword
        /// rather than on the norm. Which is why a sheet written at its field's
        /// norm asks for only 1.05 (AuthoredTalent.Floor) and the dice usually
        /// answer first.
        /// </summary>
        public const float Median = (Minimum + Maximum) / 2f;

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
            if (target > SkillGrowth.GameSkillMaximum) target = SkillGrowth.GameSkillMaximum;

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
