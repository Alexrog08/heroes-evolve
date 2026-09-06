using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class MarketRulesTests
    {
        public static void RunAll()
        {
            // A whole tier, or nothing. Anything softer turns five hundred lords
            // into shoppers who churn their kit at every gate.
            Check.True(MarketRules.IsUpgrade(2, 3, 6), "a tier better is worth buying");
            Check.False(MarketRules.IsUpgrade(3, 3, 6), "the same tier is not an upgrade");
            Check.False(MarketRules.IsUpgrade(4, 3, 6), "a worse item is never an upgrade");

            // The ceiling is the purchase cap. It outranks the upgrade: a lord
            // who has outgrown his merit stops buying.
            Check.False(MarketRules.IsUpgrade(2, 4, 3), "nothing above the ceiling is bought");
            Check.True(MarketRules.IsUpgrade(2, 3, 3), "the ceiling itself is reachable");
            Check.False(MarketRules.IsUpgrade(6, 6, 6), "a lord at his ceiling is done");

            // A ceiling below what he already wears leaves him alone rather than
            // downgrading him: the mod never takes gear off a lord.
            Check.False(MarketRules.IsUpgrade(5, 3, 3), "a fallen ceiling does not strip a lord");

            // Ordering: the better tier first, and between equals the cheaper.
            // Buying the dearest item of a tier burns a clan's purse on a
            // difference the tier says does not exist.
            Check.True(MarketRules.Compare(4, 900, 3, 100) < 0, "the higher tier comes first");
            Check.True(MarketRules.Compare(3, 100, 4, 900) > 0, "...whichever side it is on");
            Check.True(MarketRules.Compare(3, 100, 3, 900) < 0, "the cheaper of a tier comes first");
            Check.True(MarketRules.Compare(3, 900, 3, 100) > 0, "...whichever side it is on");
            Check.Equal(0, MarketRules.Compare(3, 100, 3, 100), "identical offers tie");

            // Sorting requires a consistent order, and a comparison that
            // disagrees with itself corrupts List.Sort rather than merely
            // ranking oddly. Check every pair of a small spread both ways.
            int[] tiers = { 1, 2, 3, 3, 5, 6 };
            int[] prices = { 50, 900, 100, 100, 4000, 7 };
            bool consistent = true;
            for (int a = 0; a < tiers.Length; a++)
            {
                for (int b = 0; b < tiers.Length; b++)
                {
                    int forward = MarketRules.Compare(tiers[a], prices[a], tiers[b], prices[b]);
                    int back = MarketRules.Compare(tiers[b], prices[b], tiers[a], prices[a]);
                    if (forward == 0 && back != 0) consistent = false;
                    if (forward < 0 && back <= 0) consistent = false;
                    if (forward > 0 && back >= 0) consistent = false;
                }
            }
            Check.True(consistent, "the ordering is antisymmetric, so a sort is well defined");
        }
    }
}
