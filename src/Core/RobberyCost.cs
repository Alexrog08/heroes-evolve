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
    /// alike. A robbery taken in vengeance, or one that only bad blood
    /// explains, is not charged at all, and that difference is what keeps
    /// relation from feeding on itself (PlunderRules.Motive).
    ///
    /// No circle has a floor. The first version stopped the two wide ones at
    /// three charges each, on the reasoning that a kingdom's disapproval is
    /// not a feud. It read wrongly from the other side -- the friends of a man
    /// robbed for the tenth time holding the robber no lower than they did
    /// after the third -- and it was taken out. A man who makes a career of
    /// robbing one kingdom ends with all of it against him.
    /// </summary>
    public static class RobberyCost
    {
        /// <summary>A robbery against an execution.</summary>
        public const int ExecutionDivisor = 2;

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
        /// The standing between two houses after one of them has taken
        /// vengeance on the other: nearer nought by the amount, and never past
        /// it.
        ///
        /// Never past it because a robbery is not a kindness. Two houses that
        /// have had their answer are even, not friends. The amount is what a
        /// robbery costs with the robbed house, so one answer pays for one
        /// offence and a house robbed twice is owed twice.
        /// </summary>
        public static int Settled(int relation, int amount)
        {
            if (relation >= 0 || amount <= 0) return relation;

            int settled = relation + amount;
            return settled > 0 ? 0 : settled;
        }
    }
}
