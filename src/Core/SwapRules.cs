namespace HeroesEvolve.Core
{
    /// <summary>
    /// What a weapon purchase changed about a hero, read from what was sold and
    /// what replaced it.
    /// </summary>
    public enum SwapKind
    {
        /// <summary>The same class: a better spear for a spear.</summary>
        SameClass = 0,

        /// <summary>Another class of the same family: an axe for a sword. By design.</summary>
        OtherClassSameFamily = 1,

        /// <summary>
        /// Another family on paper, reached through a second way of holding the
        /// new item: a bastard sword files itself as one-handed and still
        /// replaces a two-hander. By design.
        /// </summary>
        ThroughSecondaryUsage = 2,

        /// <summary>Another family with no way of wielding it as the old one. Never by design.</summary>
        OtherFamily = 3,

        /// <summary>Between thrown weapons and everything else. Never by design.</summary>
        ThrownLineCrossed = 4
    }

    /// <summary>
    /// Sorts weapon purchases by what they did to the man who made them.
    ///
    /// Written after 160 of 1,788 purchases in one campaign had turned spearmen
    /// into javelin throwers, and nothing in the census could see it. Every
    /// count the census kept was of how much the market bought; none was of
    /// what it changed. The last two kinds below should read zero for ever. A
    /// census that shows otherwise has found the next bug of that shape, on the
    /// day it happens rather than a thousand purchases later.
    ///
    /// Thrown is decided first and from the categories alone, which come from
    /// the item type before any usage is read. That keeps it independent of the
    /// usage walk in ItemClassifier.Supports -- the very thing that was wrong
    /// last time, and so the one thing a detector must not lean on.
    /// </summary>
    public static class SwapRules
    {
        /// <summary>How many kinds there are, for sizing a tally.</summary>
        public const int KindCount = 5;

        /// <param name="sold">The category of the weapon given up.</param>
        /// <param name="bought">The category of the weapon that replaced it.</param>
        /// <param name="boughtServesSold">
        /// Whether the new weapon can be wielded as the old one through any of
        /// its usages, which is what separates a bastard sword from a mistake.
        /// </param>
        public static SwapKind Classify(WeaponCategory sold, WeaponCategory bought, bool boughtServesSold)
        {
            if (CategoryRules.CrossesThrowingLine(sold, bought)) return SwapKind.ThrownLineCrossed;
            if (sold == bought) return SwapKind.SameClass;
            if (CategoryRules.SameFamily(sold, bought)) return SwapKind.OtherClassSameFamily;
            return boughtServesSold ? SwapKind.ThroughSecondaryUsage : SwapKind.OtherFamily;
        }

        /// <summary>Whether no rule of this mod should ever produce this kind.</summary>
        public static bool IsNeverByDesign(SwapKind kind)
        {
            return kind == SwapKind.OtherFamily || kind == SwapKind.ThrownLineCrossed;
        }
    }
}
