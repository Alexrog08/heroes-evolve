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

            // Only a sheet written for a station speaks for the man on it.
            Check.True(AuthoredTalent.IsLeaderTemplate("spc_swordsman_skills_ruler"), "Caladog's ruler sheet does");
            Check.True(AuthoredTalent.IsLeaderTemplate("spc_tactician_skills_ruler"),
                       "so does a ruler sheet three kings share: it was written for the crown");
            Check.True(AuthoredTalent.IsLeaderTemplate("spc_quartermaster_skills_clanleader"), "and Hurunag's clan-leader sheet");
            Check.True(!AuthoredTalent.IsLeaderTemplate("spc_knight_skills"), "an archetype twenty-two knights share does not");
            Check.True(!AuthoredTalent.IsLeaderTemplate("spc_archer_skills_rookie"),
                       "nor a rookie sheet, whose youth the curve already counts");
            Check.True(!AuthoredTalent.IsLeaderTemplate("spc_sailor_skills_viking"), "nor a War Sails archetype");
            Check.True(!AuthoredTalent.IsLeaderTemplate("lord_forest_bandits_1"), "nor a bandit chief's own sheet");
            Check.True(!AuthoredTalent.IsLeaderTemplate("spc_ruler_guard_skills"), "naming rulers somewhere is not being one");
            Check.True(AuthoredTalent.IsLeaderTemplate("MOD_WARLORD_SKILLS_RULER"), "a mod's sheet counts whatever its case");
            Check.True(!AuthoredTalent.IsLeaderTemplate(null), "no template, no author");
            Check.True(!AuthoredTalent.IsLeaderTemplate(""), "nor an empty one");

            // Intent measured against TaleWorlds' own lords, one field at a time:
            // a sheet as strong as the typical grown lord's is the typical talent
            // the hash deals, and a king stands above the map about as far as he
            // stood above the lords TaleWorlds wrote.
            Check.True(Near(AuthoredTalent.From(AuthoredTalent.TypicalCombat, Talent.Combat), Talent.Median),
                       "a sword arm like the typical grown lord's is the typical talent");
            Check.True(Near(AuthoredTalent.From(AuthoredTalent.TypicalCivil, Talent.Civil), Talent.Median),
                       "so is a head for business like his");
            Check.True(Near(AuthoredTalent.From(AuthoredTalent.TypicalNaval, Talent.Naval), Talent.Median),
                       "and seamanship like the typical War Sails lord's");
            Check.True(Reads(AuthoredTalent.From(210, Talent.Combat), 1.53f), "Lucon's 210 stands well above the map");
            Check.True(Reads(AuthoredTalent.From(220, Talent.Combat), 1.60f), "Garios's 220 higher still");
            Check.True(Reads(AuthoredTalent.From(250, Talent.Combat), 1.82f), "Derthert's 250 among its best");
            Check.True(Near(AuthoredTalent.From(300, Talent.Combat), AuthoredTalent.Ceiling), "Caladog's 300 reaches the most a sheet can give");
            Check.True(AuthoredTalent.From(300, Talent.Combat) > AuthoredTalent.From(260, Talent.Combat), "and he still stands above Halthdar");
            Check.True(AuthoredTalent.From(260, Talent.Combat) > AuthoredTalent.From(250, Talent.Combat), "TaleWorlds' order is kept");

            // Each field against its own. A civil best is the best of a dozen
            // skills and runs higher than any one weapon, so the same number
            // says less there.
            Check.True(AuthoredTalent.From(230, Talent.Civil) < AuthoredTalent.From(230, Talent.Combat),
                       "230 in stewardship is less remarkable than 230 with a sword");
            Check.True(Reads(AuthoredTalent.From(250, Talent.Civil), 1.49f), "Unqid's 250 in trade is above the typical grown lord");
            Check.True(Reads(AuthoredTalent.From(280, Talent.Naval), 1.67f), "Halthdar's 280 at sea is well above it");
            Check.True(Near(AuthoredTalent.From(180, null), Talent.Median),
                       "a field nobody named is measured as combat, as HeroTalent looks it up");

            // Inside the talent bounds, whatever a template says -- and never at
            // the top of them. The last stretch belongs to the hash, so the
            // rarest prodigies a campaign deals can outgrow every king.
            Check.True(Near(AuthoredTalent.From(60, Talent.Combat), Talent.Minimum), "a weak template is held at the least talent");
            Check.True(Near(AuthoredTalent.From(400, Talent.Civil), AuthoredTalent.Ceiling), "a sheet past the ceiling is held there, in any field");
            Check.True(AuthoredTalent.Ceiling < Talent.Maximum, "no sheet reaches the top of the hash");

            int beyondSheets = 0;
            for (int i = 0; i < 4000; i++)
            {
                if (Talent.For("CharacterObject_" + i, Talent.Combat) > AuthoredTalent.Ceiling) beyondSheets++;
            }
            Check.True(beyondSheets > 0 && beyondSheets * 50 < 4000,
                       "the hash deals a rare few more than any sheet can give, fewer than one in fifty");

            // Nothing written means no intent, and the caller keeps the hash.
            Check.True(AuthoredTalent.From(0, Talent.Combat) == 0f, "a domain the template leaves empty carries no intent");
            Check.True(AuthoredTalent.From(-5, Talent.Naval) == 0f, "nor does a nonsense value");
        }

        private static bool Near(float a, float b)
        {
            return Math.Abs(a - b) < 0.001f;
        }

        /// <summary>Whether a talent reads as this figure to two places, the way the census prints it.</summary>
        private static bool Reads(float value, float twoPlaces)
        {
            return Math.Abs(value - twoPlaces) < 0.005f;
        }
    }
}
