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
        ///
        /// A tier below 1 on either side means the game never ranked that item,
        /// and an unranked item is left alone rather than assumed to be the
        /// worst thing in the world. A campaign caught this: three lords
        /// carrying naphtha pots -- which the game does not tier -- read as tier
        /// zero, so a 141-denar tier-1 javelin counted as an upgrade and
        /// replaced them. The rule the whole mod rests on is that gear is only
        /// taken off a lord when the replacement is provably better, and
        /// "provably" cannot survive comparing against a number that is not
        /// there.
        /// </summary>
        public static bool IsUpgrade(int wornTier, int offeredTier, int ceiling)
        {
            if (wornTier < 1 || offeredTier < 1) return false;
            if (offeredTier > ceiling) return false;
            return offeredTier > wornTier;
        }

        /// <summary>
        /// Orders two offers best-first, on three terms in this order: the
        /// higher rank, then the one that keeps the hero's own weapon class,
        /// then the cheaper. Negative when the first comes first, in the shape
        /// List.Sort expects.
        ///
        /// Rank is the item's tier when ranking offers for one slot, and the
        /// tier gained when ranking across slots -- the same comparison serves
        /// both, since both mean "how much better".
        ///
        /// The class term is what stops a lord's sword quietly becoming a mace.
        /// The catalogue matches a whole weapon family, because a culture may
        /// not stock the exact class a hero carries, and the tier gate already
        /// forbids sidegrades -- so a swap can only happen on a real upgrade. It
        /// still leaves one case: two items of the same better tier, one his own
        /// class and one not. Preferring his own costs nothing and keeps the
        /// character the repair gave him.
        ///
        /// Cheapest last, because taking the dearest item that clears the tier
        /// burns a clan's purse on a difference the tier says does not exist.
        /// </summary>
        public static int Compare(int rankA, bool ownClassA, int priceA,
                                  int rankB, bool ownClassB, int priceB)
        {
            if (rankA != rankB) return rankB - rankA;
            if (ownClassA != ownClassB) return ownClassA ? -1 : 1;
            return priceA - priceB;
        }
    }
}
