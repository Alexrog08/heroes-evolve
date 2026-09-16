namespace HeroesEvolve.Core
{
    /// <summary>
    /// What a piece's quality is worth, in the hundredths of a tier the market
    /// rules speak.
    ///
    /// The market measured an item by the item: a Lordly cuirass and a plain one
    /// were the same tier, and the plain one cost less. Here the piece is
    /// measured as the game itself would score it with the modifier's stats
    /// counted in, and the worn piece is measured the same way as the offered
    /// one, so a damaged coat reads lower and a sound one of the same make
    /// becomes worth buying.
    ///
    /// Armour gets the game's own arithmetic, because armour is where it is
    /// simple and where it matters most. DefaultItemValueModel.CalculateArmorTier
    /// is linear -- 0.1 x a type factor x (1.2 head + body + legs + arms), less
    /// 0.4 -- and ItemModifier.ModifyArmor adds a flat amount to every part a
    /// piece covers, so the tiers a modifier is worth fall straight out of it.
    /// Material decides almost everything: measured over the shipped catalogue,
    /// a fine plate cuirass is worth 1.20 tiers and a fine cloth hood 0.14. A
    /// single figure per grade would pay the hood like the cuirass.
    ///
    /// Everything else -- weapons, shields, ammunition, mounts -- goes by grade.
    /// Their modifiers are near uniform across groups (a balanced sword, axe,
    /// mace or polearm all gain 3 damage and 1 speed), and the game's weapon
    /// formula, estimated over typical stats, puts the good grades near 0.4, 0.7
    /// and 1.0 of a tier and the damaged ones near 0.4 and 0.9 below. Three
    /// tenths a grade sits a little inside all of it.
    ///
    /// Never a whole tier either way. Quality ranks a piece within its tier; it
    /// never makes a tier 3 into a tier 4. The ceiling still reads the item's own
    /// tier, so no quality carries a piece past what a lord's merit allows.
    /// </summary>
    public static class QualityValue
    {
        /// <summary>The most a quality can be worth, up or down: short of a whole tier.</summary>
        public const int Cap = 90;

        /// <summary>For everything but armour: three tenths of a tier a grade either side of Common.</summary>
        public const int GradeStep = 30;

        /// <summary>The armour types, numbered as the game's ItemObject.ItemTypeEnum numbers them.</summary>
        public const int HeadArmorType = 14;
        public const int BodyArmorType = 15;
        public const int LegArmorType = 16;
        public const int HandArmorType = 17;
        public const int CapeType = 24;

        /// <summary>What a grade is worth on anything but armour.</summary>
        public static int ForGrade(int grade)
        {
            return Clamp((ItemGrade.Index(grade) - ItemGrade.Common) * GradeStep);
        }

        /// <summary>
        /// The weight DefaultItemValueModel.CalculateArmorTier gives an armour
        /// type: helms 1.2, boots 1.6, gloves 1.7, capes 1.8, and body armour and
        /// anything else, harnesses included, 1.
        /// </summary>
        public static float ArmorTypeFactor(int itemType)
        {
            switch (itemType)
            {
                case HeadArmorType: return 1.2f;
                case LegArmorType: return 1.6f;
                case HandArmorType: return 1.7f;
                case CapeType: return 1.8f;
                default: return 1f;
            }
        }

        /// <summary>
        /// What an armour modifier is worth on one piece, from the piece's own
        /// armour values: the tiers the game would add to its score, in
        /// hundredths, held inside the cap.
        /// </summary>
        public static int ForArmor(int itemType, int head, int body, int leg, int arm, int modifierArmor)
        {
            if (modifierArmor == 0) return 0;

            float points = 1.2f * Change(head, modifierArmor)
                         + Change(body, modifierArmor)
                         + Change(leg, modifierArmor)
                         + Change(arm, modifierArmor);

            float tiers = 0.1f * ArmorTypeFactor(itemType) * points;
            return Clamp((int)System.Math.Round(tiers * 100f));
        }

        /// <summary>
        /// A fine tier with its quality counted in.
        ///
        /// A tier the game scores below the scale stays below it: the market
        /// rules leave those alone (see MarketRules.IsUpgrade), and quality must
        /// not bring a naphtha pot into trade. One on the scale stays on it, so a
        /// damaged tier 1 is still something a lord can replace.
        /// </summary>
        public static int Apply(int fineTier, int value)
        {
            if (fineTier < 1) return fineTier;

            int valued = fineTier + value;
            return valued < 1 ? 1 : valued;
        }

        /// <summary>
        /// How one covered part changes: the game adds the modifier's armour and
        /// never takes a covered part below one point, and leaves a part the
        /// piece does not cover alone.
        /// </summary>
        private static int Change(int armor, int modifierArmor)
        {
            if (armor <= 0) return 0;

            int modified = armor + modifierArmor;
            if (modified < 1) modified = 1;
            return modified - armor;
        }

        private static int Clamp(int value)
        {
            if (value > Cap) return Cap;
            if (value < -Cap) return -Cap;
            return value;
        }
    }
}
