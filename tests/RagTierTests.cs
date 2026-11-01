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
            // The chest falls to the bottom with the rest of him. The gowns
            // down there are kept off it by the game's own flags -- Civilian,
            // and the one that bars a sex -- not by the tier (Rags.Cheapest).
            Check.Equal(RagTier.Everything, RagTier.Body, "the chest takes the cheapest there is too");
            Check.Equal(RagTier.Body, RagTier.For(true, false, false), "and For says so for body armour");

            // The weapons keep the same floor. Tier 1 of the weapon rack is the
            // peasant's -- pitchforks, hammers, sickles -- and a robbed lord was
            // coming home with a pitchfork. One floor for weapons everywhere,
            // the starting kit's own.
            Check.Equal(KitTier.Lowest, RagTier.Weapon, "weapons keep the kit's floor");
            Check.Equal(GrantTier.Minimum, RagTier.Weapon, "the same floor the starting kit keeps");
            Check.Equal(RagTier.Weapon, RagTier.For(false, true, false), "and For says so for weapons");
            Check.False(KitTier.IsClothing(RagTier.Weapon), "no weapon rag comes off the peasant's rack");
            Check.True(KitTier.IsClothing(RagTier.Everything),
                       "everything else is allowed below that floor, on purpose");

            // Everywhere else falls to the bottom. Not flavour: Nord sells four
            // pairs of boots at tier 1 and none at tier 2, and a lord can never
            // buy into an empty slot, so a tier-2 floor would strand him
            // barefoot for the rest of the campaign.
            Check.Equal(1, RagTier.Everything, "the rest of the man takes the cheapest there is");
            Check.Equal(RagTier.Everything, RagTier.For(false, false, false), "and For says so for everything else");

            // The cape alone goes to tier 0, where the plain cloth is. Tier 1
            // of the cape rack is mostly pauldrons and fur.
            Check.Equal(0, RagTier.Cape, "the cape comes off the bottom rung");
            Check.Equal(RagTier.Cape, RagTier.For(false, false, true), "and For says so for a cape");
            Check.True(RagTier.Cape < RagTier.Everything, "the cape is the one piece below the rest");

            // Below the line GrantService.NeedsGrant reads as a lord who never
            // had a soldier's kit, on purpose. That used to make a robbed man
            // look broken to the next day's repair; the repair now judges a lord
            // only the day after the game makes him, never a robbed one.
            Check.True(KitTier.IsClothing(RagTier.Body),
                       "the chest rag is clothing-tier now; the repair never looks at a robbed man");

            // The whole point is that these are worse than what a man loses.
            Check.True(RagTier.Everything < RagTier.Weapon, "only the weapons are held up");
            Check.True(RagTier.Body < GrantTier.Maximum,
                       "and worse than what the repair hands a lord who never had anything");
        }
    }
}
