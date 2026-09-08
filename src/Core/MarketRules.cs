namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// When an item on sale counts as an upgrade, and which of two offers comes
    /// first. Pure, so the rule that decides whether five hundred lords swap
    /// gear every time they walk through a gate can be proved without a running
    /// campaign.
    ///
    /// The purchase engine never re-decides what a hero is. It reads the slot he
    /// already fills and looks for something better of the same kind: repair
    /// decides what you are, buying decides how good you are. So every rule here
    /// is a comparison against what is already worn, never a choice of role.
    /// </summary>
    public static class MarketRules
    {
        /// <summary>
        /// Half a tier, in hundredths: the smallest improvement worth a
        /// purchase.
        ///
        /// This number comes out of an inconsistency in the rule it replaces.
        /// The integer tier is a rounding of a continuous quantity -- the game
        /// computes Tierf and takes round(Tierf) -- so demanding a whole integer
        /// tier let through an item 0.02 better that happened to straddle a
        /// rounding boundary, while refusing one 0.98 better that did not. The
        /// gate was letting the insignificant past and blocking the large.
        ///
        /// Half a tier is the midpoint of what the old rule already permitted
        /// (arbitrarily small, at a boundary) and what it already refused (just
        /// under a whole tier, inside one). It is strictly stricter than today
        /// on the marginal cases and strictly looser on the big ones, which is
        /// the right direction on both counts: fewer purchases, each of them
        /// worth making.
        /// </summary>
        public const int MinimumGain = 50;

        /// <summary>
        /// What a man will give up to be dressed like his own people, in the
        /// same hundredths as everything else here. One whole tier.
        ///
        /// This replaces a hard culture filter, and the filter was wrong in a
        /// way only a live campaign showed. Armour in this game is always
        /// culture-stamped -- measured on the shipped catalogue: of 656 body
        /// armours, 107 leg, 90 hand and 1079 head, exactly two carry no
        /// culture at all. So a Battanian lord holding a conquered Khuzait fief
        /// could buy a sword and never, for the rest of the campaign, buy a
        /// cuirass. And nothing would send him home: Hero.UpdateHomeSettlement
        /// follows holdings, never culture, and the only AI behaviour in the
        /// game that reads Culture to pick a destination is bandits choosing a
        /// hideout. He would simply stand in Khuzait lands in his shirt.
        ///
        /// A preference has none of that failure mode and keeps everything the
        /// filter was for. Applied to what is worn as well as to what is
        /// offered, so MinimumGain keeps its meaning, the arithmetic comes out
        /// exactly as intended:
        ///
        ///   offered >= worn + 0.5 tier   for a foreigner replacing a foreigner
        ///   offered >= worn - 0.5 tier   for his own culture replacing a foreigner
        ///   offered >= worn + 1.5 tiers  for a foreigner replacing his own
        ///
        /// So abroad he wears what he can get; at home he trades back into his
        /// own colours for the same tier, or half a tier worse; and once he is
        /// dressed as his people dress, only a very large improvement moves him
        /// out again. Nobody is stranded and the map still looks like itself.
        ///
        /// One hundred rather than fifty because fifty makes culture a
        /// tiebreak only -- it would never buy a downgrade, and the whole point
        /// is that a Battanian in Battania should be willing to take a slightly
        /// plainer Battanian helm over the fine Khuzait one he is wearing.
        /// A constant rather than a setting until a census says what it should
        /// be; it is one number and the build takes thirty seconds.
        /// </summary>
        public const int CulturePreference = 100;

        /// <summary>
        /// A fine tier as this hero values it, rather than as the game scores
        /// it. Neutral items get nothing: they are equally acceptable to
        /// everyone, and paying them the bonus would rank a generic helm above
        /// a hero's own culture's.
        /// </summary>
        public static int Effective(int fine, bool ownCulture)
        {
            return ownCulture ? fine + CulturePreference : fine;
        }

        /// <summary>
        /// The same, in whole tiers, for the coarse term the ordering leads on.
        ///
        /// Derived from the one constant rather than given a second, so the two
        /// halves of the comparison cannot drift apart. At a preference of 100
        /// his own culture is worth a whole rank; at 50 it is worth none, and
        /// culture decides only ties. That is the honest shape of the knob.
        /// </summary>
        public static int EffectiveTier(int tier, bool ownCulture)
        {
            return ownCulture ? tier + CulturePreference / 100 : tier;
        }

        /// <summary>
        /// True when an offer is enough better than what is worn to be worth
        /// buying.
        ///
        /// Tiers arrive in hundredths of the game's own fractional Tierf, so a
        /// lord can trade the worst sword of a tier for the best one without
        /// waiting for the next integer step -- but only when the gap is real.
        /// Comparing raw item value instead would have five hundred heroes
        /// churning their kit for a few points at every gate; MinimumGain is
        /// what stops that, and one purchase per lord per day is what stops it
        /// twice.
        ///
        /// The ceiling stays in integer tiers because that is what merit and
        /// the rest of the mod speak in, and it is a cap rather than a
        /// measurement.
        ///
        /// A worn or offered tier below the bottom of the scale means the game
        /// scored the item beneath tier 1 -- naphtha pots do this, being
        /// consumables the value model has no opinion about. Those sit outside
        /// the 1-to-6 scale this whole engine speaks in, so they are left alone
        /// rather than traded on a comparison the scale cannot carry. Three
        /// lords lost their naphtha pots to a 141-denar javelin before this
        /// existed.
        /// </summary>
        public static bool IsUpgrade(int wornFine, bool wornOwnCulture,
                                     int offeredFine, bool offeredOwnCulture,
                                     int offeredTier, int ceiling)
        {
            if (wornFine < 1 || offeredFine < 1) return false;
            if (offeredTier < 1 || offeredTier > ceiling) return false;

            // Both sides through the same transform. Applying the preference to
            // the offer alone would let a lord churn his own culture's gear for
            // trivial gains, because every same-culture swap would start a whole
            // tier ahead -- MinimumGain would stop meaning half a tier.
            return Effective(offeredFine, offeredOwnCulture)
                 - Effective(wornFine, wornOwnCulture) >= MinimumGain;
        }

        /// <summary>
        /// Orders two offers best-first, on four terms in this order: the higher
        /// whole tier, then the one that keeps the hero's own weapon class, then
        /// the finer tier, then the cheaper. Negative when the first comes
        /// first, in the shape List.Sort expects.
        ///
        /// Rank is the item's whole tier when ranking offers for one slot, and
        /// the whole tiers gained when ranking across slots -- the same
        /// comparison serves both, since both mean "how much better".
        ///
        /// The whole tier leads, deliberately, even though the fine tier is the
        /// more accurate number. It is what creates the ties the class term
        /// needs: ranking on the fine tier alone, a sword at 4.20 and an axe at
        /// 4.35 never tie, the axe always wins, and a lord's sword quietly
        /// becomes a mace -- which is exactly what the class term exists to
        /// prevent. Coarse first, then character, then precision.
        ///
        /// Cheapest last, because taking the dearest of two items the fine tier
        /// calls equal burns a clan's purse for nothing.
        /// </summary>
        public static int Compare(int rankA, bool ownClassA, int fineA, int priceA,
                                  int rankB, bool ownClassB, int fineB, int priceB)
        {
            if (rankA != rankB) return rankB - rankA;
            if (ownClassA != ownClassB) return ownClassA ? -1 : 1;
            if (fineA != fineB) return fineB - fineA;
            return priceA - priceB;
        }
    }
}
