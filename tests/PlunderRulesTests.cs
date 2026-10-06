using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class PlunderRulesTests
    {
        /// <summary>A chance as a whole percent, which is how the design reads.</summary>
        private static int Percent(int honor, int mercy, int generosity, int calculating,
                                   int roguery, int relation, PlunderRules.Kinship kinship)
        {
            return (int)(PlunderRules.Chance(false, honor, mercy, generosity, calculating,
                                             roguery, relation, kinship, 1f) * 100f + 0.5f);
        }

        public static void RunAll()
        {
            const PlunderRules.Kinship Stranger = PlunderRules.Kinship.None;

            // Bandits take everything, and nothing about them enters into it.
            Check.True(PlunderRules.Chance(true, 2, 2, 2, 2, 0, 100,
                                           PlunderRules.Kinship.Immediate, 1f) == 1f,
                       "bandits always strip, whatever the rest says");

            // --- the two ends, which the whole model is anchored on ---
            // A lord who is honourable, munificent, compassionate and cerebral
            // does not do this. Zero means zero: a model where everyone
            // eventually robs loses the only thing that reads as character.
            Check.Equal(0, Percent(2, 2, 2, 2, 300, -100, Stranger), "a paragon never robs, however provoked");

            // And a lord who is deceitful, sadistic, tightfisted and hotheaded
            // always does.
            Check.Equal(100, Percent(-2, -2, -2, -2, 0, 0, Stranger), "a brute always robs");

            // A lord of wholly unremarkable character is an unusual thief.
            // Calradia's custom is ransom, and the curve exists to say so: the
            // linear reading would have put him at one capture in two.
            int average = Percent(0, 0, 0, 0, 0, 0, Stranger);
            Check.True(average > 5 && average < 20, "an average lord robs rarely, not half the time");

            // --- each trait pulls the way its name says ---
            Check.True(Percent(-2, 0, 0, 0, 0, 0, Stranger) > average, "Deceitful robs more than average");
            Check.True(Percent(2, 0, 0, 0, 0, 0, Stranger) < average, "Honorable robs less");
            Check.True(Percent(0, 0, -2, 0, 0, 0, Stranger) > average, "Tightfisted robs more");
            Check.True(Percent(0, 0, 2, 0, 0, 0, Stranger) < average, "Munificent robs less");
            Check.True(Percent(0, -2, 0, 0, 0, 0, Stranger) > average, "Sadistic robs more");
            Check.True(Percent(0, 0, 0, -2, 0, 0, Stranger) > average, "Hotheaded robs more");
            Check.True(Percent(0, 0, 0, 2, 0, 0, Stranger) < average, "Cerebral stays his hand");

            // Honor leads, because breaking the customs of war is the act itself
            // rather than a disposition towards it. Below it, greed and cruelty
            // restrain a man equally: a merciful lord will not leave somebody
            // naked even when honour is not what guides him, and pity is reason
            // enough on its own.
            int deceitful = Percent(-2, 0, 0, 0, 0, 0, Stranger);
            int tightfisted = Percent(0, 0, -2, 0, 0, 0, Stranger);
            int sadistic = Percent(0, -2, 0, 0, 0, 0, Stranger);
            int hotheaded = Percent(0, 0, 0, -2, 0, 0, Stranger);
            Check.True(deceitful > tightfisted, "Honor outweighs Generosity");
            Check.Equal(tightfisted, sadistic, "Generosity and Mercy weigh the same");
            Check.True(sadistic > hotheaded, "and both outweigh Calculating");

            // --- roguery is skill, not desire ---
            Check.True(Percent(-1, 0, 0, 0, 300, 0, Stranger) > Percent(-1, 0, 0, 0, 0, 0, Stranger),
                       "a practised rogue robs more often");
            Check.Equal(0, Percent(2, 2, 2, 2, 300, 0, Stranger),
                        "but skill at theft does not make an honest man a thief");

            // --- standing between the two men ---
            Check.Equal(0, Percent(-2, -2, -2, -2, 0, 100, Stranger),
                        "a man he is close to is not robbed at all, whatever he is");
            Check.True(Percent(-1, 0, 0, 0, 0, -80, Stranger) > Percent(-1, 0, 0, 0, 0, 0, Stranger),
                       "and a man he hates is robbed more readily");

            // Friendship shields absolutely; enmity only aggravates. Being hated
            // does not make a man twice the thief that being liked makes him
            // none.
            int neutral = Percent(-1, -1, -1, -1, 0, 0, Stranger);
            int hated = Percent(-1, -1, -1, -1, 0, -100, Stranger);
            Check.True(hated > neutral && hated < neutral * 2, "enmity aggravates by half, not double");

            // --- blood ---
            Check.Equal(0, Percent(-2, -2, -2, -2, 300, 0, PlunderRules.Kinship.Immediate),
                        "a father does not strip his son");
            Check.True(Percent(-2, -2, -2, -2, 0, PlunderRules.FeudRelation - 10,
                               PlunderRules.Kinship.Immediate) > 0,
                       "...unless the two of them are already at war");

            int clansman = Percent(-1, -1, -1, -1, 0, 0, PlunderRules.Kinship.Clan);
            Check.True(clansman > 0 && clansman < neutral, "a clansman is a rare victim, not a safe one");

            // A feud lifts the veto without lifting the reticence.
            int feudingBrother = Percent(-2, -2, -2, -2, 0, -100, PlunderRules.Kinship.Immediate);
            int feudingStranger = Percent(-2, -2, -2, -2, 0, -100, Stranger);
            Check.True(feudingBrother < feudingStranger, "even in a feud, blood is robbed less than a stranger");

            // --- the calibration the player's dial is measured against ---
            // NormalRate is not applied inside Chance: multiplier 1.0 still
            // means the character model at full strength, which is what every
            // assertion above is written against. It is what Settings hands in
            // when the dial reads 1.00, so a normal campaign gets half.
            float full = PlunderRules.Chance(false, 0, 0, 0, 0, 0, 0, Stranger, 1f);
            float normal = PlunderRules.Chance(false, 0, 0, 0, 0, 0, 0, Stranger,
                                               PlunderRules.NormalRate);
            Check.True(System.Math.Abs(normal - full * PlunderRules.NormalRate) < 0.0001f,
                       "the dial multiplies the model rather than reshaping it");

            // The figures the store page prints, so the page cannot drift from
            // the code without a test saying so.
            Check.Equal(6, (int)(normal * 100f + 0.5f),
                        "a lord neutral in all four robs 6% of his prisoners in a normal campaign");
            Check.Equal(21, (int)(PlunderRules.Chance(false, -1, -1, -1, -1, 0, 0, Stranger,
                                                      PlunderRules.NormalRate) * 100f + 0.5f),
                        "minus one throughout, 21%");
            Check.Equal(50, (int)(PlunderRules.Chance(true, 0, 0, 0, 0, 0, 0, Stranger,
                                                      PlunderRules.NormalRate) * 100f + 0.5f),
                        "and a bandit robs half his prisoners, not all of them");

            // --- the player's one dial ---
            Check.True(PlunderRules.Chance(false, -2, -2, -2, -2, 0, -100, Stranger, 0f) == 0f,
                       "a zero multiplier switches the whole thing off");
            Check.True(PlunderRules.Chance(true, 0, 0, 0, 0, 0, 0, Stranger, 0f) == 0f,
                       "...including for bandits");

            // Nothing escapes zero-to-one, however absurd the inputs.
            bool bounded = true;
            int[] traits = { -9, -2, 0, 2, 9 };
            int[] relations = { -200, -50, 0, 100, 500 };
            int[] rogueries = { -50, 0, 150, 1000 };
            foreach (int t in traits)
            {
                foreach (int r in relations)
                {
                    foreach (int g in rogueries)
                    {
                        float value = PlunderRules.Chance(false, t, t, t, t, g, r, Stranger, 3f);
                        if (value < 0f || value > 1f) bounded = false;
                    }
                }
            }
            Check.True(bounded, "the chance is always a probability");

            ReprisalFollowsTheExecutionModel();
            TheDieIsCastOncePerPair();
            HisOwnDoingIsWhatItAlwaysWas();
            AThiefIsKnownToHonourableMen();
            OneDrawTwoBars();
        }

        /// <summary>The chance and the part of it that is his own doing, as whole percents.</summary>
        private static int Percent(int honor, int mercy, int generosity, int calculating, int relation,
                                   int prisonerHonor, float multiplier, out int own)
        {
            float ownDoing;
            float chance = PlunderRules.Chance(false, honor, mercy, generosity, calculating, 0, relation,
                                               PlunderRules.Kinship.None, prisonerHonor, multiplier,
                                               out ownDoing);
            own = (int)(ownDoing * 100f + 0.5f);
            return (int)(chance * 100f + 0.5f);
        }

        private static void HisOwnDoingIsWhatItAlwaysWas()
        {
            const PlunderRules.Kinship Stranger = PlunderRules.Kinship.None;

            // The second number is the first, for a prisoner with no name
            // against him: nothing about a captor's own doing was changed by
            // giving the function something else to add to it. Checked across
            // the whole range rather than at the figures somebody thought of.
            bool same = true;
            bool ordered = true;
            int[] traits = { -2, -1, 0, 1, 2 };
            int[] relations = { -150, -100, -60, -30, -10, 0, 30, 100 };
            PlunderRules.Kinship[] kin =
            {
                PlunderRules.Kinship.None, PlunderRules.Kinship.Clan, PlunderRules.Kinship.Immediate
            };

            foreach (int t in traits)
            {
                foreach (int r in relations)
                {
                    foreach (PlunderRules.Kinship k in kin)
                    {
                        float plain = PlunderRules.Chance(false, t, t, t, t, 120, r, k, PlunderRules.NormalRate);

                        float ownDoing;
                        float noName = PlunderRules.Chance(false, t, t, t, t, 120, r, k, 0,
                                                           PlunderRules.NormalRate, out ownDoing);
                        if (noName != plain || ownDoing != plain) same = false;

                        foreach (int p in traits)
                        {
                            float chance = PlunderRules.Chance(false, t, t, t, t, 120, r, k, p,
                                                               PlunderRules.NormalRate, out ownDoing);
                            if (ownDoing != plain) same = false;
                            if (ownDoing < 0f || ownDoing > chance || chance > 1f) ordered = false;
                        }
                    }
                }
            }

            Check.True(same, "his own doing is the chance this rule always gave, whoever the prisoner is");
            Check.True(ordered, "and is always a part of the whole, never more than it");

            // Bandits are outside all of it.
            float banditOwn;
            float bandit = PlunderRules.Chance(true, 0, 0, 0, 0, 0, -100, Stranger, -2,
                                               PlunderRules.NormalRate, out banditOwn);
            Check.True(bandit == banditOwn && bandit == 0.5f,
                       "a bandit's robbery is all his own doing, whoever the prisoner is");
        }

        private static void AThiefIsKnownToHonourableMen()
        {
            int own;

            // An honourable lord and a stranger of good name: his ordinary 3%.
            Check.Equal(3, Percent(1, 0, 0, 0, 0, 0, PlunderRules.NormalRate, out own),
                        "an honourable lord robs 3% of strangers");

            // The same lord holding a man known for a thief.
            Check.Equal(17, Percent(1, 0, 0, 0, 0, -1, PlunderRules.NormalRate, out own),
                        "and 17% of men without honour");
            Check.Equal(3, own, "none of the difference being his own doing");
            Check.Equal(31, Percent(1, 0, 0, 0, 0, -2, PlunderRules.NormalRate, out own),
                        "31% of the worst of them");

            // Justice is what honourable men do. A neutral lord is not offended
            // on principle, and a dishonourable one needs no excuse.
            Check.Equal(Percent(0, 0, 0, 0, 0, 0, PlunderRules.NormalRate, out own),
                        Percent(0, 0, 0, 0, 0, -2, PlunderRules.NormalRate, out own),
                        "a neutral captor does not care what the prisoner is known for");
            Check.Equal(Percent(-1, 0, 0, 0, 0, 0, PlunderRules.NormalRate, out own),
                        Percent(-1, 0, 0, 0, 0, -2, PlunderRules.NormalRate, out own),
                        "nor does a captor with no honour of his own");

            // A good name earns nothing extra, and a bad one is not held
            // against a friend.
            Check.Equal(Percent(1, 0, 0, 0, 0, 0, PlunderRules.NormalRate, out own),
                        Percent(1, 0, 0, 0, 0, 2, PlunderRules.NormalRate, out own),
                        "an honourable prisoner is robbed no less for it");
            Check.Equal(0, Percent(1, 0, 0, 0, 100, -2, PlunderRules.NormalRate, out own),
                        "and a friend is not stripped for his reputation");

            // Hating the thief besides adds nothing to the justice of it. His
            // own doing rises as enmity always raised it; the share his name
            // wears away is the same.
            int ownAtPeace, ownAtOdds;
            int atPeace = Percent(1, 0, 0, 0, 0, -2, 1f, out ownAtPeace);
            int atOdds = Percent(1, 0, 0, 0, -80, -2, 1f, out ownAtOdds);
            Check.True(ownAtOdds > ownAtPeace, "enmity raises his own doing, as it did");
            Check.True(atOdds > atPeace && atOdds < 100, "and justice does not turn hatred into certainty");

            // A clansman's bad name is a family matter first.
            float kinOwn;
            float kinsman = PlunderRules.Chance(false, 1, 0, 0, 0, 0, 0, PlunderRules.Kinship.Clan, -2,
                                                1f, out kinOwn);
            float outsider = PlunderRules.Chance(false, 1, 0, 0, 0, 0, 0, PlunderRules.Kinship.None, -2,
                                                 1f, out kinOwn);
            Check.True(kinsman > 0f && kinsman < outsider, "a thief of his own house is stripped less readily");
            Check.True(PlunderRules.Chance(false, 1, 0, 0, 0, 0, 0, PlunderRules.Kinship.Immediate, -2,
                                           1f, out kinOwn) == 0f,
                       "and his own son not at all");

            // The paragon again: no thief's name makes a robber of him.
            Check.Equal(0, Percent(2, 2, 2, 2, -100, -2, 1f, out own),
                        "a paragon does not rob even a thief he hates");

            Check.True(PlunderRules.Justice(1, -1) == PlunderRules.JusticePerLevel, "one level, one measure");
            Check.True(PlunderRules.Justice(2, -9) == PlunderRules.JusticePerLevel * 2,
                       "and a trait some mod has widened is clamped like every other");
        }

        private static void OneDrawTwoBars()
        {
            // Own doing 5%, chance 20%.
            Check.True(PlunderRules.Judge(0.02f, 0.05f, 0.20f) == PlunderRules.Motive.Character,
                       "under the lower bar it is his own doing");
            Check.True(PlunderRules.Judge(0.05f, 0.05f, 0.20f) == PlunderRules.Motive.Character,
                       "the bar itself included");
            Check.True(PlunderRules.Judge(0.10f, 0.05f, 0.20f) == PlunderRules.Motive.Justice,
                       "between the two only the prisoner's name explains it");
            Check.True(PlunderRules.Judge(0.20f, 0.05f, 0.20f) == PlunderRules.Motive.Justice,
                       "up to and including the upper bar");
            Check.True(PlunderRules.Judge(0.21f, 0.05f, 0.20f) == PlunderRules.Motive.None,
                       "and past it nobody is robbed");

            // No thief in front of him: the bars are one, and there is no
            // second motive.
            Check.True(PlunderRules.Judge(0.05f, 0.06f, 0.06f) == PlunderRules.Motive.Character,
                       "against an honest prisoner every robbery is his own doing");

            Check.True(PlunderRules.Judge(0.0f, 0f, 0f) == PlunderRules.Motive.None,
                       "no chance, no robbery, whatever the draw");

            // Over many draws the two motives come out in the proportions the
            // bars promise, which is the claim the pricing rests on.
            int character = 0, justice = 0, none = 0;
            for (int i = 0; i < 1000; i++)
            {
                PlunderRules.Motive motive = PlunderRules.Judge(i / 1000f, 0.0625f, 0.16f);
                if (motive == PlunderRules.Motive.Character) character++;
                else if (motive == PlunderRules.Motive.Justice) justice++;
                else none++;
            }
            Check.Equal(63, character, "his own doing as often as his character says");
            Check.Equal(98, justice, "the prisoner's name accounting for the rest of the robberies");
            Check.Equal(839, none, "and most prisoners still keeping their arms");
        }

        private static void TheDieIsCastOncePerPair()
        {
            // The whole point: asking again gives the same answer, so a lord
            // walking into his own keep for the fiftieth time re-asks rather
            // than re-rolls.
            float first = PlunderRules.Draw("lord_a", "prisoner_b", 1L);
            Check.True(first == PlunderRules.Draw("lord_a", "prisoner_b", 1L),
                       "the same pair always draws the same number");

            // A probability, and nothing else.
            bool bounded = true;
            string[] captors = { "a", "lord_vlandia_1", "CharacterObject_7937", "z" };
            string[] prisoners = { "b", "lord_battania_9", "CharacterObject_23172", "y" };
            foreach (string c in captors)
            {
                foreach (string p in prisoners)
                {
                    float draw = PlunderRules.Draw(c, p, 1L);
                    if (draw < 0f || draw >= 1f) bounded = false;
                }
            }
            Check.True(bounded, "every draw is in [0,1)");

            // Different men, different answers -- otherwise one hash would
            // decide the fate of every prisoner in the campaign at once.
            Check.False(PlunderRules.Draw("lord_a", "prisoner_b", 1L)
                        == PlunderRules.Draw("lord_a", "prisoner_c", 1L),
                        "one captor judges two prisoners differently");
            Check.False(PlunderRules.Draw("lord_a", "prisoner_b", 1L)
                        == PlunderRules.Draw("lord_c", "prisoner_b", 1L),
                        "two captors judge one prisoner differently");

            // The pair is ordered: robbing is not symmetrical.
            Check.False(PlunderRules.Draw("a", "b", 1L) == PlunderRules.Draw("b", "a", 1L),
                        "captor and prisoner are not interchangeable");

            // No name, no robbery.
            Check.True(PlunderRules.Draw(null, "b", 1L) == 1f, "a missing captor never robs");
            Check.True(PlunderRules.Draw("a", "", 1L) == 1f, "nor does a missing prisoner get robbed");

            // And the part that keeps "settled" from meaning "for ever". Each
            // spell in the cells is its own question: a lord who spared a man
            // in one captivity is not bound to spare him in the next.
            Check.False(PlunderRules.Draw("lord_a", "prisoner_b", 100L)
                        == PlunderRules.Draw("lord_a", "prisoner_b", 200L),
                        "a new captivity is a new question");
            Check.True(PlunderRules.Draw("lord_a", "prisoner_b", 100L)
                       == PlunderRules.Draw("lord_a", "prisoner_b", 100L),
                       "...and the same captivity is still the same answer");

            // Adjacent episodes must not correlate: captivities an hour apart
            // would otherwise share a fate.
            Check.False(PlunderRules.Draw("lord_a", "prisoner_b", 5000L)
                        == PlunderRules.Draw("lord_a", "prisoner_b", 5001L),
                        "one hour apart is a different draw");
        }

        private static void ReprisalFollowsTheExecutionModel()
        {
            // The game's test, and only the game's test: negative Honor.
            Check.True(PlunderRules.IsReprisal(-1), "a dishonourable man invites it");
            Check.True(PlunderRules.IsReprisal(-2), "and the worst of them the more so");
            Check.False(PlunderRules.IsReprisal(0), "a neutral man does not");
            Check.False(PlunderRules.IsReprisal(2), "and an honourable one certainly does not");

            // Half, matching DefaultExecutionRelationModel's own halving:
            // -60/-30/-10 become -30/-15/-5 when the victim had it coming.
            Check.Equal(-6, PlunderRules.AfterReprisal(-12, true), "relation halves");
            Check.Equal(-10, PlunderRules.AfterReprisal(-20, true), "the trait charge halves");

            // Never free. Halving a cost is not waiving it.
            Check.True(PlunderRules.AfterReprisal(-12, true) < 0, "a reprisal still costs");
            Check.True(PlunderRules.AfterReprisal(-1, true) == 0
                       || PlunderRules.AfterReprisal(-1, true) < 0,
                       "and rounds toward zero rather than away");

            // Unprovoked, nothing changes.
            Check.Equal(-12, PlunderRules.AfterReprisal(-12, false), "full price otherwise");
            Check.Equal(-20, PlunderRules.AfterReprisal(-20, false), "for the traits too");
        }
    }
}
