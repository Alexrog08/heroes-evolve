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

            int clanContribution = clanGold - pendingClanSpend - reserve;
            if (clanContribution < 0) clanContribution = 0;

            return heroGold + clanContribution;
        }

        /// <summary>
        /// The fraction of a purchase the clan covers. The richer the hero, the
        /// more he pays himself; the richer the house, the less it needs to.
        ///
        /// The four conditions below are evaluated in a fixed order -- base,
        /// then leader, then clan tier, then hero wealth, then clan wealth --
        /// and each later match overwrites whatever the previous ones set,
        /// rather than combining or short-circuiting. A hero can satisfy
        /// several conditions simultaneously (e.g. be his clan's leader *and*
        /// belong to a tier-3 clan with 5000 gold); when that happens, the
        /// last matching rule in this order wins, per the design doc's
        /// "cuota del clan, por orden de aplicacion" table.
        /// </summary>
        public static float ClanShare(bool isClanLeader, int clanTier, int heroGold, int clanGold)
        {
            float share = 0.60f;

            if (isClanLeader) share = 0.70f;
            if (clanTier >= 1) share = 0.40f;
            if (heroGold > 2000) share = 0.30f;
            if (clanGold > 40000) share = 0.20f;

            if (share < 0.10f) share = 0.10f;
            if (share > 0.80f) share = 0.80f;
            return share;
        }
    }
}
