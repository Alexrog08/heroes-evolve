using TaleWorlds.CampaignSystem;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// The single definition of "a hero this mod is allowed to touch".
    /// Shared by the live tick and the diagnostics on purpose: a census that
    /// used a different predicate than the repair path would report on a
    /// population the mod never actually acts on.
    /// </summary>
    public static class HeroFilter
    {
        public static bool IsEligible(Hero hero)
        {
            if (hero == null) return false;
            if (hero.IsDead) return false;
            if (hero.IsHumanPlayerCharacter) return false;
            if (hero == Hero.MainHero) return false;
            if (hero.IsChild) return false;
            if (!hero.IsLord) return false;

            // Hero.AllAliveHeroes (which drives DailyTickHeroEvent) includes
            // template heroes. Vanilla's own AgingCampaignBehavior.DailyTickHero
            // guards with this same hero.IsTemplate check. A template is not a
            // member of the live campaign roster -- it exists only to seed the
            // starting equipment/skills of heroes generated from it -- so
            // writing a granted loadout into its BattleEquipment would mutate
            // data that every hero later spawned from that template inherits.
            if (hero.IsTemplate) return false;

            return true;
        }
    }
}
