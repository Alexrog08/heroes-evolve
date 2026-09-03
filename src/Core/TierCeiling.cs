using System;

namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// How good a hero's gear is allowed to get. Adapted from DynamicLordGear's
    /// CalculateTargetGearTier: a weighted blend of clan standing and personal
    /// skill, floored at a configurable minimum.
    /// </summary>
    public static class TierCeiling
    {
        public const int MinTier = 1;
        public const int MaxTier = 6;

        /// <summary>Skill points that buy one tier. DynamicLordGear uses 40.</summary>
        public const int SkillPerTier = 40;

        public static int Compute(int clanTier, int maxCombatSkill,
                                  float clanWeight, float skillWeight, int minimumTier)
        {
            if (clanTier < 0) clanTier = 0;
            if (maxCombatSkill < 0) maxCombatSkill = 0;
            if (clanWeight < 0f) clanWeight = 0f;
            if (skillWeight < 0f) skillWeight = 0f;

            int skillTier = maxCombatSkill / SkillPerTier;
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
