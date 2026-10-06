using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    /// <summary>
    /// What a robbery is charged, who is owed for it, and what vengeance gives
    /// back.
    /// </summary>
    public static class RobberyCostTests
    {
        public static void RunAll()
        {
            HalfAnExecution();
            VengeancePaysTheStandingBack();
            TheLastOathSaysWhoIsOwed();
            AHouseThatIsOwedCollects();
            AHouseThatOwesHasNoGrudgeToActOn();
            TheTallyCountsBothWays();
        }

        private static void HalfAnExecution()
        {
            // DefaultExecutionRelationModel's own figures, as shipped: clan,
            // friend, kingdom, and the killer's Honor.
            Check.Equal(-30, RobberyCost.Of(-60), "his clan, half of sixty");
            Check.Equal(-15, RobberyCost.Of(-30), "his friends, half of thirty");
            Check.Equal(-5, RobberyCost.Of(-10), "his kingdom, half of ten");
            Check.Equal(-500, RobberyCost.Of(-1000), "and half an execution's Honor");

            // And the model's figures for a dead man without honour, which it
            // has already halved once: thirty, fifteen and five.
            Check.Equal(-7, RobberyCost.Of(-15), "a scoundrel's friends, rounded toward nothing");
            Check.Equal(-2, RobberyCost.Of(-5), "a scoundrel's kingdom, likewise");

            Check.Equal(0, RobberyCost.Of(0), "nothing halves to nothing");
            Check.Equal(0, RobberyCost.Of(-1), "and a price never rounds away from zero");
        }

        private static void VengeancePaysTheStandingBack()
        {
            Check.Equal(0, RobberyCost.Settled(-30, 30), "one answer pays for one robbery");
            Check.Equal(-30, RobberyCost.Settled(-60, 30), "two take two");
            Check.Equal(0, RobberyCost.Settled(-10, 30), "never past even: they are quits, not friends");
            Check.Equal(-70, RobberyCost.Settled(-100, 30), "the deepest feud is paid down at the same rate");

            Check.Equal(0, RobberyCost.Settled(0, 30), "nothing between them, nothing to pay back");
            Check.Equal(40, RobberyCost.Settled(40, 30), "and a friend is not made a better one");

            Check.Equal(-30, RobberyCost.Settled(-30, 0), "nothing given back, nothing changes");
            Check.Equal(-30, RobberyCost.Settled(-30, -10), "and this is never a way to deepen one");
        }

        private static void TheLastOathSaysWhoIsOwed()
        {
            const PlunderRules.Claim None = PlunderRules.Claim.None;
            const PlunderRules.Claim Owed = PlunderRules.Claim.Owed;
            const PlunderRules.Claim Owes = PlunderRules.Claim.Owes;

            // House b was robbed by house a and swore vengeance.
            Check.True(PlunderRules.ClaimFor("b", "a", "b", "a", -30) == Owed,
                       "the house that swore is owed when it holds one of theirs");
            Check.True(PlunderRules.ClaimFor("b", "a", "a", "b", -30) == Owes,
                       "and the house it swore against owes when it holds one of theirs");

            // The oath says who. The standing says whether anything is left.
            Check.True(PlunderRules.ClaimFor("b", "a", "b", "a", 0) == None,
                       "vengeance taken: the standing is back at nought and the oath is history");
            Check.True(PlunderRules.ClaimFor("b", "a", "a", "b", 0) == None,
                       "which frees the house that owed as well");
            Check.True(PlunderRules.ClaimFor("b", "a", "b", "a", 25) == None,
                       "as does a standing mended any other way");
            Check.True(PlunderRules.ClaimFor("b", "a", "b", "a", -1) == Owed,
                       "but any bad blood at all leaves it open");

            // Bad blood alone is not a debt. This is the whole reason the oath
            // is written down: two houses that hate each other over a burnt
            // village owe each other no robbery.
            Check.True(PlunderRules.ClaimFor(null, null, "b", "a", -80) == None,
                       "no oath, nothing owed, however deep the enmity");

            // Somebody else's oath is somebody else's.
            Check.True(PlunderRules.ClaimFor("b", "c", "b", "a", -30) == None,
                       "an oath against a third house says nothing about this one");
            Check.True(PlunderRules.ClaimFor("c", "a", "b", "a", -30) == None,
                       "nor does a third house's oath against this one");

            // And nobody owes himself.
            Check.True(PlunderRules.ClaimFor("a", "a", "a", "a", -30) == None, "one house is not two");
            Check.True(PlunderRules.ClaimFor("b", "a", "", "a", -30) == None, "a captor with no house claims nothing");
        }

        private static void AHouseThatIsOwedCollects()
        {
            const PlunderRules.Kinship Stranger = PlunderRules.Kinship.None;

            // Three captures in four, whoever he is and however little he
            // cares for robbery, at the dial's normal setting.
            Check.True(PlunderRules.Vengeance(-30, Stranger, 1f) == PlunderRules.VengeanceChance,
                       "a house that is owed collects three times in four");
            Check.True(PlunderRules.Vengeance(-30, Stranger, 0f) == 0f,
                       "the campaign's dial still switches it off with everything else");
            Check.True(PlunderRules.Vengeance(-30, Stranger, 2f) == 1f, "and at double it is certain, not more");

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
            Check.True(PlunderRules.Judge(0.08f, 0.06f, 0.10f, 0f) == PlunderRules.Motive.Grudge,
                       "and bad blood without a debt is only a grudge");
        }

        private static void AHouseThatOwesHasNoGrudgeToActOn()
        {
            const PlunderRules.Kinship Stranger = PlunderRules.Kinship.None;
            float own, owesOwn, noneOwn;

            float stranger = PlunderRules.Chance(false, 0, 0, 0, 0, 0, 0, Stranger, 0,
                                                 PlunderRules.Claim.None, 1f, out own);
            float badBlood = PlunderRules.Chance(false, 0, 0, 0, 0, 0, -60, Stranger, 0,
                                                 PlunderRules.Claim.None, 1f, out noneOwn);
            float owes = PlunderRules.Chance(false, 0, 0, 0, 0, 0, -60, Stranger, 0,
                                             PlunderRules.Claim.Owes, 1f, out owesOwn);

            Check.True(badBlood > stranger, "bad blood with nothing owed either way wears his restraint");
            Check.True(owes == stranger,
                       "but the house that robbed takes no courage from the bad blood it made");
            Check.True(owesOwn == own && noneOwn == own, "and what is his own doing is the same in all three");

            // His character still speaks, and so does the prisoner's name: an
            // honourable man who owes may still think a thief fair game.
            float owesJustice = PlunderRules.Chance(false, 1, 0, 0, 0, 0, -60, Stranger, -1,
                                                    PlunderRules.Claim.Owes, 1f, out own);
            float owesPlain = PlunderRules.Chance(false, 1, 0, 0, 0, 0, -60, Stranger, 0,
                                                  PlunderRules.Claim.Owes, 1f, out own);
            Check.True(owesJustice > owesPlain, "owing a house does not make its thieves honest men");

            // Being owed changes nothing here: what an owed house does is
            // Vengeance, and it is read on its own bar.
            float owed = PlunderRules.Chance(false, 0, 0, 0, 0, 0, -60, Stranger, 0,
                                             PlunderRules.Claim.Owed, 1f, out own);
            Check.True(owed == badBlood, "the chance itself does not know about vengeance");
        }

        private static void TheTallyCountsBothWays()
        {
            RobberyTally.Reset();
            Check.Equal("offences=0 oaths=0 vengeance=0 grudges=0 justice=0 bandits=0"
                        + " relationSpent=0 housesTouched=0 relationSettled=0",
                        RobberyTally.Describe(), "an empty session");

            // One robbery of character against a man with a friend and seven
            // other houses in his kingdom: 30 + 15 + 7 x 5.
            RobberyTally.Offence(-80, 9);
            RobberyTally.Oath();
            RobberyTally.Avenged(30);
            RobberyTally.Unprovoked(false);
            RobberyTally.Unprovoked(true);
            RobberyTally.Bandit();

            Check.Equal("offences=1 oaths=1 vengeance=1 grudges=1 justice=1 bandits=1"
                        + " relationSpent=80 housesTouched=9 relationSettled=30",
                        RobberyTally.Describe(), "spent and settled, each counted once");

            // Costs arrive negative from the game and positive from a sum of
            // magnitudes; either is the same amount of relation.
            RobberyTally.Offence(45, 2);
            Check.Equal(125, RobberyTally.RelationSpent, "a cost is a cost whichever way it is signed");

            // Vengeance on a standing already mended gives nothing back and is
            // still vengeance.
            RobberyTally.Avenged(0);
            Check.Equal(2, RobberyTally.Vengeance, "an answer that settles nothing is still counted");
            Check.Equal(30, RobberyTally.RelationSettled, "but adds nothing to what was given back");

            RobberyTally.Reset();
            Check.Equal(0, RobberyTally.Offences + RobberyTally.Oaths + RobberyTally.RelationSpent
                           + RobberyTally.RelationSettled,
                        "a new session starts from nothing");
        }
    }
}
