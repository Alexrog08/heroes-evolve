using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class GrantTierTests
    {
        public static void RunAll()
        {
            int lowest, highest;

            // A capable lord is still only dressed, not outfitted for life. The
            // ceiling is what he may buy; the grant is what makes him decent.
            GrantTier.Band(6, out lowest, out highest);
            Check.Equal(2, lowest, "a tier-6 lord is granted from tier 2");
            Check.Equal(3, highest, "...up to tier 3, no further");

            GrantTier.Band(3, out lowest, out highest);
            Check.Equal(2, lowest, "a tier-3 lord gets the same band");
            Check.Equal(3, highest, "...which his ceiling exactly allows");

            // Below the band, the ceiling wins. Refusing on merit would leave a
            // hero in the rags the bug gave him, which is the thing being fixed.
            GrantTier.Band(2, out lowest, out highest);
            Check.Equal(2, lowest, "a tier-2 lord gets tier 2");
            Check.Equal(2, highest, "and no more");

            // And below the band nothing falls to tier 1, because tier 1 is the
            // wardrobe -- see KitTier. A repair dresses a man; it does not clothe
            // him in what the bug left him wearing.
            GrantTier.Band(1, out lowest, out highest);
            Check.Equal(KitTier.Lowest, lowest, "a tier-1 lord is dressed at the lowest tier that is kit");
            Check.Equal(KitTier.Lowest, highest, "and no further");

            GrantTier.Band(0, out lowest, out highest);
            Check.True(lowest >= KitTier.Lowest && highest >= KitTier.Lowest,
                       "a nonsensical ceiling still yields kit rather than clothing");

            Check.True(KitTier.IsClothing(1), "tier 1 is clothing");
            Check.True(!KitTier.IsClothing(KitTier.Lowest), "tier 2 is kit");
            Check.True(!KitTier.IsClothing(0), "an item the value model had no opinion about is left alone");

            // Never tier 1 when anything better is allowed: tier 1 is the
            // civilian clothing the bug leaves behind, so granting it would hand
            // the hero back his own symptom.
            for (int ceiling = 1; ceiling <= 6; ceiling++)
            {
                GrantTier.Band(ceiling, out lowest, out highest);
                Check.True(lowest >= GrantTier.Minimum, "the grant never drops to civilian clothing");
            }

            // The choice is stable: the same lord is dressed the same way on
            // every load, since nothing about this is stored.
            Check.Equal(GrantTier.Choose("lord_4_1", "w0", 7),
                        GrantTier.Choose("lord_4_1", "w0", 7), "the same hero and slot choose alike");

            // Different heroes, and different slots of one hero, diverge -- which
            // is the point: picking the best always issued identical kit.
            int differentHeroes = 0, differentSlots = 0;
            for (int i = 0; i < 200; i++)
            {
                string a = "lord_" + i + "_1";
                string b = "lord_" + i + "_2";
                if (GrantTier.Choose(a, "w0", 7) != GrantTier.Choose(b, "w0", 7)) differentHeroes++;
                if (GrantTier.Choose(a, "w0", 7) != GrantTier.Choose(a, "Body", 7)) differentSlots++;
            }
            Check.True(differentHeroes > 120, "two lords are usually dressed differently");
            Check.True(differentSlots > 120, "and one lord's slots are chosen independently");

            // Degenerate inputs must not throw or index out of range.
            Check.Equal(0, GrantTier.Choose("lord_4_1", "w0", 1), "one candidate is always the answer");
            Check.Equal(0, GrantTier.Choose("lord_4_1", "w0", 0), "no candidates yields a safe index");
            Check.Equal(0, GrantTier.Choose(null, "w0", 7), "a missing id yields a safe index");

            bool inRange = true;
            for (int i = 0; i < 500; i++)
            {
                int pick = GrantTier.Choose("CharacterObject_" + i, "Head", 4);
                if (pick < 0 || pick >= 4) inRange = false;
            }
            Check.True(inRange, "the choice always indexes a real candidate");
        }
    }
}
