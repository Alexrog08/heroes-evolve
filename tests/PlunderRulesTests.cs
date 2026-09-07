using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class PlunderRulesTests
    {
        public static void RunAll()
        {
            const float One = 1.0f;

            // Bandits take everything, and no trait, relation or kinship of
            // theirs enters into it -- there is nobody to appeal to.
            Check.True(PlunderRules.Chance(true, 2, 2, 2, 0, 100, PlunderRules.Kinship.Immediate, One) == 1f,
                       "bandits always strip, whatever the rest says");

            // The one absolute among lords.
            Check.True(PlunderRules.Chance(false, -2, 1, 1, 300, -100, PlunderRules.Kinship.None, One) == 0f,
                       "a merciful and generous lord never robs a prisoner");

            // Blood is a veto, not a weight -- until the relation says the blood
            // has already failed.
            Check.True(PlunderRules.Chance(false, -2, -2, -2, 300, 0, PlunderRules.Kinship.Immediate, One) == 0f,
                       "a father does not strip his son");
            Check.True(PlunderRules.Chance(false, -2, -2, -2, 300, PlunderRules.FeudRelation - 1,
                                           PlunderRules.Kinship.Immediate, One) > 0f,
                       "...but a father who hates him will");

            // Distant family is penalised without being spared.
            float stranger = PlunderRules.Chance(false, -1, -1, -1, 100, 0, PlunderRules.Kinship.None, One);
            float cousin = PlunderRules.Chance(false, -1, -1, -1, 100, 0, PlunderRules.Kinship.Distant, One);
            Check.True(cousin > 0f && cousin < stranger, "a cousin is less likely to be robbed, but not safe");

            // Character moves it in the direction the traits say.
            float honourable = PlunderRules.Chance(false, 2, 0, 0, 0, 0, PlunderRules.Kinship.None, One);
            float ordinary = PlunderRules.Chance(false, 0, 0, 0, 0, 0, PlunderRules.Kinship.None, One);
            float deceitful = PlunderRules.Chance(false, -2, 0, 0, 0, 0, PlunderRules.Kinship.None, One);
            Check.True(honourable < ordinary, "honour resists");
            Check.True(deceitful > ordinary, "and dishonour indulges");

            // Standing between the two men outweighs a great deal.
            float friendly = PlunderRules.Chance(false, -2, 0, 0, 0, 60, PlunderRules.Kinship.None, One);
            float hostile = PlunderRules.Chance(false, -2, 0, 0, 0, -60, PlunderRules.Kinship.None, One);
            Check.True(friendly < hostile, "goodwill protects a prisoner and a grudge exposes him");

            // Roguery is knowing how, not wanting to: it adds, gently.
            float unskilled = PlunderRules.Chance(false, 0, 0, 0, 0, 0, PlunderRules.Kinship.None, One);
            float rogue = PlunderRules.Chance(false, 0, 0, 0, 200, 0, PlunderRules.Kinship.None, One);
            Check.True(rogue > unskilled, "a practised rogue is likelier");

            // The multiplier is the one dial a player has, and it must reach
            // both ends: off entirely, and as often as the rules ever allow.
            Check.True(PlunderRules.Chance(false, -2, -2, -2, 300, -100, PlunderRules.Kinship.None, 0f) == 0f,
                       "a zero multiplier switches the whole thing off");
            Check.True(PlunderRules.Chance(true, 0, 0, 0, 0, 0, PlunderRules.Kinship.None, 0f) == 0f,
                       "...including for bandits");

            // Nothing ever escapes zero-to-one, however absurd the inputs. A
            // trait a mod has widened must not silently produce a certainty.
            bool bounded = true;
            int[] traits = { -9, -2, 0, 2, 9 };
            int[] relations = { -200, -40, 0, 100 };
            int[] rogueries = { -50, 0, 150, 1000 };
            foreach (int t in traits)
            {
                foreach (int r in relations)
                {
                    foreach (int g in rogueries)
                    {
                        float value = PlunderRules.Chance(false, t, t, t, g, r, PlunderRules.Kinship.None, 3f);
                        if (value < 0f || value > 1f) bounded = false;
                    }
                }
            }
            Check.True(bounded, "the chance is always a probability");
        }
    }
}
