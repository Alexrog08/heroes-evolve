namespace HeroesEvolve.Core
{
    /// <summary>
    /// Whose colours a lord favours when he buys: his clan's, his own, or no
    /// one's.
    ///
    /// The preference itself is MarketRules.CulturePreference, worth a whole
    /// tier. This only decides which culture it is measured against. The two
    /// differ more often than they look: a wife married into a foreign house,
    /// a lord who changed kingdoms, every companion in a clan of another people.
    /// Choosing none takes culture out of buying altogether, and the best
    /// piece wins whoever made it.
    ///
    /// Numbered in the order the options screen lists them, so the index MCM's
    /// dropdown reports is the value.
    /// </summary>
    public enum CultureChoice
    {
        Clan = 0,
        Hero = 1,
        None = 2
    }

    public static class CultureChoices
    {
        /// <summary>How many choices there are.</summary>
        public const int Count = 3;

        private static readonly string[] Names = { "clan", "hero", "none" };

        /// <summary>The name settings.xml uses and the census prints.</summary>
        public static string NameOf(CultureChoice choice)
        {
            int index = (int)choice;
            return index >= 0 && index < Count ? Names[index] : Names[(int)CultureChoice.Hero];
        }

        /// <summary>
        /// A choice as settings.xml spells it, trimmed and in any case. Anything
        /// else keeps what was already in force: the file is typed by hand, and a
        /// misspelling should cost the player his edit, not his purchases.
        /// </summary>
        public static CultureChoice Parse(string text, CultureChoice fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;

            string wanted = text.Trim();
            for (int i = 0; i < Count; i++)
            {
                if (string.Equals(Names[i], wanted, System.StringComparison.OrdinalIgnoreCase))
                {
                    return (CultureChoice)i;
                }
            }
            return fallback;
        }

        /// <summary>A choice from the dropdown's index, keeping what was in force for one out of range.</summary>
        public static CultureChoice FromIndex(int index, CultureChoice fallback)
        {
            return index >= 0 && index < Count ? (CultureChoice)index : fallback;
        }
    }
}
