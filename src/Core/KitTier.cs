namespace HeroesEvolve.Core
{
    /// <summary>
    /// Where clothing ends and kit begins.
    ///
    /// A subscriber reported lords in silly armour, "even female dress", and the
    /// catalogue says exactly how that happens. Below tier 2 the armour on sale
    /// is the wardrobe: 104 of the 115 body pieces down there carry the game's
    /// own Civilian flag, and so do 33 of the 34 leg pieces and 54 of the 67
    /// head pieces. Every dress in the game lives there -- nordic_civilian_dress
    /// at 12 armour, khuzait_dress at 12, vlandian_woman_dress at 4.
    ///
    /// The game's tier is the right instrument for the cut, and the only one
    /// that works. The Civilian flag alone cannot do it: every horse and every
    /// harness in the game carries it, and so do 37 of the 46 boots on sale,
    /// which are ordinary battle boots a soldier also wears into town. Nor can
    /// the equipment rosters: peasants and bandits fight in their dresses, so
    /// every dress in the game appears in somebody's battle set.
    ///
    /// The tier does it cleanly because the game normalises it per slot --
    /// DefaultItemValueModel weighs a leg piece 1.6 times a body one, a cape 1.8
    /// -- and the two populations do not overlap anywhere: the best piece under
    /// tier 2 has 13 armour and the weakest at tier 2 has 14, slot by slot.
    ///
    /// What it costs is a thin leg rack -- 12 pieces on sale rather than 46 --
    /// and nothing anywhere else: 198 helmets, 154 body pieces, 63 capes and 25
    /// gloves remain. A lord who can find no boots keeps the ones he has.
    /// </summary>
    public static class KitTier
    {
        /// <summary>The lowest tier that is armour rather than clothing.</summary>
        public const int Lowest = 2;

        /// <summary>
        /// Whether a piece the game scores at this tier is clothing rather than
        /// kit. Tiers are the 1-to-6 kind this mod speaks everywhere; a tier of
        /// nought or less means the value model had no opinion, and those are
        /// left to the callers that know what they are looking at.
        /// </summary>
        public static bool IsClothing(int tier)
        {
            return tier > 0 && tier < Lowest;
        }
    }
}
