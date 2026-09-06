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

            // Minor factions are excluded, not repaired. The Eleftheroi, the
            // Jawwal and their kind are deliberately rough: their whole troop
            // tree fields cruder equipment because that is the clan's identity,
            // and their leaders match it. Two of the sixteen heroes the symptom
            // detector flagged in a live campaign were minor-faction leaders,
            // and "repairing" them would have erased the design rather than a
            // defect. Their gear still improves later through the purchase
            // engine, which scales with the clan's own wealth and skills.
            if (hero.Clan != null && (hero.Clan.IsMinorFaction
                                      || hero.Clan.IsBanditFaction
                                      || hero.Clan.IsOutlaw
                                      || hero.Clan.IsSect
                                      || hero.Clan.IsNomad
                                      || hero.Clan.IsMafia)) return false;

            return true;
        }

        /// <summary>
        /// Who the purchase engine acts on. Deliberately not the same set as
        /// the repair.
        ///
        /// Minor factions are IN. The repair leaves them alone because their
        /// rough kit is their identity and "fixing" it would erase a design
        /// choice -- but buying is not a correction, it is a clan spending what
        /// it has earned, and the Jawwal getting richer over a campaign is the
        /// game working.
        ///
        /// The player's own clan is OUT, which is section 11's stated scope --
        /// AI lords only -- and matters more than a setting. Clan.Gold IS the
        /// leader's gold, so for the player's house that is the player's own
        /// purse: companions buying armour would spend his money without asking
        /// him. Losing control of your own companions is the complaint that
        /// started this mod; the purchase engine must not reproduce it.
        ///
        /// Bandits stay out for the same reason as always: they are not lords
        /// with houses and purses.
        /// </summary>
        public static bool IsEligibleToShop(Hero hero)
        {
            if (hero == null) return false;
            if (hero.IsDead) return false;
            if (hero.IsHumanPlayerCharacter) return false;
            if (hero == Hero.MainHero) return false;
            if (hero.IsChild) return false;
            if (!hero.IsLord) return false;
            if (hero.IsTemplate) return false;

            if (hero.Clan == null) return false;
            if (hero.Clan == Clan.PlayerClan) return false;
            if (hero.Clan.IsBanditFaction || hero.Clan.IsOutlaw) return false;

            return true;
        }
    }
}
