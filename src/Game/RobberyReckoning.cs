using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// What a robbery costs the man who did it, charged through the game's own
    /// doors. The vengeance sworn on him for it is VengeanceOaths' business.
    ///
    /// A house pays, not a man. Bannerlord resolves the standing between two
    /// heroes of different clans to the standing between their clan leaders
    /// (DefaultDiplomacyModel.GetHeroesForEffectiveRelation), so what a lord
    /// does is charged to his house and felt by all of it. That is why the
    /// player answers for the companions he sends out at the head of a party:
    /// they rob by their own characters (PlunderService), and it is his house
    /// that did it.
    ///
    /// The other side of the same fact is the one exception here. Being robbed
    /// never moves the player's standing with anybody. One number between two
    /// houses cannot fall for the robber without falling for his victim, and
    /// of the two the player's is the one that should only ever move for
    /// things his house did. The game makes the same exception for the same
    /// reason: CharacterRelationCampaignBehavior.OnRaidCompleted charges a
    /// raider with the owner of the village he burnt unless the owner is the
    /// player's clan. It used to cover the player alone and charge him for a
    /// companion robbed out of his sight, which was the same number moving
    /// for the same reason; it covers his house now.
    /// </summary>
    public static class RobberyReckoning
    {
        /// <summary>
        /// Charges the robber's house its standing with the house he robbed,
        /// and returns what was charged: nothing when the robbed house is the
        /// player's.
        ///
        /// hadItComing is the game's own discount, and the caller decides it:
        /// a prisoner without honour costs half -- unless he is the robber's
        /// friend, who is never a reprisal whatever his name
        /// (PrisonerDialogue).
        /// </summary>
        public static int Charge(Hero robber, Hero victim, bool hadItComing)
        {
            if (robber == null || victim == null || robber == victim) return 0;

            bool mine = robber.Clan != null && robber.Clan == Clan.PlayerClan;
            if (!mine && victim.Clan != null && victim.Clan == Clan.PlayerClan) return 0;

            int cost = PlunderRules.AfterReprisal(RobberyCost.Standing, hadItComing);
            if (cost == 0) return 0;

            // The player's house through the player's own door, whichever of
            // his people did it: it is the same number either way, and this is
            // the door that tells him.
            if (mine) ChangeRelationAction.ApplyPlayerRelation(victim, cost, true, true);
            else ChangeRelationAction.ApplyRelationChangeBetweenHeroes(robber, victim, cost, false);

            return cost;
        }

        /// <summary>
        /// What robbing a prisoner does to the player's own name: half an
        /// execution's Honor, and the small mark on Mercy it always left.
        /// Returns the Honor charged.
        ///
        /// Only the player has a name that moves. AddTraitXp is hardwired to
        /// Campaign.PlayerTraitDeveloper and Hero.MainHero, and an AI lord's
        /// character is fixed when he is generated, so this is called from the
        /// one place the player robs: the conversation where he chose to.
        ///
        /// It is the part of the price that makes him known. Honourable
        /// captors read his Honor when they hold him
        /// (PlunderRules.JusticePerLevel), and a trait level is a thousand
        /// experience either side of nought
        /// (DefaultCharacterDevelopmentModel.GetTraitXpRequiredForTraitLevel),
        /// so two prisoners stripped take a man of ordinary name to Honor
        /// minus one. Fifty used to.
        ///
        /// OnIncidentResolved is the game's own plain door onto one trait:
        /// AddPlayerTraitXPAndLogEntry for the trait and amount it is given,
        /// logged against the player. OnHostileAction, used before, charges
        /// Honor and Mercy the same amount and cannot charge them differently.
        /// </summary>
        public static int ChargeName(bool hadItComing)
        {
            int honour = NamePrice(hadItComing);
            int mercy = PlunderRules.AfterReprisal(RobberyCost.MercyXp, hadItComing);

            if (honour != 0) TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Honor, honour);
            if (mercy != 0) TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Mercy, mercy);

            return honour;
        }

        /// <summary>The Honor a robbery would be charged, read out and not charged.</summary>
        public static int NamePrice(bool hadItComing)
        {
            ExecutionRelationModel prices = null;
            if (Campaign.Current != null && Campaign.Current.Models != null)
            {
                prices = Campaign.Current.Models.ExecutionRelationModel;
            }

            int honour = prices != null ? RobberyCost.Of(prices.PlayerExecutingHeroHonorPenalty) : 0;
            return PlunderRules.AfterReprisal(honour, hadItComing);
        }
    }
}
