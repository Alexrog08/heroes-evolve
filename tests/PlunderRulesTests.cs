using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
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
            // rather than a disposition towards it.
            int deceitful = Percent(-2, 0, 0, 0, 0, 0, Stranger);
            int tightfisted = Percent(0, 0, -2, 0, 0, 0, Stranger);
            int sadistic = Percent(0, -2, 0, 0, 0, 0, Stranger);
            int hotheaded = Percent(0, 0, 0, -2, 0, 0, Stranger);
            Check.True(deceitful > tightfisted, "Honor outweighs Generosity");
            Check.True(tightfisted > sadistic, "Generosity outweighs Mercy");
            Check.True(sadistic > hotheaded, "and Mercy outweighs Calculating");

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
