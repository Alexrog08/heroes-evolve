namespace HeroesEvolve.Core
{
    /// <summary>
    /// What a robbery costs the man who did it, apart from the vengeance sworn
    /// on him for it (VengeanceOaths).
    ///
    /// Two prices, and only one of them is new. His house loses standing with
    /// the house he robbed, much as it always has. And the player, who alone
    /// has a name that moves, loses half of what the game takes from it for
    /// an execution (ExecutionHonor).
    ///
    /// Both are halved against a prisoner without honour who is not the
    /// robber's friend, which is the game's own discount for the same case
    /// (PlunderRules.IsReprisal).
    /// </summary>
    public static class RobberyCost
    {
        /// <summary>
        /// Standing with the robbed man's house.
        ///
        /// Twelve, which is what the player paid before any of this. An AI
        /// lord paid ten, and paid it whoever the prisoner was, for no reason
        /// anybody wrote down; one figure and one discount serve both now
        /// (PlunderRules.AfterReprisal). For an AI lord it comes to much what
        /// it was: about one prisoner in five has no honour, and twelve four
        /// times in five with six the fifth is 10.8.
        ///
        /// For two versions that never shipped it was a great deal more:
        /// thirty with his clan, fifteen with each of his friends and five
        /// with every house of his kingdom, half the game's price for an
        /// execution. That was built to make the people a robbery wrongs
        /// likelier to rob the robber, when the standing between two houses
        /// was the only thing there was to carry a grievance. The oath
        /// carries it now, exactly and in one direction, and the wide charge
        /// had a price of its own: simulated over thirteen years it doubled
        /// the pairs of houses at minus thirty or worse, where this leaves
        /// the map within a tenth of a point of where the old ten left it.
        /// </summary>
        public const int Standing = -12;

        /// <summary>A robbery against an execution, for what it does to a name.</summary>
        public const int ExecutionDivisor = 2;

        /// <summary>
        /// The Honor the game takes from the player for executing a lord.
        ///
        /// Written here because the game no longer says it anywhere it can be
        /// read. Until v1.4.8 it was ExecutionRelationModel's
        /// PlayerExecutingHeroHonorPenalty, read while the campaign ran so a
        /// mod that repriced executions repriced robbery with them. v1.5.7
        /// removed the model, and the same thousand is now a literal inside
        /// TraitLevelingHelper.OnBloodFeudStarted, charged only when the
        /// executed man's Honor is not negative.
        /// </summary>
        public const int ExecutionHonor = -1000;

        /// <summary>
        /// What the act leaves on the robber's Mercy. Unchanged from when it
        /// was charged through TraitLevelingHelper.OnHostileAction, twenty to
        /// Honor and Mercy alike: Honor now pays half an execution, and until
        /// v1.5.7 the game charged an execution to Honor alone, so Mercy keeps
        /// the small mark it always took for a small cruelty.
        /// </summary>
        public const int MercyXp = -20;

        /// <summary>A robbery's share of what the game charges for an execution.</summary>
        public static int Of(int executionPenalty)
        {
            // Integer division truncates toward zero: a price never grows and
            // never changes sign on the way through.
            return executionPenalty / ExecutionDivisor;
        }
    }
}
