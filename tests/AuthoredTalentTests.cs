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
            int[] written = { 60, 90, 130, 150, 180, 210, 220, 250, 260, 300 };
            for (int i = 0; i < written.Length; i++)
            {
                int best = written[i];
                int promised = (int)(best * (1f + AuthoredTalent.Surplus));
                int peak = SkillGrowth.PrimaryTarget(SkillGrowth.MatureAge,
                                                     AuthoredTalent.Floor(best, SkillGrowth.PeakNorm));

                Check.True(peak >= best, "a lord written with " + best + " gets it back: " + peak);
                Check.True(peak >= promised - 1 && peak <= promised + 1,
                           "and stands the surplus above it at sixty, no more: " + peak + " for " + best);
            }

            // Caladog's 300 is the highest sheet in the game and is honoured in
            // full, so he matures past it -- and the dice still reach past him,
            // which is what keeps a prodigy possible. See Talent.Maximum.
            Check.True(AuthoredTalent.Floor(300, SkillGrowth.PeakNorm) > Talent.Level,
                       "his 300 asks for more than the talent that merely stands level with him");
            Check.True(AuthoredTalent.Floor(300, SkillGrowth.PeakNorm) < Talent.Maximum,
                       "and less than the dice can deal, so somebody may yet pass him");
            int caladog = SkillGrowth.PrimaryTarget(SkillGrowth.MatureAge,
                                                    AuthoredTalent.Floor(300, SkillGrowth.PeakNorm));
            Check.True(caladog > 300, "so he matures past what he was written with: " + caladog);

            // Before sixty he is still climbing toward it, which is the whole
            // point of taking his years off the sheet in the first place.
            float grown = AuthoredTalent.Floor(220, SkillGrowth.PeakNorm);
            Check.True(SkillGrowth.PrimaryTarget(40f, grown) < 220, "at forty he has not arrived");
            Check.True(SkillGrowth.PrimaryTarget(18f, grown) < SkillGrowth.PrimaryTarget(40f, grown),
                       "and a youth stands lower than a man in his prime");
            Check.Equal(SkillGrowth.PrimaryTarget(SkillGrowth.MatureAge, grown),
                        SkillGrowth.PrimaryTarget(90f, grown),
                        "past sixty his sheet asks for nothing further");

            // A sheet written at its field's norm asks for barely more than the
            // norm, and the middle hero is dealt more than that: a floor speaks
            // for the unlucky rather than for everyone.
            Check.True(Near(AuthoredTalent.Floor(SkillGrowth.PeakNorm, SkillGrowth.PeakNorm),
                            1f + AuthoredTalent.Surplus),
                       "the field norm is the anchor, because it is what talent multiplies");
            Check.True(AuthoredTalent.Floor(SkillGrowth.PeakNorm, SkillGrowth.PeakNorm) < Talent.Median,
                       "which the dice already beat at the median");
            Check.True(AuthoredTalent.Floor(90, SkillGrowth.PeakNorm) < Talent.Median,
                       "a rookie sheet asks for less still, so a young lord is not aged twice");
            Check.True(AuthoredTalent.Floor(260, SkillGrowth.PeakNorm)
                       > AuthoredTalent.Floor(250, SkillGrowth.PeakNorm), "TaleWorlds order is kept");

            // Each field on its own scale: the same figure says more in a ledger
            // than with a sword, because TaleWorlds wrote 300 with a sword and
            // only 250 in trade.
            Check.True(AuthoredTalent.Floor(230, SkillGrowth.CivilPeakNorm)
                       > AuthoredTalent.Floor(230, SkillGrowth.PeakNorm),
                       "230 in stewardship is nearer the top of its field than 230 with a sword");
            Check.Equal(300, Talent.TargetFor(SkillGrowth.PeakNorm, Talent.Level),
                        "the talent that stands level with Caladog finishes on his 300");
            Check.Equal(250, Talent.TargetFor(SkillGrowth.CivilPeakNorm, Talent.Level),
                        "and with Pharon in the ledger");
            Check.Equal(279, Talent.TargetFor(SkillGrowth.NavalPeakNorm, Talent.Level),
                        "and with Halthdar at sea");

            // And the dice reach past it, so a prodigy passes the best written
            // man of a field rather than tying him -- see Talent.Maximum.
            Check.True(Talent.Maximum > Talent.Level, "the dice reach past level");
            for (int f = 0; f < 3; f++)
            {
                int norm = f == 0 ? SkillGrowth.PeakNorm
                         : f == 1 ? SkillGrowth.CivilPeakNorm : SkillGrowth.NavalPeakNorm;
                int topSheet = f == 0 ? 300 : f == 1 ? 250 : 280;
                int prodigy = Talent.TargetFor(norm, Talent.Maximum);
                int bestSheet = Talent.TargetFor(norm, AuthoredTalent.Floor(topSheet, norm));
                Check.True(prodigy > bestSheet,
                           "a prodigy passes the best sheet of his field: " + prodigy + " over " + bestSheet);
                Check.True(prodigy < SkillGrowth.GameSkillMaximum,
                           "and still leaves the played hero the longest road: " + prodigy);
            }

            // A floor, never a demotion: the dice answer whenever they are kinder.
            Check.True(AuthoredTalent.AtLeastHisSheet(1.80f, 130, SkillGrowth.PeakNorm) == 1.80f,
                       "lucky dice stand above a modest sheet");
            Check.True(AuthoredTalent.AtLeastHisSheet(0.60f, 250, SkillGrowth.PeakNorm) > 1.70f,
                       "and a strong sheet lifts unlucky dice");
            Check.True(AuthoredTalent.AtLeastHisSheet(Talent.Minimum, 0, SkillGrowth.PeakNorm) == Talent.Minimum,
                       "no sheet, no floor");

            int lowered = 0, lifted = 0;
            for (int i = 0; i < 4000; i++)
            {
                float dealt = Talent.For("CharacterObject_" + i, Talent.Combat);
                float standing = AuthoredTalent.AtLeastHisSheet(dealt, 180, SkillGrowth.PeakNorm);
                if (standing < dealt) lowered++;
                if (standing > dealt) lifted++;
            }
            Check.Equal(0, lowered, "no sheet ever lowers the man on it");
            Check.True(lifted > 0 && lifted < 4000,
                       "a knight sheet lifts the unlucky and leaves everyone else his dice");

            // A sheet is held only where it asks for a figure the game could
            // never show, whatever a modded roster writes.
            Check.True(Near(AuthoredTalent.Floor(400, SkillGrowth.PeakNorm),
                            AuthoredTalent.CeilingFor(SkillGrowth.PeakNorm)),
                       "a sheet past what the game can show is held there");
            Check.Equal(SkillGrowth.GameSkillMaximum,
                        SkillGrowth.PrimaryTarget(SkillGrowth.MatureAge,
                                                  AuthoredTalent.Floor(400, SkillGrowth.PeakNorm)),
                        "and peaks on exactly what the game can show");
            Check.True(AuthoredTalent.CeilingFor(SkillGrowth.PeakNorm) > Talent.Maximum,
                       "the dice are no longer the limit on a lord TaleWorlds wrote");

            // Every sheet but Caladog's is still within reach of them: Halthdar's
            // 260 with a blade asks for 1.88, and the rarest prodigies pass it.
            Check.True(AuthoredTalent.Floor(260, SkillGrowth.PeakNorm) < Talent.Maximum,
                       "a strong sheet is not beyond the dice");
            int beyondHalthdar = 0;
            for (int i = 0; i < 4000; i++)
            {
                if (Talent.For("CharacterObject_" + i, Talent.Combat)
                    > AuthoredTalent.Floor(260, SkillGrowth.PeakNorm)) beyondHalthdar++;
            }
            Check.True(beyondHalthdar > 0 && beyondHalthdar * 5 < 4000,
                       "and a rare few of them do, fewer than one in five");

            // Every other line of the sheet is promised the same way, one skill
            // at a time -- and with no age term, because TaleWorlds wrote the
            // sheet for the man's years already and carrying it along the
            // maturity curve ages him twice.
            Check.Equal(90, AuthoredTalent.WrittenFloor(90), "a written skill is worth itself");
            Check.Equal(0, AuthoredTalent.WrittenFloor(0), "a line he was never written asks for nothing");
            Check.Equal(0, AuthoredTalent.WrittenFloor(-5), "nor a nonsense one");

            // The field promise is the one that carries the surplus, and it is
            // still there: his best skill in a field ends above his sheet.
            Check.True(SkillGrowth.PrimaryTarget(SkillGrowth.MatureAge,
                                                 AuthoredTalent.Floor(220, SkillGrowth.PeakNorm))
                       > AuthoredTalent.WrittenFloor(220),
                       "his best skill of a field still ends above the line floor");

            // Nothing written there means no floor, and the dice decide that
            // field on their own.
            Check.True(AuthoredTalent.Floor(0, SkillGrowth.PeakNorm) == 0f,
                       "a field the sheet leaves empty asks for nothing");
            Check.True(AuthoredTalent.Floor(-5, SkillGrowth.PeakNorm) == 0f, "nor does a nonsense value");
        }

        private static bool Near(float a, float b)
        {
            return Math.Abs(a - b) < 0.001f;
        }
    }
}
