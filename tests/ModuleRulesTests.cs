using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    /// <summary>
    /// Keeping a whole gear pack out of a campaign by name.
    /// </summary>
    public static class ModuleRulesTests
    {
        public static void RunAll()
        {
            Empty();
            Naming();
            Unknown();
        }

        private static void Empty()
        {
            // The default, and the one that has to cost nothing: Any is what
            // the hot path asks before it considers reading a file.
            Check.False(ModuleRules.Any(null), "no list at all excludes nothing");
            Check.False(ModuleRules.Any(new string[0]), "nor an empty one");
            Check.True(ModuleRules.Any(new string[] { "BensUltimateArmory" }), "one name is a list");

            Check.False(ModuleRules.Refuses("BensUltimateArmory", new string[0]),
                        "a module nobody excluded is allowed");
            Check.False(ModuleRules.Refuses("BensUltimateArmory", null),
                        "and a missing list is not a refusal");
        }

        private static void Naming()
        {
            string[] excluded = new string[] { "BensUltimateArmory" };

            Check.True(ModuleRules.Refuses("BensUltimateArmory", excluded), "the named module is refused");
            Check.True(ModuleRules.Refuses("bensultimatearmory", excluded),
                       "case is not something a player should have to get right");

            // The point of the whole feature: excluding one pack must not touch
            // another. The player who asked for this runs Open Source Armory
            // and wants to keep it.
            Check.False(ModuleRules.Refuses("OpenSourceArmory", excluded),
                        "a pack he did not name stays");
            Check.False(ModuleRules.Refuses("SandBoxCore", excluded), "and so does the base game");

            string[] two = new string[] { "OneArmoury", "AnotherArmoury" };
            Check.True(ModuleRules.Refuses("AnotherArmoury", two), "a second name is read too");
            Check.False(ModuleRules.Refuses("OpenSourceArmory", two), "and still lets the rest through");
        }

        private static void Unknown()
        {
            // A third of the catalogue is made at runtime and is declared in no
            // file, every smithed blade among it. Refusing what cannot be
            // placed would delete that third the moment anything was excluded.
            Check.False(ModuleRules.Refuses(null, new string[] { "BensUltimateArmory" }),
                        "an item of unknown provenance is allowed");
            Check.False(ModuleRules.Refuses("", new string[] { "BensUltimateArmory" }),
                        "and so is one with no module at all");
        }
    }
}
