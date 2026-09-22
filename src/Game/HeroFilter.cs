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
        /// <summary>
        /// Which test IsEligible failed on, or null when it passed.
        ///
        /// Mirrors the order below and exists because "grown=False" in a census
        /// is a fact without a cause, and the causes want opposite fixes: a
        /// companion refused for riding in the player's party is a boundary
        /// working as designed, and one refused for having no CompanionOf is a
        /// hole. Guessing between them from names and ages wasted a round trip.
        ///
        /// Kept beside the real filter deliberately. A copy that drifts would be
        /// worse than no copy at all, so anything added there is added here.
        /// </summary>
        /// <summary>
        /// Whether the starting kit may be given to this hero.
        ///
        /// Narrower than IsEligible, which also decides who can be robbed --
        /// and every companion can be robbed, the player's included. The kit is
        /// something else: the store page promises it "only for lords who never
        /// had a loadout", because it exists for TaleWorlds' come-of-age bug,
        /// and a lord is the only kind of hero who comes of age into it.
        ///
        /// A hired companion never qualifies. He walked out of the tavern with
        /// a kit, and whatever he carries since was the player's choice -- the
        /// market already respects that, improving a companion's loadout only
        /// while he leads a party of his own and never replacing it. The
        /// repair did the opposite: it read a companion with a tier-1 piece as
        /// broken and swapped the piece out, which is a player's choice
        /// overwritten by a rule written for a bug. Companions were let in for
        /// one real reason, dressing a caravan master again after bandits took
        /// everything; the rags have answered that since v1.3.5, in the same
        /// instant and in the same shape.
        ///
        /// The player's own clan follows his switch, as it does at the market.
        /// </summary>
        public static bool IsEligibleForRepair(Hero hero)
        {
            return WhyNoRepair(hero) == null;
        }

        /// <summary>Why the starting kit is refused this hero, or null when it is not.</summary>
        public static string WhyNoRepair(Hero hero)
        {
            string why = WhyIneligible(hero);
            if (why != null) return why;
            if (!hero.IsLord) return "companion";
            if (!Settings.ManageOwnClan && hero.Clan == Clan.PlayerClan) return "ownClanOff";
            return null;
        }

        public static string WhyIneligible(Hero hero)
        {
            if (hero == null) return "null";
            if (hero.IsDead) return "dead";
            if (hero.IsHumanPlayerCharacter) return "isPlayer";
            if (hero == Hero.MainHero) return "isMainHero";
            if (hero.IsChild) return "isChild";
            if (!hero.IsLord && hero.CompanionOf == null) return "notLordAndNotCompanion";
            if (hero.IsDisabled) return "disabled";
            if (hero.PartyBelongedTo != null && hero.PartyBelongedTo == MobileParty.MainParty)
            {
                return "inMainParty";
            }
            if (hero.IsTemplate) return "isTemplate";
            if (hero.Clan != null && hero.Clan.IsBanditFaction) return "banditClan";
            return null;
        }

        /// <summary>
        /// Whether this mod grows this hero's skills.
        ///
        /// Everything IsEligible tests except the main party, and the exception
        /// is the whole point. That clause exists because the player outfits the
        /// heroes riding with him out of his own inventory, and gear changing
        /// without him asking is the complaint this mod was written to answer.
        /// It is a rule about equipment, and it had been silently deciding
        /// skills too -- so a companion at the player's shoulder learned nothing
        /// for life while every lord on the map gained one to three points a
        /// year, and over a long campaign he could only fall further behind.
        ///
        /// Growth takes nothing from anyone. It cannot overwrite a choice the
        /// player made, and it cannot overshoot: SkillGrowth.PointsStep returns
        /// zero the moment a hero reaches his target, so a companion the player
        /// actually fights with is already at or past his ceiling and receives
        /// nothing at all. The only hero this reaches is one who has genuinely
        /// fallen behind, and it carries him to the same ceiling every other
        /// lord is aiming at -- no further.
        ///
        /// ManageOwnClan does not gate this either, for the same reason: a
        /// player who would rather choose his brother's armour himself rarely
        /// wants his brother to stop learning.
        /// </summary>
        public static bool IsEligibleToGrow(Hero hero)
        {
            if (hero == null) return false;
            if (hero.IsDead) return false;
            if (hero.IsHumanPlayerCharacter) return false;
            if (hero == Hero.MainHero) return false;
            if (hero.IsChild) return false;
            // Lords, companions, and the wanderers nobody has hired yet.
            //
            // Wider than the gear filter on purpose. A lord sitting out a war in
            // his castle is training, running his accounts and reading, and a
            // wanderer waiting in a tavern is doing whatever he did before
            // anyone offered him work -- neither is standing still, and only a
            // party made the difference under the old rule. The two things that
            // follow are worth having: a lord who has spent years garrisoned
            // rides out competent rather than rusty, and a companion hired late
            // is worth hiring late, since the years he spent unhired went
            // somewhere.
            //
            // Note that Hero.Level rises with skill and Hero.Level is one term
            // in DefaultCompanionHiringPriceCalculationModel, so an old
            // wanderer now costs more to take on. That is the same trade the
            // player is being offered -- a better man for more money -- rather
            // than a side effect to be sorry about.
            //
            // Notables stay out, which the occupation check does for free:
            // merchants, headmen, gang leaders, preachers and rural notables
            // are none of the three. They are not fighters, they do not carry a
            // loadout, and a village headman quietly gaining One Handed for
            // sixty years is nobody's idea of an improvement.
            if (!hero.IsLord
                && hero.CompanionOf == null
                && hero.Occupation != Occupation.Wanderer) return false;

            if (hero.IsTemplate) return false;

            if (hero.Clan != null && hero.Clan.IsBanditFaction) return false;

            return true;
        }

        /// <remarks>
        /// The clan test below was six flags and is now one, which let thirteen
        /// mercenary companies back into the campaign.
        ///
        /// It used to refuse IsMinorFaction, IsOutlaw, IsSect, IsNomad and
        /// IsMafia alongside IsBanditFaction, on the reading that all of them
        /// meant bandits. Counted over the shipped clans, they do not. Every one
        /// of the eight bandit clans carries is_bandit, and nothing else does.
        /// The other five flags mark the mercenary companies a player hires and
        /// fights: is_minor_faction covers all seventeen of them, is_outlaw
        /// thirteen, is_mafia six (Wolfskins, Hidden Hand, Lake Rats and their
        /// like), is_nomad five (Jawwal, Forest People, Eleftheroi) and is_sect
        /// two (Embers of the Flame, Chosen of the Sky). None of them is a
        /// bandit clan.
        ///
        /// So their lords were frozen: no skill growth, no repair when stripped,
        /// no market, and -- the report that found this -- no way to rob one you
        /// had captured, however well dressed he was. Every other lord on the map
        /// improved around them for the length of a campaign. That is the exact
        /// stagnation this mod exists to undo, applied to seventeen companies by
        /// accident.
        ///
        /// IsBanditFaction alone separates the two groups cleanly, so the
        /// Clan.PlayerClan guard that stood in front of this goes with the rest:
        /// it was there because the player's own clan carries is_minor_faction,
        /// and nothing now tests that.
        /// </remarks>
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

            // Off the map. TaleWorlds disables a hero it has sent away: a
            // companion on an issue's alternative solution
            // (IssueBase.StartIssueWithAlternativeSolution -> DisableHeroAction,
            // which also lifts him out of his party), or a candidate set aside
            // during heir selection. Riding with nobody, he would read to the
            // party line below as a man out on his own, and the mod would take
            // him in hand. He is on the player's errand instead, and must come
            // back exactly as he left.
            //
            // Found in play: a companion who never left the player's side except
            // for one such errand came home in a new helmet and new bracers.
            if (hero.IsDisabled) return false;

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
            //
            // Never the player's own clan, whatever flags it happens to carry.
            // A census found nine of nineteen player-clan heroes refused here,
            // including companions leading their own parties, which is the
            // opposite of what the comment three blocks up promises. The guard
            // is about respecting somebody else's design; the player's house is
            // not somebody else's design, and if the engine marks it with one
            // of these that is a fact about how the clan was created rather
            // than a statement that it should stay rough.
            if (hero.Clan != null && hero.Clan.IsBanditFaction) return false;

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
            // Lords, and companions who lead a party of their own.
            //
            // This clause read "if (!hero.IsLord) return false" and threw out
            // every companion in the campaign -- including the caravan master
            // the paragraph above names by hand. Hero.IsLord is Occupation ==
            // Lord, and AddCompanionAction.ApplyInternal writes CompanionOf and
            // nothing else, so a hired companion keeps Occupation.Wanderer for
            // life. Of the two things in the whole assembly that call
            // SetNewOccupation, one is hero creation and the other is a
            // companion founding his own clan.
            //
            // So the man the comment described as away doing his own job and
            // buying his own kit was the one hero the engine could never
            // describe that way. Repair dressed him once and his gear then
            // stood still for the rest of the campaign while every lord on the
            // map went on improving.
            //
            // A companion must lead a party and a lord need not, and that
            // asymmetry is the rule rather than an oversight. A lord has a
            // house and a station to keep up whether or not he is in the field.
            // A companion with no party has no job and no purse -- he is
            // waiting in a tavern, and the unhired wanderers waiting beside him
            // are refused by the same line.
            if (!hero.IsLord && hero.CompanionOf == null) return false;
            if (hero.IsTemplate) return false;

            if (hero.Clan == null) return false;

            // Bandits only, the same one flag the gear filter uses and for the
            // same reason. Thirteen mercenary companies carry is_outlaw and not
            // one of them is a bandit clan; refusing them here would have left
            // them dressed by the repair and unable to buy anything afterwards,
            // which is the half-measure worth avoiding.
            if (hero.Clan.IsBanditFaction) return false;

            MobileParty party = hero.PartyBelongedTo;
            if (party != null && party == MobileParty.MainParty) return false;

            if (!hero.IsLord && (party == null || party.LeaderHero != hero)) return false;

            if (hero.Clan == Clan.PlayerClan)
            {
                // The player's own, at the player's discretion. See
                // Settings.ManageOwnClan for why this is a boundary rather
                // than an exemption.
                if (!Settings.ManageOwnClan) return false;
                if (party == null) return false;
                if (party.LeaderHero != hero) return false;
            }

            return true;
        }
    }
}
