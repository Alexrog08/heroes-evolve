using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class MarketRulesTests
    {
        public static void RunAll()
        {
            // Tiers arrive in hundredths of the game's fractional Tierf, on the
            // same 1-based scale: the whole tier is round(Tierf), so 350 is
            // exactly the boundary between whole tiers 3 and 4.
            const int Step = MarketRules.MinimumGain;

            // Half a tier is the bar. Anything less is not worth a purchase.
            Check.True(MarketRules.IsUpgrade(350, false, 350 + Step, false, 4, 6), "half a tier better is worth buying");
            Check.False(MarketRules.IsUpgrade(350, false, 350 + Step - 1, false, 4, 6), "one hundredth short is not");
            Check.False(MarketRules.IsUpgrade(350, false, 350, false, 4, 6), "the same item is not an upgrade");
            Check.False(MarketRules.IsUpgrade(400, false, 350, false, 3, 6), "a worse item is never an upgrade");

            // The case the old whole-tier rule got backwards. 349 and 351 sit in
            // different whole tiers and are two hundredths apart; buying that was
            // spending real gold for nothing.
            Check.False(MarketRules.IsUpgrade(349, false, 351, false, 4, 6), "straddling a rounding boundary is not an upgrade");

            // ...and the case it refused. Both of these are whole tier 3
            // (round(2.75) and round(3.30)), and the gap between them is larger
            // than the one above that it allowed.
            Check.True(MarketRules.IsUpgrade(275, false, 330, false, 3, 6), "a real gain inside one whole tier is an upgrade");

            // The ceiling is a purchase cap in whole tiers, and it outranks the
            // gain: a lord who has outgrown his merit stops buying.
            Check.False(MarketRules.IsUpgrade(300, false, 500, false, 5, 4), "nothing above the ceiling is bought");
            Check.True(MarketRules.IsUpgrade(300, false, 400, false, 4, 4), "the ceiling itself is reachable");

            // A ceiling below what he wears leaves him alone rather than
            // downgrading him: the mod never takes gear off a lord.
            Check.False(MarketRules.IsUpgrade(500, false, 300, false, 3, 3), "a fallen ceiling does not strip a lord");

            // Below the bottom of the scale is outside the model. Naphtha pots
            // score there, and reading them as "worse than everything" had a
            // 141-denar javelin replacing three lords' pots in a live campaign.
            Check.False(MarketRules.IsUpgrade(0, false, 400, false, 4, 6), "an item below the scale is left alone");
            Check.False(MarketRules.IsUpgrade(0, false, 600, false, 6, 6), "...not even for something good");
            Check.False(MarketRules.IsUpgrade(300, false, 0, false, 1, 6), "and nothing below the scale is ever bought");
            Check.False(MarketRules.IsUpgrade(300, false, 400, false, 0, 6), "nor anything whose whole tier is below one");

            // --- culture as a preference, not a wall ---
            // The wall came down because armour is always culture-stamped: two
            // of 2,332 pieces in the shipped catalogue carry no culture, so a
            // lord abroad could buy a sword and never a cuirass. See
            // MarketRules.CultureShare.

            // Abroad, nothing changes. He is held to the ordinary bar.
            Check.True(MarketRules.IsUpgrade(400, false, 450, false, 5, 6),
                       "a foreigner still needs half a tier");
            Check.False(MarketRules.IsUpgrade(400, false, 440, false, 5, 6),
                        "and does not get it for less");

            // At home his own colours are worth the swap at the same tier...
            Check.True(MarketRules.IsUpgrade(400, false, 400, true, 4, 6),
                       "his own culture at the same tier is worth the swap");

            // ...and at a quarter of itself worse, which is the whole point: a
            // lord trades a fine foreign helm for a plainer one of his people.
            Check.True(MarketRules.IsUpgrade(400, false, 360, true, 4, 6),
                       "two fifths of a tier worse in his own colours still is");
            Check.False(MarketRules.IsUpgrade(400, false, 359, true, 4, 6),
                        "one hundredth further is not");

            // Once dressed as his people dress, it takes a great deal to move
            // him out again -- and more the better he is dressed, which is what
            // the share buys over the flat tier it replaced.
            Check.False(MarketRules.IsUpgrade(400, true, 500, false, 5, 6),
                        "a whole tier of foreign gear does not tempt him");
            Check.True(MarketRules.IsUpgrade(400, true, 550, false, 6, 6),
                       "a tier and a half does");
            Check.False(MarketRules.IsUpgrade(700, true, 900, false, 6, 6),
                        "at the top of the scale two tiers is not enough");
            Check.True(MarketRules.IsUpgrade(700, true, 925, false, 6, 6),
                       "and it takes two and a quarter, which no culture gap in the game reaches");
            Check.True(MarketRules.IsUpgrade(200, true, 300, false, 3, 6),
                       "while a poorly dressed lord abroad still dresses himself at one tier");

            // Symmetry, which is what keeps MinimumGain meaning half a tier.
            // Applied to the offer alone, every same-culture swap would start a
            // whole tier ahead and lords would churn their own kit for nothing.
            Check.False(MarketRules.IsUpgrade(400, true, 420, true, 5, 6),
                        "own-to-own still needs half a tier");
            Check.True(MarketRules.IsUpgrade(400, true, 450, true, 5, 6),
                       "and gets it at half a tier, exactly as before");

            // A neutral item belongs to nobody and gets no thumb on the scale.
            Check.Equal(400, MarketRules.Effective(400, false), "a neutral item is valued as scored");
            Check.Equal(400 + MarketRules.Bonus(400), MarketRules.Effective(400, true),
                        "his own is valued higher");
            Check.Equal(100, MarketRules.Bonus(400), "by a quarter of what it is worth");
            Check.Equal(0, MarketRules.Bonus(0), "and nothing is worth nothing");
            Check.Equal(5, MarketRules.EffectiveTier(4, true),
                        "a whole rank higher in the middle of the scale");
            Check.Equal(8, MarketRules.EffectiveTier(6, true), "two at the top of it");
            Check.Equal(4, MarketRules.EffectiveTier(4, false), "a foreign one is not");

            // --- ordering ---
            // Whole tier first: a real step up beats a cheaper or more
            // characterful offer.
            Check.True(MarketRules.Compare(4, false, 420, 900, 3, true, 380, 100) < 0,
                       "the higher whole tier comes first");
            Check.True(MarketRules.Compare(3, true, 380, 100, 4, false, 420, 900) > 0,
                       "...whichever side it is on");

            // Then his own weapon class. This is why the whole tier leads and
            // not the fine one: ranking on hundredths alone, a sword at 4.20 and
            // an axe at 4.35 never tie, the axe always wins, and a lord's sword
            // quietly becomes a mace.
            Check.True(MarketRules.Compare(4, true, 420, 900, 4, false, 435, 100) < 0,
                       "his own class beats a finer, cheaper one of the same tier");
            Check.True(MarketRules.Compare(4, false, 435, 100, 4, true, 420, 900) > 0,
                       "...whichever side it is on");

            // Then precision, among offers alike in tier and class.
            Check.True(MarketRules.Compare(4, true, 435, 900, 4, true, 420, 100) < 0,
                       "the finer of two same-class offers comes first");

            // Then price, when even the fine tier calls them equal.
            Check.True(MarketRules.Compare(3, true, 320, 100, 3, true, 320, 900) < 0,
                       "the cheaper of two identical offers comes first");
            Check.Equal(0, MarketRules.Compare(3, true, 320, 100, 3, true, 320, 100), "identical offers tie");

            // Sorting requires a consistent order, and a comparison that
            // disagrees with itself corrupts List.Sort rather than merely
            // ranking oddly. Check every pair of a small spread both ways.
            int[] ranks = { 1, 2, 3, 3, 3, 3, 5, 6 };
            bool[] own = { true, false, true, true, false, false, true, false };
            int[] fine = { 120, 240, 280, 280, 280, 320, 470, 610 };
            int[] prices = { 50, 900, 100, 100, 100, 700, 4000, 7 };
            bool consistent = true;
            for (int a = 0; a < ranks.Length; a++)
            {
                for (int b = 0; b < ranks.Length; b++)
                {
                    int forward = MarketRules.Compare(ranks[a], own[a], fine[a], prices[a],
                                                      ranks[b], own[b], fine[b], prices[b]);
                    int back = MarketRules.Compare(ranks[b], own[b], fine[b], prices[b],
                                                   ranks[a], own[a], fine[a], prices[a]);
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
            Check.True(CategoryRules.TwoHandedPartner(WeaponCategory.OneHandedMace) == WeaponCategory.TwoHandedMace,
                       "nor a one-handed mace, now that the enum can say which is which");
            Check.True(CategoryRules.TwoHandedPartner(WeaponCategory.Bow) == WeaponCategory.None,
                       "and a bow has nothing to duplicate");
        }
    }
}
