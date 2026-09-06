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

            // An item the game never ranked reads as tier zero. Treating that as
            // "worse than everything" had a 141-denar javelin replacing the
            // naphtha pots of three lords in a live campaign.
            Check.False(MarketRules.IsUpgrade(0, 1, 6), "an unranked item is left alone, not replaced by junk");
            Check.False(MarketRules.IsUpgrade(0, 6, 6), "...not even by something good");
            Check.False(MarketRules.IsUpgrade(-1, 4, 6), "a negative tier is unranked too");
            Check.False(MarketRules.IsUpgrade(2, 0, 6), "and an unranked offer is never bought");

            // Rank first: a real upgrade always beats a cheaper or more
            // characterful one.
            Check.True(MarketRules.Compare(4, false, 900, 3, true, 100) < 0, "the higher rank comes first");
            Check.True(MarketRules.Compare(3, true, 100, 4, false, 900) > 0, "...whichever side it is on");

            // Then his own weapon class, so a lord's sword does not quietly
            // become a mace when both are the same better tier.
            Check.True(MarketRules.Compare(4, true, 900, 4, false, 100) < 0, "his own class beats a cheaper one");
            Check.True(MarketRules.Compare(4, false, 100, 4, true, 900) > 0, "...whichever side it is on");

            // Then price. Paying more for a difference the tier says does not
            // exist just burns the clan's purse.
            Check.True(MarketRules.Compare(3, true, 100, 3, true, 900) < 0, "the cheaper of a tier comes first");
            Check.True(MarketRules.Compare(3, true, 900, 3, true, 100) > 0, "...whichever side it is on");
            Check.Equal(0, MarketRules.Compare(3, true, 100, 3, true, 100), "identical offers tie");

            // Sorting requires a consistent order, and a comparison that
            // disagrees with itself corrupts List.Sort rather than merely
            // ranking oddly. Check every pair of a small spread both ways.
            int[] ranks = { 1, 2, 3, 3, 3, 5, 6 };
            bool[] own = { true, false, true, true, false, true, false };
            int[] prices = { 50, 900, 100, 100, 100, 4000, 7 };
            bool consistent = true;
            for (int a = 0; a < ranks.Length; a++)
            {
                for (int b = 0; b < ranks.Length; b++)
                {
                    int forward = MarketRules.Compare(ranks[a], own[a], prices[a], ranks[b], own[b], prices[b]);
                    int back = MarketRules.Compare(ranks[b], own[b], prices[b], ranks[a], own[a], prices[a]);
                    if (forward == 0 && back != 0) consistent = false;
                    if (forward < 0 && back <= 0) consistent = false;
                    if (forward > 0 && back >= 0) consistent = false;
                }
            }
            Check.True(consistent, "the ordering is antisymmetric, so a sort is well defined");

            // The redundancy rule the grant and the market share: a one-handed
            // slot must not be filled by a second two-hander.
            Check.True(CategoryRules.TwoHandedPartner(WeaponCategory.OneHandedSword) == WeaponCategory.TwoHandedSword,
                       "a one-handed sword must not double a two-handed one");
            Check.True(CategoryRules.TwoHandedPartner(WeaponCategory.OneHandedAxe) == WeaponCategory.TwoHandedAxe,
                       "nor a one-handed axe");
            Check.True(CategoryRules.TwoHandedPartner(WeaponCategory.Mace) == WeaponCategory.None,
                       "maces have no partner while the enum collapses both kinds");
            Check.True(CategoryRules.TwoHandedPartner(WeaponCategory.Bow) == WeaponCategory.None,
                       "and a bow has nothing to duplicate");
        }
    }
}
