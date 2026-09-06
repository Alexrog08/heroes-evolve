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
        /// True when an offered tier is worth buying over what is worn.
        ///
        /// The step is a whole tier, not a better statline, and that is
        /// deliberate. Ranking by item value instead would have lords trading up
        /// by a few points every visit -- five hundred heroes churning their kit
        /// across the map, spending real gold for no visible change. A tier is
        /// the coarse unit the rest of the mod already speaks in, and it makes a
        /// purchase a rare, visible event.
        ///
        /// wornTier is 1-based like everything user-facing here; an empty slot
        /// has no tier and callers must not ask about one. Filling empty slots
        /// is the repair's job, and letting the market do it would let a lord
        /// buy his way into a role nobody planned for him.
        /// </summary>
        public static bool IsUpgrade(int wornTier, int offeredTier, int ceiling)
        {
            if (offeredTier > ceiling) return false;
            return offeredTier > wornTier;
        }

        /// <summary>
        /// Orders two offers best-first: the higher tier wins, and between equal
        /// tiers the cheaper one does. Negative when the first comes first, in
        /// the shape List.Sort expects.
        ///
        /// Cheapest-of-the-best matters because the alternative -- taking the
        /// dearest item that clears the tier -- burns a clan's purse on a
        /// difference the tier says does not exist.
        /// </summary>
        public static int Compare(int tierA, int priceA, int tierB, int priceB)
        {
            if (tierA != tierB) return tierB - tierA;
            return priceA - priceB;
        }
    }
}
