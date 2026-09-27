using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    /// <summary>
    /// The order a hero's weapons take their shares of his growth in.
    /// </summary>
    public static class WeaponRankTests
    {
        public static void RunAll()
        {
            // Most focus first, whatever slot it sits in: a spear in the third
            // slot with five focus leads a sword in the first with two.
            Check.Equal("2,0,1", Order(2, 1, 5), "the most focus leads, wherever it is carried");

            // Equal focus keeps slot order -- the tie rule. Grown alike, two
            // weapons at five would both sit at the full target for life.
            Check.Equal("0,1", Order(5, 5), "a tie goes to the higher slot");
            Check.Equal("1,0,2", Order(3, 5, 3), "and a tie below the top keeps slot order too");

            // A hero with no focus in any weapon he carries ranks exactly by
            // slot, which is the order growth used before focus decided it.
            Check.Equal("0,1,2,3", Order(0, 0, 0, 0), "no focus is slot order");

            Check.Equal("0,1,2", Order(5, 3, 1), "focus that agrees with the slots moves nothing");
            Check.Equal("3,2,1,0", Order(1, 2, 3, 4), "focus that disagrees throughout reverses them");

            // Focus orders the weapons he carries and nothing else. Each comes
            // back exactly once and no position is invented, so a weapon he
            // holds focus in but does not carry can never be handed a share: it
            // is not in the list this is asked about.
            int[] carried = WeaponRank.ByFocus(new int[] { 0, 5, 2, 5 });
            Check.Equal(4, carried.Length, "as many ranks as weapons carried, no more");
            bool[] seen = new bool[4];
            bool eachOnce = true;
            for (int i = 0; i < carried.Length; i++)
            {
                if (carried[i] < 0 || carried[i] >= 4 || seen[carried[i]]) eachOnce = false;
                else seen[carried[i]] = true;
            }
            Check.True(eachOnce, "each carried weapon exactly once");

            Check.Equal("0", Order(4), "one weapon is first");
            Check.Equal("", Order(), "no weapons, no order");
            Check.Equal(0, WeaponRank.ByFocus(null).Length, "and nothing asked about");

            // The census. Each case below is one hero, his focus listed in the
            // order he carries his weapons.
            WeaponRank.Tally tally = new WeaponRank.Tally();
            tally.Add(new int[] { 5 });        // one weapon: order cannot matter
            tally.Add(new int[] { 5, 3 });     // focus agrees with the slots
            tally.Add(new int[] { 2, 5 });     // the primary moves
            tally.Add(new int[] { 5, 1, 3 });  // reordered below a primary that stays
            tally.Add(new int[] { 4, 4, 1 });  // tied at the top: the slot picked
            tally.Add(new int[] { 0, 0 });     // tied at nothing
            tally.Add(null);                   // not a hero at all

            Check.Equal(5, tally.MultiWeapon, "a single weapon is not counted");
            Check.Equal(1, tally.PrimaryByFocus, "one primary moved");
            Check.Equal(2, tally.ReorderedByFocus, "two orders changed, at the top or below it");
            Check.Equal(2, tally.TiedToSlot, "two left the primary to the slots");
            Check.Equal(1, tally.NoFocus, "one of them for want of any focus");
            Check.Equal("multiWeapon=5 primaryByFocus=1 reorderedByFocus=2 tiedToSlot=2 noFocus=1",
                        tally.Describe(), "the census line reads the same figures");
        }

        private static string Order(params int[] focusBySlot)
        {
            int[] order = WeaponRank.ByFocus(focusBySlot);
            string[] parts = new string[order.Length];
            for (int i = 0; i < order.Length; i++) parts[i] = order[i].ToString();
            return string.Join(",", parts);
        }
    }
}
