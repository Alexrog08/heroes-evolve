using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    /// <summary>
    /// What a robbery is charged, where the charge stops, and what a robbery
    /// in answer gives back.
    /// </summary>
    public static class RobberyCostTests
    {
        public static void RunAll()
        {
            HalfAnExecution();
            TheWideCirclesHaveAFloor();
            AGrudgeIsSpentNotReversed();
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
            Check.Equal(-15, RobberyCost.Of(-30), "a scoundrel's clan");
            Check.Equal(-7, RobberyCost.Of(-15), "a scoundrel's friends, rounded toward nothing");
            Check.Equal(-2, RobberyCost.Of(-5), "a scoundrel's kingdom, likewise");

            Check.Equal(0, RobberyCost.Of(0), "nothing halves to nothing");
            Check.Equal(0, RobberyCost.Of(-1), "and a price never rounds away from zero");
        }

        private static void TheWideCirclesHaveAFloor()
        {
            const int Floor = RobberyCost.KingdomFloor;

            Check.Equal(-5, RobberyCost.Floored(0, -5, Floor), "a first charge lands whole");
            Check.Equal(-5, RobberyCost.Floored(20, -5, Floor), "on a friendly house as on a neutral one");
            Check.Equal(-5, RobberyCost.Floored(-10, -5, Floor), "and exactly reaches the floor");
            Check.Equal(-3, RobberyCost.Floored(-12, -5, Floor), "the last charge is only what is left");
            Check.Equal(0, RobberyCost.Floored(Floor, -5, Floor), "at the floor it has said what it has to say");
            Check.Equal(0, RobberyCost.Floored(-80, -5, Floor),
                        "and a house that already hates him for its own reasons is not charged again");

            // Three robberies deep, for both circles.
            int kingdom = 0;
            for (int i = 0; i < 10; i++) kingdom += RobberyCost.Floored(kingdom, -5, RobberyCost.KingdomFloor);
            Check.Equal(RobberyCost.KingdomFloor, kingdom, "ten robberies cost a kingdom what three do");

            int friends = 0;
            for (int i = 0; i < 10; i++) friends += RobberyCost.Floored(friends, -15, RobberyCost.FriendsFloor);
            Check.Equal(RobberyCost.FriendsFloor, friends, "and a friend the same");

            // A floor is for costs. It is not a way to hand relation out.
            Check.Equal(0, RobberyCost.Floored(-80, 5, Floor), "a gain is not this function's business");
            Check.Equal(0, RobberyCost.Floored(0, 0, Floor), "and no cost is no cost");
        }

        private static void AGrudgeIsSpentNotReversed()
        {
            Check.Equal(0, RobberyCost.Settled(-30, 30), "one robbery in answer settles one robbery");
            Check.Equal(-30, RobberyCost.Settled(-60, 30), "two take two");
            Check.Equal(0, RobberyCost.Settled(-10, 30), "never past even: they are quits, not friends");
            Check.Equal(-70, RobberyCost.Settled(-100, 30), "the deepest feud is spent at the same rate");

            // Nothing between them, nothing to settle. This is the honourable
            // man stripping a known thief he has never met.
            Check.Equal(0, RobberyCost.Settled(0, 30), "a stranger stays a stranger");
            Check.Equal(40, RobberyCost.Settled(40, 30), "and a friend is not made a better one");

            Check.Equal(-30, RobberyCost.Settled(-30, 0), "nothing given back, nothing changes");
            Check.Equal(-30, RobberyCost.Settled(-30, -10), "and this is never a way to deepen one");
        }

        private static void TheTallyCountsBothWays()
        {
            RobberyTally.Reset();
            Check.Equal("offences=0 grudges=0 justice=0 bandits=0 relationSpent=0 housesTouched=0 relationSettled=0",
                        RobberyTally.Describe(), "an empty session");

            // One robbery of character against a man with a friend and seven
            // other houses in his kingdom: 30 + 15 + 7 x 5.
            RobberyTally.Offence(-80, 9);
            RobberyTally.Answer(30, false);
            RobberyTally.Answer(0, true);
            RobberyTally.Bandit();

            Check.Equal("offences=1 grudges=1 justice=1 bandits=1 relationSpent=80 housesTouched=9 relationSettled=30",
                        RobberyTally.Describe(), "spent and settled, each counted once");

            // Costs arrive negative from the game and positive from a sum of
            // magnitudes; either is the same amount of relation.
            RobberyTally.Offence(45, 2);
            Check.Equal(125, RobberyTally.RelationSpent, "a cost is a cost whichever way it is signed");

            RobberyTally.Reset();
            Check.Equal(0, RobberyTally.Offences + RobberyTally.RelationSpent + RobberyTally.RelationSettled,
                        "a new session starts from nothing");
        }
    }
}
