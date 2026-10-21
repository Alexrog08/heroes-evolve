namespace HeroesEvolve.Core
{
    /// <summary>
    /// A lord's own taste, between pieces of nearly the same quality.
    ///
    /// The market ranks a shelf best-first, and for years of a campaign that
    /// was the whole of the choice: every lord took the top of the list. Late
    /// in a long campaign that is everybody, because the houses are rich and
    /// the purse stops being what decides. So every lord of a people ended up
    /// in the one piece that scored highest -- the imperial lion-head cloak,
    /// worn by most of three kingdoms, was the case that showed it, and it
    /// scores highest by 0.07 of a tier over a wolf pelt nobody bought.
    ///
    /// Nobody dresses like that. Between two cloaks that are as good as each
    /// other, a man takes the one he likes. So here, among the pieces nearly
    /// as good as the best he can pay for, he takes the one he likes best.
    ///
    /// "Nearly as good" is the market's own measure and not a new one: less
    /// than half a tier worse (MarketRules.MinimumGain), the gap the market
    /// already holds too small to be worth paying for. And it never reaches
    /// past the two things the order leads on: the whole tier, and the class
    /// of weapon he fights with. Taste picks a cloak; it does not cost him a
    /// tier or turn a swordsman into an axeman.
    ///
    /// The taste itself is a stable hash of the man and the piece, so it is
    /// his and nobody else's, it is the same every time he looks, and nothing
    /// has to be saved for it: a reload asks and gets the same answer. Two
    /// lords at one shelf can like different cloaks; one lord at two shelves
    /// likes the same cloak in both.
    /// </summary>
    public static class MarketTaste
    {
        /// <summary>
        /// How much this man likes this piece: a number with no meaning but its
        /// order. FNV-1a, as everywhere else in this mod that needs a stable
        /// hash -- string.GetHashCode is not promised to survive a restart.
        /// </summary>
        public static uint Of(string heroId, string itemId)
        {
            uint hash = 2166136261u;
            string text = (heroId ?? "") + "/" + (itemId ?? "");
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }

            return hash;
        }

        /// <summary>
        /// Which offer he takes, or -1 when he can pay for none.
        ///
        /// The arrays describe the shelf in the market's order, best first:
        /// whole tier, whether it keeps his weapon class, fine tier in
        /// hundredths, price, and his taste for each. The first offer he can
        /// pay for anchors the choice, exactly as it did before taste: the
        /// others he may take instead are those he can also pay for, of the
        /// same whole tier and class, and less than half a tier below it. Of
        /// those, the one he likes most; on a tie, the market's first.
        /// </summary>
        public static int Choose(int[] tiers, bool[] ownClass, int[] fines, int[] prices, uint[] tastes, int limit)
        {
            if (tiers == null || ownClass == null || fines == null || prices == null || tastes == null) return -1;

            int count = tiers.Length;
            if (ownClass.Length < count || fines.Length < count || prices.Length < count || tastes.Length < count)
            {
                return -1;
            }

            int anchor = -1;
            for (int i = 0; i < count; i++)
            {
                if (prices[i] <= limit) { anchor = i; break; }
            }
            if (anchor < 0) return -1;

            int chosen = anchor;
            for (int i = anchor + 1; i < count; i++)
            {
                if (prices[i] > limit) continue;
                if (tiers[i] != tiers[anchor]) continue;
                if (ownClass[i] != ownClass[anchor]) continue;
                if (fines[anchor] - fines[i] >= MarketRules.MinimumGain) continue;

                if (tastes[i] > tastes[chosen]) chosen = i;
            }

            return chosen;
        }
    }
}
