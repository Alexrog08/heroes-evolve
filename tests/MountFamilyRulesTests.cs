using System.Collections.Generic;
using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class MountFamilyRulesTests
    {
        private const int Horse = 1;
        private const int Camel = 2;

        public static void RunAll()
        {
            HashSet<int> aserai = new HashSet<int> { Horse, Camel };
            HashSet<int> vlandia = new HashSet<int> { Horse };
            HashSet<int> unknown = new HashSet<int>();

            // A people's own beasts are open to its lords.
            Check.True(MountFamilyRules.MayRide(Camel, Horse, aserai, aserai), "an Aserai on a horse may take a camel");
            Check.True(MountFamilyRules.MayRide(Horse, Camel, aserai, aserai), "and an Aserai on a camel a horse");
            Check.True(MountFamilyRules.MayRide(Horse, Horse, vlandia, vlandia), "a Vlandian keeps to horses");

            // Nobody takes up a beast his people do not ride.
            Check.False(MountFamilyRules.MayRide(Camel, Horse, vlandia, vlandia), "a Vlandian never buys a camel");
            Check.False(MountFamilyRules.MayRide(Camel, Camel, vlandia, vlandia),
                        "nor a better one for the camel he already sits on");
            Check.True(MountFamilyRules.MayRide(Horse, Camel, vlandia, vlandia), "but he may trade it for a horse");

            // His people or his house: either one riding camels will do.
            Check.True(MountFamilyRules.MayRide(Camel, Horse, vlandia, aserai), "a Vlandian married into an Aserai clan may");
            Check.True(MountFamilyRules.MayRide(Camel, Horse, aserai, vlandia), "and an Aserai in a Vlandian clan still may");

            // A culture nobody can read never changes beast.
            Check.True(MountFamilyRules.MayRide(Horse, Horse, unknown, null), "an unknown people keeps the beast it rides");
            Check.False(MountFamilyRules.MayRide(Camel, Horse, unknown, null), "and never switches it");
            Check.False(MountFamilyRules.MayRide(Horse, MountFamilyRules.NoFamily, null, null),
                        "a lord riding nothing of a known family is offered nothing");
        }
    }
}
