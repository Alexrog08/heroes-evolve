using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    /// <summary>
    /// What a robbery costs, how surely it is answered, and how the census
    /// counts both.
    /// </summary>
    public static class RobberyCostTests
    {
        public static void RunAll()
        {
            WhatItCosts();
            AHouseThatIsOwedCollects();
            TheTallyCountsWhatHappened();
        }

        private static void WhatItCosts()
        {
            // Standing with the house he robbed, and half of it against a
            // scoundrel who is not his friend.
            Check.Equal(-12, RobberyCost.Standing, "a robbery costs twelve with the robbed man's house");
            Check.Equal(-6, PlunderRules.AfterReprisal(RobberyCost.Standing, true),
                        "and six when the man had it coming");

            // DefaultExecutionRelationModel's Honor figure, as shipped.
            Check.Equal(-500, RobberyCost.Of(-1000), "half an execution's Honor");
            Check.Equal(-250, PlunderRules.AfterReprisal(RobberyCost.Of(-1000), true),
                        "a quarter against a scoundrel");
            Check.Equal(-10, PlunderRules.AfterReprisal(RobberyCost.MercyXp, true),
                        "and the small mark on Mercy halves with it");

            Check.Equal(0, RobberyCost.Of(0), "nothing halves to nothing");
            Check.Equal(0, RobberyCost.Of(-1), "and a price never rounds away from zero");
        }

        private static void AHouseThatIsOwedCollects()
        {
            const PlunderRules.Kinship Stranger = PlunderRules.Kinship.None;

            // Three captures in four, whoever he is and however little he
            // cares for robbery, at the dial's normal setting.
            Check.True(PlunderRules.Vengeance(0, Stranger, 1f) == PlunderRules.VengeanceChance,
                       "a house that is owed collects three times in four");
            Check.True(PlunderRules.Vengeance(0, Stranger, 0f) == 0f,
                       "the campaign's dial still switches it off with everything else");
            Check.True(PlunderRules.Vengeance(0, Stranger, 2f) == 1f, "and at double it is certain, not more");

            // A debt is a debt: how the two houses stand does not enter into
            // it, either way.
            Check.True(PlunderRules.Vengeance(80, Stranger, 1f) == PlunderRules.Vengeance(-80, Stranger, 1f),
                       "standing neither excuses a debt nor adds to it");

            // Blood is still blood.
            Check.True(PlunderRules.Vengeance(-30, PlunderRules.Kinship.Immediate, 1f) == 0f,
                       "a man does not collect from his own son");
            Check.True(PlunderRules.Vengeance(-80, PlunderRules.Kinship.Immediate, 1f) > 0f,
                       "unless they are past that");
            Check.True(PlunderRules.Vengeance(-30, PlunderRules.Kinship.Clan, 1f)
                       < PlunderRules.Vengeance(-30, Stranger, 1f),
                       "and a clansman is collected from less readily than a stranger");

            // Owed: every robbery is vengeance, including the ones his
            // character would have done anyway.
            Check.True(PlunderRules.Judge(0.02f, 0.06f, 0.10f, 0.75f) == PlunderRules.Motive.Vengeance,
                       "a man collecting a debt is not also committing an offence");
            Check.True(PlunderRules.Judge(0.70f, 0.06f, 0.10f, 0.75f) == PlunderRules.Motive.Vengeance,
                       "vengeance reaches far past what his character would do");
            Check.True(PlunderRules.Judge(0.80f, 0.06f, 0.10f, 0.75f) == PlunderRules.Motive.None,
                       "and one capture in four the prisoner still keeps his arms");

            // A brute who is owed is no less sure than his own appetite.
            Check.True(PlunderRules.Judge(0.90f, 0.95f, 0.95f, 0.75f) == PlunderRules.Motive.Vengeance,
                       "the higher of the two bars is the one that counts");

            // Not owed: the plain reading, unchanged.
            Check.True(PlunderRules.Judge(0.02f, 0.06f, 0.10f, 0f) == PlunderRules.Motive.Character,
                       "with nothing owed his own doing is his own doing");
            Check.True(PlunderRules.Judge(0.08f, 0.06f, 0.10f, 0f) == PlunderRules.Motive.Justice,
                       "and past it only the prisoner's name is left to explain it");
        }

        private static void TheTallyCountsWhatHappened()
        {
            RobberyTally.Reset();
            Check.Equal("offences=0 oaths=0 vengeance=0 justice=0 bandits=0",
                        RobberyTally.Describe(), "an empty session");

            RobberyTally.Offence();
            RobberyTally.Oath();
            RobberyTally.Avenged();
            RobberyTally.Justified();
            RobberyTally.Bandit();
            RobberyTally.Bandit();

            Check.Equal("offences=1 oaths=1 vengeance=1 justice=1 bandits=2",
                        RobberyTally.Describe(), "each kind counted once");

            RobberyTally.Reset();
            Check.Equal(0, RobberyTally.Offences + RobberyTally.Oaths + RobberyTally.Vengeance
                           + RobberyTally.Justice + RobberyTally.Bandits,
                        "a new session starts from nothing");
        }
    }
}
