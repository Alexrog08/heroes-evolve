using System.Collections.Generic;

namespace HeroesEvolve.Core
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
        /// <summary>
        /// Never grant tier 1: that is the bug's own uniform, and KitTier says
        /// why it is everybody's. One constant for both, so the rule that keeps
        /// clothing off a shelf and the rule that keeps it out of a repair
        /// cannot drift apart.
        /// </summary>
        public const int Minimum = KitTier.Lowest;

        /// <summary>
        /// Above this a granted kit starts competing with what the hero should
        /// be buying for himself.
        /// </summary>
        public const int Maximum = 3;

        /// <summary>
        /// The band to grant at, narrowed by the hero's own ceiling but never
        /// below Minimum.
        ///
        /// A hero whose merit does not reach tier 2 is granted tier 2 anyway.
        /// The band used to fall to his ceiling instead, so that a hero the
        /// merit model scored at tier 1 was dressed from the tier-1 rack -- and
        /// that rack is the wardrobe, which is how a lord ends up in a dress.
        /// The ceiling is a purchase cap and this is a repair: it exists to make
        /// a naked noble presentable, and tier 2 is the cheapest way to be
        /// dressed rather than clothed.
        /// </summary>
        public static void Band(int ceiling, out int lowest, out int highest)
        {
            highest = ceiling < Maximum ? ceiling : Maximum;
            if (highest < Minimum) highest = Minimum;
            lowest = Minimum;
        }

        /// <summary>
        /// The tier to grant from when the band holds nothing: the cheapest tier
        /// above it, or failing that the best below it. Zero when there is
        /// neither.
        ///
        /// The fallback used to be FindBest -- the finest thing under the
        /// hero's ceiling -- which is the purchase question, not this one. When
        /// a culture made no throwing weapon at tier 2 or 3, the kit reached for
        /// the top of the rack: the census found freshly repaired lords handed
        /// eastern_javelin_3_t4 at tier 6, 28,237 denars of javelins, which is
        /// exactly what Maximum exists to stop. The nearest tier above keeps a
        /// kit a kit. Below the band comes last, as it does for the rags: tier 1
        /// is the peasant's rack, and only a culture that offers nothing else is
        /// dressed from it.
        /// </summary>
        public static int Fallback(IList<int> tiersOnOffer, int lowest, int highest)
        {
            if (tiersOnOffer == null) return 0;

            int above = int.MaxValue;
            int below = 0;
            for (int i = 0; i < tiersOnOffer.Count; i++)
            {
                int tier = tiersOnOffer[i];
                if (tier > highest && tier < above) above = tier;
                else if (tier < lowest && tier > below) below = tier;
            }

            return above != int.MaxValue ? above : below;
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
