using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class CultureChoiceTests
    {
        public static void RunAll()
        {
            // The names settings.xml uses and the census prints.
            Check.Equal("clan", CultureChoices.NameOf(CultureChoice.Clan), "the clan's culture is written clan");
            Check.Equal("hero", CultureChoices.NameOf(CultureChoice.Hero), "his own is written hero");
            Check.Equal("none", CultureChoices.NameOf(CultureChoice.None), "and no preference none");
            Check.Equal(3, CultureChoices.Count, "three choices in all");

            // settings.xml is typed by hand, so it is read forgivingly and never trusted.
            Check.True(CultureChoices.Parse("clan", CultureChoice.Hero) == CultureChoice.Clan, "clan reads as clan");
            Check.True(CultureChoices.Parse(" None ", CultureChoice.Hero) == CultureChoice.None,
                       "trimmed, whatever its case");
            Check.True(CultureChoices.Parse("HERO", CultureChoice.Clan) == CultureChoice.Hero, "HERO reads as hero");
            Check.True(CultureChoices.Parse("kingdom", CultureChoice.Hero) == CultureChoice.Hero,
                       "anything else keeps what was already in force");
            Check.True(CultureChoices.Parse("", CultureChoice.Clan) == CultureChoice.Clan, "as does an empty element");
            Check.True(CultureChoices.Parse(null, CultureChoice.None) == CultureChoice.None, "or a missing one");

            // MCM hands over the dropdown's index, listed in the enum's own order.
            Check.True(CultureChoices.FromIndex(0, CultureChoice.Hero) == CultureChoice.Clan, "the first entry is the clan's");
            Check.True(CultureChoices.FromIndex(1, CultureChoice.Clan) == CultureChoice.Hero, "the second his own");
            Check.True(CultureChoices.FromIndex(2, CultureChoice.Hero) == CultureChoice.None, "the third no preference");
            Check.True(CultureChoices.FromIndex(7, CultureChoice.Hero) == CultureChoice.Hero,
                       "an index past the list keeps what was in force");
            Check.True(CultureChoices.FromIndex(-1, CultureChoice.Clan) == CultureChoice.Clan, "as does a negative one");
        }
    }
}
