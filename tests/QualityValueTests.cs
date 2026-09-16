using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class QualityValueTests
    {
        public static void RunAll()
        {
            // Weapons, shields, ammunition and mounts: three tenths of a tier a grade.
            Check.Equal(0, QualityValue.ForGrade(ItemGrade.Common), "a sound piece is worth exactly its item");
            Check.Equal(30, QualityValue.ForGrade(ItemGrade.Fine), "a balanced sword three tenths of a tier more");
            Check.Equal(60, QualityValue.ForGrade(ItemGrade.Masterwork), "a masterwork six tenths");
            Check.Equal(90, QualityValue.ForGrade(ItemGrade.Legendary), "a legendary nine, short of a whole tier");
            Check.Equal(-30, QualityValue.ForGrade(ItemGrade.Inferior), "a bent one three tenths less");
            Check.Equal(-60, QualityValue.ForGrade(ItemGrade.Poor), "a rusty one six tenths less");
            Check.Equal(90, QualityValue.ForGrade(12), "a mod's grade above legendary is still short of a tier");

            // Armour weighed as the game weighs it when it scores a tier.
            Check.True(QualityValue.ArmorTypeFactor(QualityValue.HeadArmorType) == 1.2f, "helms by 1.2");
            Check.True(QualityValue.ArmorTypeFactor(QualityValue.BodyArmorType) == 1f, "body armour by 1");
            Check.True(QualityValue.ArmorTypeFactor(QualityValue.LegArmorType) == 1.6f, "boots by 1.6");
            Check.True(QualityValue.ArmorTypeFactor(QualityValue.HandArmorType) == 1.7f, "gloves by 1.7");
            Check.True(QualityValue.ArmorTypeFactor(QualityValue.CapeType) == 1.8f, "capes by 1.8");
            Check.True(QualityValue.ArmorTypeFactor(25) == 1f, "and a harness, which the game leaves unweighted, by 1");

            // A plate helm: fine adds 4 to the head, 0.1 x 1.2 x 1.2 x 4 = 0.576 of a tier.
            Check.Equal(58, QualityValue.ForArmor(QualityValue.HeadArmorType, 40, 0, 0, 0, 4), "a fine plate helm");
            Check.Equal(90, QualityValue.ForArmor(QualityValue.HeadArmorType, 40, 0, 0, 0, 8),
                        "a lordly one is worth more than a tier, and is held short of it");
            Check.Equal(14, QualityValue.ForArmor(QualityValue.HeadArmorType, 8, 0, 0, 0, 1),
                        "a tailored cloth hood is worth barely anything");

            // Every part a piece covers gains, and only those.
            Check.Equal(30, QualityValue.ForArmor(QualityValue.BodyArmorType, 0, 10, 2, 1, 1),
                        "a tailored robe gains on body, legs and arms");
            Check.Equal(10, QualityValue.ForArmor(QualityValue.BodyArmorType, 0, 10, 0, 0, 1),
                        "a vest covering the body alone gains a third of that");
            Check.Equal(51, QualityValue.ForArmor(QualityValue.HandArmorType, 0, 0, 0, 8, 3), "waxed leather gloves");
            Check.Equal(64, QualityValue.ForArmor(QualityValue.LegArmorType, 0, 0, 12, 0, 4), "fine plate boots");
            Check.Equal(36, QualityValue.ForArmor(QualityValue.CapeType, 0, 3, 0, 1, 1), "a tailored cloak");

            // Damage counts the same way, downward.
            Check.Equal(-90, QualityValue.ForArmor(QualityValue.BodyArmorType, 0, 30, 10, 8, -6),
                        "a companion's worn coat reads nearly a tier lower");
            Check.Equal(-20, QualityValue.ForArmor(QualityValue.BodyArmorType, 0, 2, 2, 1, -2),
                        "the game never takes a covered part below one point, so a thin ripped robe loses little");
            Check.Equal(0, QualityValue.ForArmor(QualityValue.BodyArmorType, 0, 10, 2, 1, 0), "no change, no value");

            // Applied to a fine tier.
            Check.Equal(430, QualityValue.Apply(400, 30), "a balanced sword of 4.00 reads 4.30");
            Check.Equal(310, QualityValue.Apply(400, -90), "a worn coat of 4.00 reads 3.10");
            Check.Equal(1, QualityValue.Apply(50, -90), "a damaged piece at the bottom of the scale stays on it");
            Check.Equal(0, QualityValue.Apply(0, 90),
                        "one the game scores below the scale stays below it, whatever its quality");

            // What that does to a purchase.
            Check.True(QualityValue.Cap < 100, "never a whole tier");
            Check.True(MarketRules.IsUpgrade(400, false, QualityValue.Apply(400, 60), false, 4, 6),
                       "the same sword in masterwork is worth buying");
            Check.False(MarketRules.IsUpgrade(400, false, QualityValue.Apply(400, 30), false, 4, 6),
                        "in a merely balanced one it is not");
            Check.True(MarketRules.IsUpgrade(QualityValue.Apply(400, -60), false, 400, false, 4, 6),
                       "a sound sword replaces the same one rusted");
            Check.False(MarketRules.IsUpgrade(QualityValue.Apply(300, 90), false, 400, false, 4, 6),
                        "a plain 4.00 is no upgrade on a legendary 3.00");
            Check.True(MarketRules.IsUpgrade(QualityValue.Apply(300, 90), false, 450, false, 4, 6),
                       "a plain 4.50 is");
            Check.False(MarketRules.IsUpgrade(300, false, QualityValue.Apply(400, 90), false, 4, 3),
                        "a legendary tier 4 is still tier 4 to a lord allowed tier 3");

            // The armour order reads damage, never quality.
            Check.Equal(4, QualityValue.OrderTier(4, 400), "a sound tier 4 is tier 4 to the armour order");
            Check.Equal(3, QualityValue.OrderTier(4, 310), "a worn coat of 4.00 is read a tier further behind");
            Check.Equal(4, QualityValue.OrderTier(4, 490), "a legendary one is never read as tier 5");
            Check.Equal(4, QualityValue.OrderTier(5, 360), "a damaged 4.50 reads as 3.60, a tier 4");
            Check.Equal(1, QualityValue.OrderTier(1, 40), "a damaged tier 1 stays on the scale");
            Check.Equal(0, QualityValue.OrderTier(0, 0), "and a piece below the scale stays below it");
        }
    }
}
