namespace HeroesEvolve.Core
{
    /// <summary>
    /// How far a piece of gear is behind the best its slot could hold, and
    /// which armour a lord should spend on first.
    ///
    /// Written because lords dressed from the extremities inward. A trip
    /// divides its money evenly between the slots and buys the biggest gain
    /// first, and an even slice buys far more tiers of glove than of coat: a
    /// good pair of gloves costs what a poor coat does. So the cheap pieces won
    /// trip after trip, and a lord could walk about in fine boots and gloves
    /// over armour two tiers worse than either.
    ///
    /// The cure is an order, not a price rule. The armour furthest behind is
    /// bought first, and nothing more than a tier ahead of it is bought at all
    /// until it catches up. The money follows without being told: fewer slots
    /// are open, so each open slot's share of the trip is larger, and the coat
    /// that kept losing to the gloves inherits the gloves' share.
    ///
    /// Armour only. It is one protection cut into five pieces, so a lord is as
    /// well dressed as its weakest part. Weapons are separate tools, each
    /// judged on its own -- a poor helmet is no reason to go without a bow --
    /// and the horse is another purchase again.
    /// </summary>
    public static class GearBalance
    {
        /// <summary>
        /// How far ahead of his poorest armour another piece may still be
        /// bought: one tier.
        ///
        /// Zero would make every piece wait on the single worst one, and a town
        /// that happened not to stock it would sell him no armour at all. One
        /// tier lets the pieces just behind the worst move with it, and the
        /// whole tier is the finest step the ceiling speaks, so nothing finer
        /// could be told apart honestly anyway.
        /// </summary>
        public const int Tolerance = 1;

        /// <summary>
        /// Whole tiers between what is worn and the best the slot could hold:
        /// the lower of the hero's ceiling and the best tier sold of that kind.
        ///
        /// The best sold matters as much as the ceiling. In the campaign this
        /// was measured on, nothing of leg or hand armour was sold above tier 4
        /// while coats, helmets and capes reached 6, so boots at 4 are the best
        /// boots there are. Counted against a tier-6 ceiling they would look
        /// two tiers behind forever and hold the coat back for nothing.
        ///
        /// Never negative: gear above either limit is simply not behind. And
        /// zero for anything the game scores below tier 1, which the market
        /// refuses to trade, so a slot that can never be bought for must never
        /// be the one everything else waits on.
        /// </summary>
        public static int Shortfall(int wornTier, int ceiling, int bestSold)
        {
            if (wornTier < 1) return 0;

            int reach = bestSold < ceiling ? bestSold : ceiling;
            int behind = reach - wornTier;
            return behind > 0 ? behind : 0;
        }

        /// <summary>
        /// Whether armour this far behind may be bought now, while the poorest
        /// piece he wears is <paramref name="worst"/> behind.
        /// </summary>
        public static bool IsDue(int shortfall, int worst)
        {
            return shortfall >= worst - Tolerance;
        }
    }
}
