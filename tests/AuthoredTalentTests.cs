using System;
using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class AuthoredTalentTests
    {
        public static void RunAll()
        {
            // A sheet is read one field at a time. Movement says nothing on its
            // own: it follows whatever the hero fights with.
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

            // The promise. A lord whose years were taken off the sheet TaleWorlds
            // wrote him grows back onto it, and stands a surplus above it once he
            // is grown.
            int[] written = { 60, 90, 130, 150, 180, 210, 220, 250, 260 };
            for (int i = 0; i < written.Length; i++)
            {
                int best = written[i];
                int promised = (int)(best * (1f + AuthoredTalent.Surplus));
                int peak = SkillGrowth.PrimaryTarget(SkillGrowth.MatureAge, AuthoredTalent.Floor(best));

                Check.True(peak >= best, "a lord written with " + best + " gets it back: " + peak);
                Check.True(peak >= promised - 1 && peak <= promised + 1,
                           "and stands the surplus above it at sixty, no more: " + peak + " for " + best);
            }

            // Caladog alone asks for more than a sheet may have, and is held at
            // the ceiling a little short of his own 300 -- see AuthoredTalent.
            Check.True(Near(AuthoredTalent.Floor(300), AuthoredTalent.Ceiling), "his 300 is held at the ceiling");
            int caladog = SkillGrowth.PrimaryTarget(SkillGrowth.MatureAge, AuthoredTalent.Floor(300));
            Check.True(caladog > 285 && caladog < 300,
                       "so he finishes just short of what he was written with: " + caladog);

            // Before sixty he is still climbing toward it, which is the whole
            // point of taking his years off the sheet in the first place.
            float grown = AuthoredTalent.Floor(220);
            Check.True(SkillGrowth.PrimaryTarget(40f, grown) < 220, "at forty he has not arrived");
            Check.True(SkillGrowth.PrimaryTarget(18f, grown) < SkillGrowth.PrimaryTarget(40f, grown),
                       "and a youth stands lower than a man in his prime");
            Check.Equal(SkillGrowth.PrimaryTarget(SkillGrowth.MatureAge, grown),
                        SkillGrowth.PrimaryTarget(90f, grown),
                        "past sixty his sheet asks for nothing further");

            // A sheet written at the peak norm asks for barely more than the norm,
            // and the middle hero is dealt more than that: a floor speaks for the
            // unlucky rather than for everyone.
            Check.True(Near(AuthoredTalent.Floor(SkillGrowth.PeakNorm), 1f + AuthoredTalent.Surplus),
                       "the peak norm is the anchor, because it is what talent multiplies");
            Check.True(AuthoredTalent.Floor(SkillGrowth.PeakNorm) < Talent.Median,
                       "which the dice already beat at the median");
            Check.True(AuthoredTalent.Floor(90) < Talent.Median,
                       "a rookie sheet asks for less still, so a young lord is not aged twice");
            Check.True(AuthoredTalent.Floor(260) > AuthoredTalent.Floor(250), "TaleWorlds order is kept");

            // A floor, never a demotion: the dice answer whenever they are kinder.
            Check.True(AuthoredTalent.AtLeastHisSheet(1.80f, 130) == 1.80f,
                       "lucky dice stand above a modest sheet");
            Check.True(AuthoredTalent.AtLeastHisSheet(0.60f, 250) > 1.70f,
                       "and a strong sheet lifts unlucky dice");
            Check.True(AuthoredTalent.AtLeastHisSheet(Talent.Minimum, 0) == Talent.Minimum,
                       "no sheet, no floor");

            int lowered = 0, lifted = 0;
            for (int i = 0; i < 4000; i++)
            {
                float dealt = Talent.For("CharacterObject_" + i, Talent.Combat);
                float standing = AuthoredTalent.AtLeastHisSheet(dealt, 180);
                if (standing < dealt) lowered++;
                if (standing > dealt) lifted++;
            }
            Check.Equal(0, lowered, "no sheet ever lowers the man on it");
            Check.True(lifted > 0 && lifted < 4000,
                       "a knight sheet lifts the unlucky and leaves everyone else his dice");

            // Inside the talent bounds, whatever a sheet says -- and never at the
            // top of them. The last stretch belongs to the hash, so the rarest
            // prodigies a campaign deals can outgrow every king.
            Check.True(Near(AuthoredTalent.Floor(400), AuthoredTalent.Ceiling), "a sheet past the ceiling is held there");
            Check.True(AuthoredTalent.Ceiling < Talent.Maximum, "no sheet reaches the top of the hash");

            int beyondSheets = 0;
            for (int i = 0; i < 4000; i++)
            {
                if (Talent.For("CharacterObject_" + i, Talent.Combat) > AuthoredTalent.Ceiling) beyondSheets++;
            }
            Check.True(beyondSheets > 0 && beyondSheets * 50 < 4000,
                       "the hash deals a rare few more than any sheet can give, fewer than one in fifty");

            // Nothing written there means no floor, and the dice decide that
            // field on their own.
            Check.True(AuthoredTalent.Floor(0) == 0f, "a field the sheet leaves empty asks for nothing");
            Check.True(AuthoredTalent.Floor(-5) == 0f, "nor does a nonsense value");
        }

        private static bool Near(float a, float b)
        {
            return Math.Abs(a - b) < 0.001f;
        }
    }
}
