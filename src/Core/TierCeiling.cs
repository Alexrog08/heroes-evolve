using System;

namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// How good a hero's gear is allowed to get: a weighted blend of clan
    /// standing and personal skill, floored at a configurable minimum. The shape
    /// is DynamicLordGear's CalculateTargetGearTier; the numbers are no longer.
    ///
    /// Two censuses of a live campaign, one young and one mature, took clan
    /// standing out of the answer. It does not correlate with what a lord wears:
    /// bucketed by clan tier, the gear vanilla itself issued sits at 4.42, 4.50
    /// and 4.50 for tiers 4, 5 and 6, and in the young save it runs *backwards*,
    /// 4.25 at tier 1 against 4.10 at tier 6. And it stops separating anything
    /// at all by midgame: the mature save had no clan below tier 4 and 47 of 82
    /// at tier 5 or 6. It was also never a comparable currency to skill -- a
    /// clan reaches tier 4 and may found a kingdom, while a lord reaching 240
    /// combat skill takes a lifetime.
    ///
    /// So production passes clanWeight 0 and this is skill alone. The blend
    /// stays because the weights are settings, not because clan tier is
    /// expected back.
    ///
    /// Wealth still decides the top end -- it turned out to be the only one of
    /// the three that separates anything, cleanly and across the whole life of a
    /// campaign -- but it does it in BudgetService, where a purse belongs,
    /// instead of being smuggled in as a rank.
    /// </summary>
    public static class TierCeiling
    {
        public const int MinTier = 1;
        public const int MaxTier = 6;

        /// <summary>
        /// Skill points that buy one tier, when the caller has no opinion.
        ///
        /// DynamicLordGear uses 40, which put the median lord's ceiling at 3 once
        /// clan standing stopped propping it up -- below the 4.4 average tier of
        /// the gear vanilla had already given him, so the engine would have had
        /// nothing to offer anybody. 28 puts the median lord (about 140 combat
        /// skill in the mature census) at 5, one tier above what he is wearing,
        /// so there is always something better on the shelf.
        ///
        /// That is the point of the number: it deliberately stops the ceiling
        /// from being the binding constraint, and hands that job to the purse.
        /// A tier-6 piece costs about 43,300, so at a tenth of the wallet it
        /// takes a house holding some 433,000 -- which in the young save was no
        /// clan at all and in the mature one was 61 of 82.
        ///
        /// Passed in rather than read from a global so the core stays pure and
        /// the tests can sweep it, exactly as the two weights already are.
        /// </summary>
        public const int DefaultSkillPerTier = 28;

        public static int Compute(int clanTier, int maxCombatSkill,
                                  float clanWeight, float skillWeight, int minimumTier)
        {
            return Compute(clanTier, maxCombatSkill, clanWeight, skillWeight, minimumTier,
                           DefaultSkillPerTier);
        }

        public static int Compute(int clanTier, int maxCombatSkill,
                                  float clanWeight, float skillWeight, int minimumTier,
                                  int skillPerTier)
        {
            if (clanTier < 0) clanTier = 0;
            if (maxCombatSkill < 0) maxCombatSkill = 0;
            if (clanWeight < 0f) clanWeight = 0f;
            if (skillWeight < 0f) skillWeight = 0f;
            if (skillPerTier < 1) skillPerTier = DefaultSkillPerTier;

            int skillTier = maxCombatSkill / skillPerTier;
            if (skillTier > MaxTier) skillTier = MaxTier;

            float totalWeight = clanWeight + skillWeight;

            int blended;
            if (totalWeight <= 0f)
            {
                // Degenerate configuration: no opinion, so the floor decides.
                blended = MinTier;
            }
            else
            {
                float weighted = (clanTier * clanWeight + skillTier * skillWeight) / totalWeight;
                blended = (int)Math.Round(weighted, MidpointRounding.AwayFromZero);
            }

            int result = blended;
            if (result < minimumTier) result = minimumTier;
            if (result < MinTier) result = MinTier;
            if (result > MaxTier) result = MaxTier;
            return result;
        }
    }
}
