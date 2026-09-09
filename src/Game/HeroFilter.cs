using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace HeroesEvolve
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

            // Lords, and hired companions, and nobody else. A companion given a
            // caravan or a war party is a campaign actor with a loadout to keep
            // up exactly like a lord: he fights, he trains the weapons he
            // carries, and if bandits strip him he needs dressing again before
            // anyone will take him back. CompanionOf is what separates him from
            // the wanderers sitting in taverns, who belong to nobody and have
            // no kit to maintain -- and from notables, who are not fighters at
            // all.
            if (!hero.IsLord && hero.CompanionOf == null) return false;

            // The line is the party, not the clan. Anyone riding inside the
            // player's own party is under his hand: he outfits them out of his
            // inventory, and gear changing without him asking is the exact
            // complaint that started this mod. A hero leading a party of his
            // own is not -- a lord sent off with troops, a companion running a
            // caravan -- and the mod looks after him like any other.
            //
            // The same line IsEligibleToShop already drew, now drawn once for
            // both. Before this, the repair keyed on "is he a lord", which is a
            // different question and answered the wrong one: it let the mod
            // fiddle with a clan brother riding at the player's shoulder while
            // refusing to dress his caravan master after bandits took
            // everything the man owned.
            if (hero.PartyBelongedTo != null && hero.PartyBelongedTo == MobileParty.MainParty)
            {
                return false;
            }

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
        /// The line for the player's own house is not the clan, it is the
        /// party. A hero riding inside the player's party is under his hand:
        /// he outfits them from his own inventory, and gear changing without
        /// him asking is the exact complaint that started this mod. A hero
        /// leading a party of his own is not -- a lord sent off with troops, a
        /// companion running a caravan. Those are away doing their own job and
        /// buying their own kit, and their gold is spent as the clan's, which
        /// is what a party leader's gold is for.
        ///
        /// So a player-clan hero must lead a party that is not the main one.
        /// Anyone at all inside the main party is out, whatever his clan.
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
            if (hero.Clan.IsBanditFaction || hero.Clan.IsOutlaw) return false;

            MobileParty party = hero.PartyBelongedTo;
            if (party != null && party == MobileParty.MainParty) return false;

            if (hero.Clan == Clan.PlayerClan)
            {
                if (party == null) return false;
                if (party.LeaderHero != hero) return false;
            }

            return true;
        }
    }
}
