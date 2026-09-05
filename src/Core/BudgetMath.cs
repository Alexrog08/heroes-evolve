namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// Money arithmetic, kept pure so the economy can be reasoned about without
    /// a running campaign. Adapted from Lords Gear, plus a safety reserve that
    /// neither Lords Gear nor NoblesBuyStuff applies.
    ///
    /// Nothing in src/Core consumes this yet -- the purchase engine that spends
    /// against these numbers is a later plan. That is deliberate: this is pure
    /// arithmetic, it belongs in the testable core, and writing and proving it
    /// now means the trickiest economic logic is verified before anything
    /// touches the live game. Do not read the absence of a caller as a defect.
    /// </summary>
    public static class BudgetMath
    {
        /// <summary>
        /// DefaultClanFinanceModel.PartyGoldLowerThreshold. Below this a party is
        /// in financial trouble, so it is the floor we refuse to spend past.
        /// </summary>
        public const int PartyGoldLowerThreshold = 5000;

        /// <summary>
        /// Gold kept untouchable so buying gear can never push a clan below
        /// <see cref="PartyGoldLowerThreshold"/> and stop wages being paid, scaled
        /// by how many war parties the clan is fielding.
        /// </summary>
        public static int Reserve(int warPartyCount, float multiplier)
        {
            // Zero or fewer parties needs no reserve at all; treated the same as
            // "no parties" rather than let a negative count fall through to the
            // multiplication below and produce a negative reserve.
            if (warPartyCount <= 0) return 0;
            if (multiplier < 0f) multiplier = 0f;
            return (int)(warPartyCount * PartyGoldLowerThreshold * multiplier);
        }

        /// <summary>
        /// What a hero may spend: his own gold, plus whatever of the clan purse
        /// is not already committed this cycle and not protected by the reserve.
        /// </summary>
        public static int Available(int heroGold, int clanGold, int pendingClanSpend, int reserve)
        {
            // A hero's own gold should never be negative in a real campaign, but
            // this is pure arithmetic with no upstream enforcement -- guard it
            // here rather than let it silently subsidize the clan side below.
            if (heroGold < 0) heroGold = 0;

            // Same story for the clan-side inputs: pendingClanSpend and reserve
            // should never be negative in a real campaign either, and nothing
            // upstream enforces that here either. Left unguarded, a negative
            // value flips the subtraction below into addition and inflates the
            // clan's apparent contribution above what it actually holds --
            // exactly what the reserve exists to prevent (see
            // PartyGoldLowerThreshold above).
            if (pendingClanSpend < 0) pendingClanSpend = 0;
            if (reserve < 0) reserve = 0;

            int clanContribution = clanGold - pendingClanSpend - reserve;
            if (clanContribution < 0) clanContribution = 0;

            return heroGold + clanContribution;
        }

        /// <summary>
        /// The fraction of a purchase the clan covers. The richer the hero, the
        /// more he pays himself; the richer the house, the less it needs to.
        ///
        /// Hero wealth and clan wealth are independent axes pulling in opposite
        /// directions. An overwrite chain -- where only the last matching
        /// condition decides -- cannot represent both at once: it used to leave
        /// an almost-broke leader of a rich clan with a LOWER share (20%) than a
        /// well-off leader of a modest clan (30%), which is backwards. Each
        /// condition below instead contributes its own additive adjustment to
        /// the base, so a hero satisfying several at once (e.g. being his
        /// clan's leader *and* belonging to a wealthy tier-3 clan) gets the sum
        /// of all of them, per the design doc's now-additive "cuota del clan"
        /// table.
        ///
        /// The upper 80% clamp is genuinely reachable and is not dead code: an
        /// almost-broke leader of a rich clan (leader +10%, clan-wealth +15%,
        /// no hero-wealth deduction) sums to 0.60+0.10+0.15 = 0.85, clamped
        /// down to 0.80. The lower 10% clamp is NOT reachable with today's five
        /// literals -- the minimum achievable is the base minus the hero-wealth
        /// deduction alone, 0.30 -- but it is kept anyway as a guard for if
        /// these adjustments become configurable/data-driven later, where a
        /// combination could actually undershoot it.
        /// </summary>
        public static float ClanShare(bool isClanLeader, int clanTier, int heroGold, int clanGold)
        {
            float share = 0.60f;

            if (isClanLeader) share += 0.10f;
            if (clanTier >= 1) share += 0.05f;
            if (clanGold > 40000) share += 0.15f;
            if (heroGold > 2000) share -= 0.30f;

            if (share < 0.10f) share = 0.10f;
            if (share > 0.80f) share = 0.80f;
            return share;
        }
    }
}
