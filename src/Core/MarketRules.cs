namespace HeroesEvolve.Core
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
        ///   offered >= worn + 0.5 tier              between two foreigners, or two of his own
        ///   offered >= worn - a quarter of itself   for his own replacing a foreigner
        ///   offered >= worn + a quarter of worn     for a foreigner replacing his own
        ///
        /// So abroad he wears what he can get; at home he trades back into his
        /// own colours for the same tier, or half a tier worse; and once he is
        /// dressed as his people dress, only a very large improvement moves him
        /// out again. Nobody is stranded and the map still looks like itself.
        ///
        /// A share of the piece rather than a flat tier, which is the whole
        /// difference between this and the hundred it replaces. The flat figure
        /// was worth a whole tier at the bottom of the scale, where a lord
        /// should be free to dress himself out of whatever is on the shelf, and
        /// only a fifth of a piece at the top, where the question actually
        /// matters. The catalogue says where it matters: measured over the
        /// armour on sale, the gap between the best a culture can field and the
        /// best anybody can reaches 1.62 tiers (a Sturgian cape against a
        /// Vlandian one), 1.44 (a Nord helmet against a Sturgian) and 1.30 (a
        /// Battanian cuirass against a Khuzait). A flat tier leaves every one of
        /// those open, so a lord at his ceiling drifts into whichever culture
        /// happens to top his slot and the map slowly dresses alike.
        ///
        /// At a quarter, a foreigner must beat what he wears by 0.5 + a quarter
        /// of it: 2.25 tiers over a tier-7 piece, which closes every gap above,
        /// and 1.0 over a tier-2 one, which leaves a poorly dressed lord abroad
        /// free to dress himself. That is the shape the problem has.
        /// </summary>
        public const int CultureShare = 25;

        /// <summary>
        /// What a piece is worth on top of its own tier for being his people's
        /// work. Neutral items get nothing: they are equally acceptable to
        /// everyone, and paying them the bonus would rank a generic helm above
        /// a hero's own culture's.
        /// </summary>
        public static int Bonus(int fine)
        {
            return fine > 0 ? fine * CultureShare / 100 : 0;
        }

        /// <summary>
        /// A fine tier as this hero values it, rather than as the game scores it.
        /// </summary>
        public static int Effective(int fine, bool ownCulture)
        {
            return ownCulture ? fine + Bonus(fine) : fine;
        }

        /// <summary>
        /// The same, in whole tiers, for the coarse term the ordering leads on.
        ///
        /// Derived from the one share rather than given a second constant, so
        /// the two halves of the comparison cannot drift apart, and rounded
        /// rather than truncated so a quarter of a tier-2 piece is not nothing.
        /// </summary>
        public static int EffectiveTier(int tier, bool ownCulture)
        {
            return ownCulture ? tier + (tier * CultureShare + 50) / 100 : tier;
        }

        /// <summary>
        /// True when an offer is enough better than what is worn to be worth
        /// buying.
        ///
        /// Tiers arrive in hundredths of the game's own fractional Tierf, so a
        /// lord can trade the worst sword of a tier for the best one without
        /// waiting for the next integer step -- but only when the gap is real.
        /// Comparing raw item value instead would have five hundred heroes
        /// churning their kit for a few points at every gate. MinimumGain is
        /// what stops that, and since a trip now buys until the town runs out
        /// it is the only thing that stops it: a lord who has just bought the
        /// best helmet on the shelf is offered no other, because none of them
        /// clears this bar against what he is now wearing.
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

            // The bonus enters only where the two sides differ in culture.
            // Running both through Effective instead would scale a same-culture
            // swap by the share as well -- (offered - worn) * 1.25 -- and
            // MinimumGain would quietly stop meaning half a tier. The ordering
            // does put both sides through it, on purpose: there the question is
            // which of two gains to spend on first, and a gain in his own
            // colours is worth more.
            int gain = offeredFine - wornFine;
            if (offeredOwnCulture && !wornOwnCulture) gain += Bonus(offeredFine);
            else if (!offeredOwnCulture && wornOwnCulture) gain -= Bonus(wornFine);

            return gain >= MinimumGain;
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
