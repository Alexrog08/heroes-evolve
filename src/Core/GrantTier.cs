namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// What tier the free repair hands out, as opposed to what a lord may
    /// eventually buy.
    ///
    /// These are different questions and conflating them broke the design. The
    /// repair used to grant the best item within the hero's ceiling, which for a
    /// tier-6 lord is a kit worth some 476,000 denars given away -- and then the
    /// purchase engine, whose whole purpose is to let a lord earn his way up, had
    /// nothing left to sell him. The ceiling is a *purchase* cap. The repair only
    /// has to make a naked noble presentable.
    ///
    /// The band is 2 to 3 because the campaign says what those tiers look like.
    /// Tier 1 is literally the civilian clothing the bug leaves behind --
    /// aserai_civil_d, vlandian_woman_dress, nord_casual_tunic -- so granting it
    /// would reproduce the symptom. Tier 2 is light military kit: padded armour,
    /// studded leather, warrior coats. Tier 3 is proper armour: scale, mail,
    /// lamellar. Somewhere in there is what a young lord turns up with.
    /// </summary>
    public static class GrantTier
    {
        /// <summary>Never grant tier 1: that is the bug's own uniform.</summary>
        public const int Minimum = 2;

        /// <summary>
        /// Above this a granted kit starts competing with what the hero should
        /// be buying for himself.
        /// </summary>
        public const int Maximum = 3;

        /// <summary>
        /// The band to grant at, narrowed by the hero's own ceiling.
        ///
        /// A hero whose merit does not reach tier 2 gets whatever his ceiling
        /// allows rather than nothing: the repair exists to clothe him, and
        /// refusing on grounds of merit would leave him in the rags the bug gave
        /// him.
        /// </summary>
        public static void Band(int ceiling, out int lowest, out int highest)
        {
            highest = ceiling < Maximum ? ceiling : Maximum;
            lowest = highest < Minimum ? highest : Minimum;

            if (highest < 1) highest = 1;
            if (lowest < 1) lowest = 1;
        }

        /// <summary>
        /// Which of several equally acceptable items a hero receives.
        ///
        /// Chosen from his own id rather than rolled, so the same lord is dressed
        /// the same way on every load, and two lords of one culture and ceiling
        /// are not issued identical kit -- which is what picking the best always
        /// produced, and what makes a map of clones.
        /// </summary>
        public static int Choose(string heroId, string slotKey, int candidateCount)
        {
            if (candidateCount <= 1) return 0;
            if (string.IsNullOrEmpty(heroId)) return 0;

            uint hash = 2166136261u;
            string text = heroId + "/" + slotKey;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }

            return (int)(hash % (uint)candidateCount);
        }
    }
}
