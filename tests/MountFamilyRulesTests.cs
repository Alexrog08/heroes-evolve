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

            // A lord keeps the beast he rides. The Aserai field both, and they
            // used to swap between them at market whenever the other was a tier
            // better -- keeping the saddle of the first, so camels went out in
            // horse harness and horses in camel saddles.
            Check.False(MountFamilyRules.MayRide(Camel, Horse, aserai, aserai), "an Aserai on a horse keeps to horses");
            Check.False(MountFamilyRules.MayRide(Horse, Camel, aserai, aserai), "and one on a camel keeps to camels");
            Check.True(MountFamilyRules.MayRide(Camel, Camel, aserai, aserai), "a better camel for a camel");
            Check.True(MountFamilyRules.MayRide(Horse, Horse, aserai, aserai), "and a better horse for a horse");
            Check.True(MountFamilyRules.MayRide(Horse, Horse, vlandia, vlandia), "a Vlandian keeps to horses");

            // Nobody takes up a beast his people do not ride.
            Check.False(MountFamilyRules.MayRide(Camel, Horse, vlandia, vlandia), "a Vlandian never buys a camel");
            Check.False(MountFamilyRules.MayRide(Camel, Camel, vlandia, vlandia),
                        "nor a better one for the camel he already sits on");
            Check.True(MountFamilyRules.MayRide(Horse, Camel, vlandia, vlandia), "but he may trade it for a horse");

            // His people or his house: a beast either one rides is one he may
            // keep. Neither makes him change the one he is on.
            Check.True(MountFamilyRules.MayRide(Camel, Camel, vlandia, aserai), "a Vlandian in an Aserai clan keeps his camel");
            Check.False(MountFamilyRules.MayRide(Camel, Horse, vlandia, aserai), "and does not trade a horse for one");
            Check.True(MountFamilyRules.MayRide(Camel, Camel, aserai, vlandia), "an Aserai in a Vlandian clan keeps his camel");

            // Riding nothing, any beast his people ride is open to him.
            Check.True(MountFamilyRules.MayRide(Camel, MountFamilyRules.NoFamily, aserai, aserai),
                       "a lord on foot may be offered either of his people's beasts");

            // A culture nobody can read never changes beast.
            Check.True(MountFamilyRules.MayRide(Horse, Horse, unknown, null), "an unknown people keeps the beast it rides");
            Check.False(MountFamilyRules.MayRide(Camel, Horse, unknown, null), "and never switches it");
            Check.False(MountFamilyRules.MayRide(Horse, MountFamilyRules.NoFamily, null, null),
                        "a lord riding nothing of a known family is offered nothing");
        }
    }
}
