using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// Gives a repaired hero the skills his age should already have brought him.
    ///
    /// The come-of-age failure is not only an equipment failure. The lords it
    /// produces carry one combat skill or none: Hasawa reached forty-eight with
    /// zero in all seven, Wara forty-four the same, Elyaksha forty-five with
    /// nothing but Riding. Dressing them changes how they look and nothing else
    /// -- they still cannot fight, and because the tier ceiling is computed from
    /// their best combat skill they stay capped at tier 1 for life however much
    /// gold the purchase engine ever gives them.
    ///
    /// Weekly growth alone would drag them out eventually, but a man of
    /// forty-eight should not spend another decade of campaign time becoming
    /// competent. Seeding puts him where his contemporaries are on the day he is
    /// repaired, and growth carries him from there.
    ///
    /// Only ever applied to heroes the mod has just repaired. A lord the game
    /// built properly is never touched.
    /// </summary>
    public static class SkillSeeding
    {
        /// <summary>
        /// Brings the hero's combat skills up to the level his years and talent
        /// call for, in the skills his newly granted equipment uses. Returns how
        /// many skills were raised.
        /// </summary>
        public static int Seed(Hero hero)
        {
            if (hero == null || hero.HeroDeveloper == null || hero.BattleEquipment == null) return 0;

            float talent = HeroTalent.For(hero);
            int primaryTarget = SkillGrowth.PrimaryTarget(hero.Age, talent);
            if (primaryTarget <= 0) return 0;

            int raised = 0;

            List<SkillObject> ranked = SkillGrowthService.RankedWeaponSkills(hero);
            for (int rank = 0; rank < ranked.Count; rank++)
            {
                if (Raise(hero, ranked[rank], SkillGrowth.TargetForRank(primaryTarget, rank))) raised++;
            }

            bool mounted = hero.BattleEquipment[EquipmentIndex.Horse].Item != null;
            SkillObject movement = mounted ? DefaultSkills.Riding : DefaultSkills.Athletics;
            if (Raise(hero, movement, SkillGrowth.TargetForRank(primaryTarget, SkillGrowthService.MovementRank)))
            {
                raised++;
            }

            return raised;
        }

        /// <summary>
        /// Raises one skill to a level, never lowering it.
        ///
        /// ChangeSkillLevel rather than AddSkillXp: it converts a level
        /// difference into experience through the game's own
        /// GetXpRequiredForSkillLevel curve and applies it with the focus factor
        /// switched off, so the hero lands exactly on the level asked for. An XP
        /// grant would have to guess at the curve -- which costs far more per
        /// point at high levels -- and would then be multiplied by whatever
        /// focus the hero happens to hold.
        /// </summary>
        private static bool Raise(Hero hero, SkillObject skill, int target)
        {
            if (skill == null || target <= 0) return false;

            int current = hero.GetSkillValue(skill);
            if (current >= target) return false;

            hero.HeroDeveloper.ChangeSkillLevel(skill, target - current, false);
            return true;
        }
    }
}
