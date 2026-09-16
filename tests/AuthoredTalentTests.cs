using System;
using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class AuthoredTalentTests
    {
        public static void RunAll()
        {
            // A template's skills are read one domain at a time. Movement says
            // nothing on its own: it follows whatever the hero fights with.
            Check.Equal(Talent.Combat, AuthoredTalent.DomainOf("OneHanded"), "a sword skill speaks for combat");
            Check.Equal(Talent.Combat, AuthoredTalent.DomainOf("Throwing"), "so does throwing");
            Check.Equal(Talent.Civil, AuthoredTalent.DomainOf("Charm"), "charm speaks for civil aptitude");
            Check.Equal(Talent.Civil, AuthoredTalent.DomainOf("Steward"), "and so does stewardship");
            Check.Equal(Talent.Naval, AuthoredTalent.DomainOf("Shipmaster"), "a War Sails skill speaks for naval aptitude");
            Check.True(AuthoredTalent.DomainOf("Riding") == null, "riding says nothing alone");
            Check.True(AuthoredTalent.DomainOf("Athletics") == null, "nor does athletics");
            Check.Equal(Talent.Civil, AuthoredTalent.DomainOf("SomeModdedSkill"),
                        "a skill another mod adds counts as civil, as growth already treats it");
            Check.True(AuthoredTalent.DomainOf(null) == null, "no id, no domain");

            // Intent on the scale the curve peaks on, where the norm is 150.
            Check.True(Near(AuthoredTalent.From(150), 1.00f), "a template best of 150 is an ordinary talent");
            Check.True(Near(AuthoredTalent.From(300), 2.00f), "Caladog's 300 is a prodigy's");
            Check.True(Near(AuthoredTalent.From(210), 1.40f), "Lucon's 210 is well above the norm");
            Check.True(AuthoredTalent.From(250) > AuthoredTalent.From(230), "TaleWorlds' order is kept");

            // Inside the talent bounds, whatever a template says.
            Check.True(Near(AuthoredTalent.From(80), Talent.Minimum), "a weak template is held at the least talent");
            Check.True(Near(AuthoredTalent.From(400), Talent.Maximum), "a template past the curve is held at the most");

            // Nothing written means no intent, and the caller keeps the hash.
            Check.True(AuthoredTalent.From(0) == 0f, "a domain the template leaves empty carries no intent");
            Check.True(AuthoredTalent.From(-5) == 0f, "nor does a nonsense value");
        }

        private static bool Near(float a, float b)
        {
            return Math.Abs(a - b) < 0.001f;
        }
    }
}
