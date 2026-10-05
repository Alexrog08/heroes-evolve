namespace HeroesEvolve.Core
{
    /// <summary>
    /// What a robbery costs the house that did it, in the game's own coin.
    ///
    /// Half an execution. The game prices killing a prisoner in
    /// ExecutionRelationModel -- sixty with his clan, thirty with each of his
    /// friends, ten with every lord of his kingdom, and a thousand of the
    /// killer's Honor -- and taking his arms is the lesser act of the same
    /// kind: a breach of the customs of war against a man who cannot answer
    /// it. So the figures are read from that model while the campaign runs
    /// and halved here rather than written down a second time, and a mod
    /// that reprices executions reprices this with them.
    ///
    /// Three of the model's four circles are charged as relation. The fourth,
    /// every honourable noble on the map, is charged as reputation instead;
    /// PlunderRules.JusticePerLevel says why.
    ///
    /// The model halves its own prices when the dead man had no honour, and
    /// those halved figures are the ones read for a prisoner of the same
    /// sort. The discount this mod has always given for robbing a scoundrel
    /// (PlunderRules.IsReprisal) is therefore still the game's own.
    ///
    /// Everyone who robs out of his own character pays it, lord and player
    /// alike. A robbery that only a grudge explains is not charged at all,
    /// and that difference is what keeps relation from feeding on itself
    /// (PlunderRules.Motive).
    /// </summary>
    public static class RobberyCost
    {
        /// <summary>A robbery against an execution.</summary>
        public const int ExecutionDivisor = 2;

        /// <summary>
        /// Where the cost with a victim's friends stops, and below it the cost
        /// with his kingdom: three charges deep, each.
        ///
        /// The wide circles have a floor and the house itself has none. A
        /// kingdom's disapproval is not a feud. It is charged to seven or
        /// eight houses at a time for something done to one of them, and left
        /// unbounded it would put a man who robbed twenty prisoners of one
        /// realm at minus a hundred with houses he never touched -- well inside
        /// the range where the game lets a captor execute him, which begins
        /// below minus thirty (PlayerCaptivityCampaignBehavior.OnPrisonerTaken)
        /// -- for an offence against somebody else. Three charges and it has
        /// said what it has to say. The house he actually robbed is the one
        /// that may hate him as deeply as he digs.
        ///
        /// Simulated over a map of seventy-two houses, the floors change
        /// little in thirteen years (mean relation between houses -8.0 with
        /// them, -8.6 without) and more in thirty, which is what a bound is
        /// for.
        /// </summary>
        public const int FriendsFloor = -45;
        public const int KingdomFloor = -15;

        /// <summary>
        /// What the act leaves on the robber's Mercy. Unchanged from when it
        /// was charged through TraitLevelingHelper.OnHostileAction, twenty to
        /// Honor and Mercy alike: Honor now pays half an execution, and the
        /// game charges an execution to Honor alone, so Mercy keeps the small
        /// mark it always took for a small cruelty.
        /// </summary>
        public const int MercyXp = -20;

        /// <summary>A robbery's share of what the game charges for an execution.</summary>
        public static int Of(int executionPenalty)
        {
            // Integer division truncates toward zero: a price never grows and
            // never changes sign on the way through.
            return executionPenalty / ExecutionDivisor;
        }

        /// <summary>
        /// What may still be charged of a cost that stops at a floor: all of
        /// it, the part that reaches the floor, or nothing once the relation
        /// is already there.
        /// </summary>
        public static int Floored(int relation, int cost, int floor)
        {
            if (cost >= 0) return 0;
            if (relation <= floor) return 0;

            int room = floor - relation;
            return cost < room ? room : cost;
        }

        /// <summary>
        /// A relation after a grudge has been spent on a robbery: nearer zero
        /// by the amount, and never past it.
        ///
        /// Never past it because a robbery is not a kindness. Two houses that
        /// have had their answer are even, not friends. And a relation that
        /// was not a grudge is left exactly as it stood: the robbery of a
        /// known thief by an honourable man who had nothing against him
        /// settles nothing, because there was nothing between them to settle.
        /// </summary>
        public static int Settled(int relation, int amount)
        {
            if (relation >= 0 || amount <= 0) return relation;

            int settled = relation + amount;
            return settled > 0 ? 0 : settled;
        }
    }
}
