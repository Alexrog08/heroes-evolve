using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class GearBalanceTests
    {
        public static void RunAll()
        {
            // Behind is measured to the lower of the ceiling and the best sold.
            Check.Equal(4, GearBalance.Shortfall(2, 6, 6), "a tier-2 coat under a tier-6 ceiling is four behind");
            Check.Equal(2, GearBalance.Shortfall(2, 4, 6), "the ceiling binds when it is the lower");
            Check.Equal(0, GearBalance.Shortfall(4, 6, 4), "boots at the best tier sold are not behind, whatever the ceiling");
            Check.Equal(1, GearBalance.Shortfall(3, 6, 4), "and one short of the best sold is one behind");

            // Never negative, and never driven by what cannot be traded.
            Check.Equal(0, GearBalance.Shortfall(6, 4, 6), "gear above the ceiling is not behind");
            Check.Equal(0, GearBalance.Shortfall(3, 6, 0), "a kind nobody sells cannot be behind");
            Check.Equal(0, GearBalance.Shortfall(0, 6, 6), "an unranked item the market will not trade is not behind");

            // The poorest piece is always due, and so is anything within a tier.
            Check.True(GearBalance.IsDue(3, 3), "the poorest armour is always due");
            Check.True(GearBalance.IsDue(2, 3), "a piece one tier better is still due");
            Check.False(GearBalance.IsDue(1, 3), "a piece two tiers better waits");

            // Evenly dressed, nothing waits on anything.
            Check.True(GearBalance.IsDue(0, 0), "a lord at his best everywhere may still trade sideways");
            Check.True(GearBalance.IsDue(0, 1), "nothing waits on a single tier");

            // The case that prompted it: fine gloves and boots over a poor coat.
            // Ceiling 5, and nothing sold above tier 4 for hands or legs.
            int coat = GearBalance.Shortfall(2, 5, 6);
            int helmet = GearBalance.Shortfall(3, 5, 6);
            int cape = GearBalance.Shortfall(4, 5, 6);
            int gloves = GearBalance.Shortfall(4, 5, 4);
            int boots = GearBalance.Shortfall(3, 5, 4);
            int worst = Max(coat, helmet, cape, gloves, boots);

            Check.Equal(3, worst, "the coat is the poorest piece");
            Check.True(GearBalance.IsDue(coat, worst), "the coat is bought first");
            Check.True(GearBalance.IsDue(helmet, worst), "the helmet, a tier better, moves with it");
            Check.False(GearBalance.IsDue(cape, worst), "the cape waits for the coat");
            Check.False(GearBalance.IsDue(gloves, worst), "the gloves wait for the coat");
            Check.False(GearBalance.IsDue(boots, worst), "the boots wait for the coat");

            // Once the coat and helmet catch up, the rest reopen.
            coat = GearBalance.Shortfall(4, 5, 6);
            helmet = GearBalance.Shortfall(4, 5, 6);
            worst = Max(coat, helmet, cape, gloves, boots);

            Check.Equal(1, worst, "caught up, nothing is more than a tier behind");
            Check.True(GearBalance.IsDue(gloves, worst), "the gloves reopen");
            Check.True(GearBalance.IsDue(boots, worst), "the boots reopen");
            Check.True(GearBalance.IsDue(cape, worst), "the cape reopens");
        }

        private static int Max(params int[] values)
        {
            int best = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] > best) best = values[i];
            }
            return best;
        }
    }
}
