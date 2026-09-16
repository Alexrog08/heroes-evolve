using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class ItemGradeTests
    {
        public static void RunAll()
        {
            // Numbered exactly as the game numbers ItemQuality, so a cast carries
            // a modifier's quality across without a table to fall out of step.
            Check.Equal(0, ItemGrade.Poor, "poor is the game's 0");
            Check.Equal(1, ItemGrade.Inferior, "inferior its 1");
            Check.Equal(2, ItemGrade.Common, "common its 2, what a piece with no modifier is");
            Check.Equal(5, ItemGrade.Legendary, "legendary its 5");
            Check.Equal(6, ItemGrade.Count, "six grades in all");

            // Damaged means made worse than the item it is: rusty, bent, worn.
            Check.True(ItemGrade.IsDamaged(ItemGrade.Poor), "a rusty sword is damaged");
            Check.True(ItemGrade.IsDamaged(ItemGrade.Inferior), "so is a bent one");
            Check.False(ItemGrade.IsDamaged(ItemGrade.Common), "a sound piece is not");
            Check.False(ItemGrade.IsDamaged(ItemGrade.Fine), "nor a balanced one");
            Check.False(ItemGrade.IsDamaged(ItemGrade.Legendary), "nor a legendary one");
            Check.True(ItemGrade.IsDamaged(-1), "a grade below the game's own is damaged too");

            // Tallies are indexed by grade, whatever a mod's modifier reports.
            Check.Equal(0, ItemGrade.Index(-3), "a grade below poor counts as poor");
            Check.Equal(ItemGrade.Legendary, ItemGrade.Index(9), "one above legendary counts as legendary");
            Check.Equal(ItemGrade.Fine, ItemGrade.Index(ItemGrade.Fine), "and every real grade as itself");

            Check.Equal("poor=1 inferior=0 common=7 fine=0 masterwork=2 legendary=0",
                        ItemGrade.Describe(new int[] { 1, 0, 7, 0, 2, 0 }),
                        "the census names every grade, zeros included");
            Check.Equal("none", ItemGrade.Describe(null), "and says so when there is nothing to name");
            Check.Equal("masterwork", ItemGrade.NameOf(ItemGrade.Masterwork), "a grade is named on its own too");
            Check.Equal("poor", ItemGrade.NameOf(-2), "a grade below the game's is named as poor");
        }
    }
}
