using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    /// <summary>
    /// What a robbery leaves a man standing in.
    /// </summary>
    public static class RagTierTests
    {
        public static void RunAll()
        {
            // The chest is held at the mod's own clothing line, because every
            // gown in the game is a tier-1 body armour and nothing can tell
            // them from the tunics beside them.
            Check.Equal(KitTier.Lowest, RagTier.Body, "the chest keeps the clothing floor");
            Check.Equal(RagTier.Body, RagTier.For(true), "and For says so for body armour");
            Check.True(KitTier.IsClothing(RagTier.Everything),
                       "everything else is allowed below that floor, on purpose");

            // Everywhere else falls to the bottom. Not flavour: Nord sells four
            // pairs of boots at tier 1 and none at tier 2, and a lord can never
            // buy into an empty slot, so a tier-2 floor would strand him
            // barefoot for the rest of the campaign.
            Check.Equal(1, RagTier.Everything, "the rest of the man takes the cheapest there is");
            Check.Equal(RagTier.Everything, RagTier.For(false), "and For says so for everything else");

            // The whole point is that these are worse than what a man loses.
            Check.True(RagTier.Everything < RagTier.Body, "rags are not evenly bad");
            Check.True(RagTier.Body < GrantTier.Maximum,
                       "and worse than what the repair hands a lord who never had anything");
        }
    }
}
