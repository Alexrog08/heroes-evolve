using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    /// <summary>
    /// A lord's own taste between pieces of nearly the same quality.
    /// Offers arrive sorted best-first, as the market sorts them.
    /// </summary>
    public static class MarketTasteTests
    {
        public static void RunAll()
        {
            NothingAffordable();
            OnlyOne();
            TasteDecidesBetweenNearEquals();
            NeverDropsAWholeTier();
            NeverChangesWeaponClass();
            NeverMoreThanHalfATierWorse();
            OnlyWhatHeCanPay();
            TiesKeepTheMarketOrder();
            TasteIsHisOwnAndStable();
        }

        private static void NothingAffordable()
        {
            Check.Equal(-1, Choose(new[] { 5 }, new[] { true }, new[] { 507 }, new[] { 20000 }, new uint[] { 1 }, 10000),
                        "nothing he can pay for is no choice");
            Check.Equal(-1, MarketTaste.Choose(new int[0], new bool[0], new int[0], new int[0], new uint[0], 10000),
                        "and an empty shelf is none either");
        }

        private static void OnlyOne()
        {
            Check.Equal(0, Choose(new[] { 5 }, new[] { true }, new[] { 507 }, new[] { 20000 }, new uint[] { 1 }, 50000),
                        "one offer is the offer");
        }

        private static void TasteDecidesBetweenNearEquals()
        {
            // The imperial capes that started this: the plated lion head at
            // 5.07, the plain ones at 4.71, the wolf pelt at 4.64. All within
            // half a tier of the best, so his taste picks among them.
            int[] tiers = { 5, 5, 5 };
            bool[] own = { true, true, true };
            int[] fines = { 507, 471, 464 };
            int[] prices = { 20000, 14000, 9600 };

            Check.Equal(2, Choose(tiers, own, fines, prices, new uint[] { 10, 20, 30 }, 50000),
                        "the one he likes best, though it scores lowest");
            Check.Equal(1, Choose(tiers, own, fines, prices, new uint[] { 10, 30, 20 }, 50000),
                        "a different lord, a different cape");
            Check.Equal(0, Choose(tiers, own, fines, prices, new uint[] { 30, 20, 10 }, 50000),
                        "and the best scored when that is his taste");
        }

        private static void NeverDropsAWholeTier()
        {
            // 4.46 is within half a tier of 4.71, but it rounds to tier 4: the
            // whole tier leads the market's order, and taste does not undo it.
            Check.Equal(0, Choose(new[] { 5, 4 }, new[] { true, true }, new[] { 471, 446 },
                                  new[] { 14000, 5000 }, new uint[] { 1, 99 }, 50000),
                        "taste never costs a whole tier");
        }

        private static void NeverChangesWeaponClass()
        {
            // A sword-and-shield man is not talked into an axe by his taste.
            Check.Equal(0, Choose(new[] { 5, 5 }, new[] { true, false }, new[] { 480, 479 },
                                  new[] { 9000, 8000 }, new uint[] { 1, 99 }, 50000),
                        "taste never changes what he fights with");
        }

        private static void NeverMoreThanHalfATierWorse()
        {
            // Half a tier, the market's own measure of an improvement worth
            // paying for: a gap at least that big is not "nearly the same".
            Check.Equal(0, Choose(new[] { 5, 5 }, new[] { true, true }, new[] { 549, 499 },
                                  new[] { 9000, 8000 }, new uint[] { 1, 99 }, 50000),
                        "half a tier worse is a worse piece, not a matter of taste");
            Check.Equal(1, Choose(new[] { 5, 5 }, new[] { true, true }, new[] { 549, 500 },
                                  new[] { 9000, 8000 }, new uint[] { 1, 99 }, 50000),
                        "just under half a tier is still his to choose");
        }

        private static void OnlyWhatHeCanPay()
        {
            // The best he can pay for anchors the choice, as it always has; a
            // piece he likes but cannot afford is not on his list.
            int[] tiers = { 5, 5, 5 };
            bool[] own = { true, true, true };
            int[] fines = { 507, 471, 464 };
            int[] prices = { 20000, 14000, 9600 };

            Check.Equal(2, Choose(tiers, own, fines, prices, new uint[] { 99, 50, 60 }, 15000),
                        "his favourite is out of reach, so the one he likes next");
            Check.Equal(2, Choose(tiers, own, fines, prices, new uint[] { 99, 99, 1 }, 10000),
                        "and only what he can pay for at all");
        }

        private static void TiesKeepTheMarketOrder()
        {
            Check.Equal(0, Choose(new[] { 5, 5 }, new[] { true, true }, new[] { 507, 471 },
                                  new[] { 20000, 14000 }, new uint[] { 7, 7 }, 50000),
                        "liking two the same, he takes the market's first");
        }

        private static void TasteIsHisOwnAndStable()
        {
            Check.True(MarketTaste.Of("lord_1_1", "AR_lion_head_a") == MarketTaste.Of("lord_1_1", "AR_lion_head_a"),
                       "the same man likes the same piece the same every time");

            // Not a proof of spread, just that the man is part of the answer:
            // over a few lords, the favourite of two capes is not always the same.
            int lionFirst = 0;
            string[] lords = { "lord_1_1", "lord_1_3", "lord_1_5", "lord_1_7", "lord_2_1", "lord_2_3", "lord_3_1", "lord_4_1" };
            for (int i = 0; i < lords.Length; i++)
            {
                if (MarketTaste.Of(lords[i], "AR_lion_head_a") > MarketTaste.Of(lords[i], "AR_wolf_shoulder_b")) lionFirst++;
            }
            Check.True(lionFirst > 0 && lionFirst < lords.Length, "different lords prefer different capes");
        }

        private static int Choose(int[] tiers, bool[] own, int[] fines, int[] prices, uint[] tastes, int limit)
        {
            return MarketTaste.Choose(tiers, own, fines, prices, tastes, limit);
        }
    }
}
