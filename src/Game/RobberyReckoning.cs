using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// The bill for a robbery, and the receipt for one taken in answer.
    ///
    /// Charged like an execution at half the price, to the same people the
    /// game charges an execution to and by the same tests, read off
    /// DefaultExecutionRelationModel.GetRelationChangeForExecutingHero:
    ///
    ///   his clan      the victim's own house
    ///   his friends   every house whose leader he counts a friend
    ///   his kingdom   every other house of his realm led by a lord
    ///
    /// first match wins, as there. The amounts come from the game's model
    /// through RobberyCost, so nothing here names a number.
    ///
    /// A house pays, not a man. Bannerlord resolves the standing between two
    /// heroes of different clans to the standing between their clan leaders
    /// (DefaultDiplomacyModel.GetHeroesForEffectiveRelation), so what a lord
    /// does is charged to his house and felt by all of it, and what is done to
    /// a lord is resented by all of his. That is why a robbery can be answered
    /// on a man who never robbed anybody -- a brother, a companion leading a
    /// caravan -- and why the player answers for the companions he sends out
    /// at the head of a party: they rob by their own characters
    /// (PlunderService), and it is his house that did it.
    ///
    /// The other side of the same fact is the one exception here. Being robbed
    /// never moves the player's standing with anybody. One number between two
    /// houses cannot fall for the robber without falling for his victim, and
    /// now that a low number makes the next robbery likelier
    /// (PlunderRules.GrudgeDeadZone), charging the lord who strips the
    /// player's man would raise the chance of his stripping the next one --
    /// the victim paying for the offence. The game makes the same exception
    /// for the same reason: CharacterRelationCampaignBehavior.OnRaidCompleted
    /// charges a raider with the owner of the village he burnt unless the
    /// owner is the player's clan. Between two AI houses the number falls for
    /// both and nobody is there to mind; a feud is mutual.
    /// </summary>
    public static class RobberyReckoning
    {
        /// <summary>What one offence was charged, for the log and the census.</summary>
        public struct Bill
        {
            /// <summary>Charged with the victim's own house. Zero when it was spared.</summary>
            public int Clan;

            /// <summary>Houses charged as his friends.</summary>
            public int Friends;

            /// <summary>Houses charged as his kingdom.</summary>
            public int Kingdom;

            /// <summary>Relation taken in all, as a magnitude.</summary>
            public int Spent;

            /// <summary>Standings this offence moved.</summary>
            public int Houses
            {
                get { return (Clan != 0 ? 1 : 0) + Friends + Kingdom; }
            }

            public string Describe()
            {
                return "clan=" + Clan + " friends=" + Friends + " kingdom=" + Kingdom + " spent=" + Spent;
            }
        }

        /// <summary>
        /// Charges a robbery that was the robber's own doing.
        ///
        /// hadItComing is the game's own discount, and the caller decides it:
        /// a prisoner without honour costs what the model charges for
        /// executing one, which is half -- unless he is the robber's friend,
        /// who is never a reprisal whatever his name (PrisonerDialogue).
        /// </summary>
        public static Bill Charge(Hero robber, Hero victim, bool hadItComing)
        {
            return Reckon(robber, victim, hadItComing, true);
        }

        /// <summary>
        /// The same bill, read out and not sent: what Charge would do at this
        /// moment, for the console and the census. Moves nothing.
        /// </summary>
        public static Bill Quote(Hero robber, Hero victim, bool hadItComing)
        {
            return Reckon(robber, victim, hadItComing, false);
        }

        private static Bill Reckon(Hero robber, Hero victim, bool hadItComing, bool charge)
        {
            Bill bill = new Bill();
            if (robber == null || victim == null || robber == victim) return bill;

            ExecutionRelationModel prices = Prices();
            if (prices == null) return bill;

            int clanCost = RobberyCost.Of(hadItComing
                ? prices.PlayerExecutingHeroClanRelationPenaltyDishonorable
                : prices.PlayerExecutingHeroClanRelationPenalty);
            int friendCost = RobberyCost.Of(hadItComing
                ? prices.PlayerExecutingHeroFriendRelationPenaltyDishonorable
                : prices.PlayerExecutingHeroFriendRelationPenalty);
            int kingdomCost = RobberyCost.Of(hadItComing
                ? prices.PlayerExecutingHeroFactionRelationPenaltyDishonorable
                : prices.PlayerExecutingHeroFactionRelationPenalty);

            Clan robbers = robber.Clan;
            Clan victims = victim.Clan;

            // Whether the standings about to move are the player's own. When
            // they are it is because his house did the robbing, which is the
            // only way they should ever move here.
            bool mine = robbers != null && robbers == Clan.PlayerClan;

            // The house itself: no floor, because this one is the feud.
            if (clanCost < 0 && (mine || victims != Clan.PlayerClan))
            {
                if (charge) Apply(robber, victim, clanCost, mine, true);
                bill.Clan = clanCost;
                bill.Spent -= clanCost;
            }

            IFaction realm = victim.MapFaction;

            foreach (Clan clan in Clan.All)
            {
                if (clan == null || clan.IsEliminated || clan.IsBanditFaction) continue;
                if (clan == robbers || clan == victims) continue;

                // The same exception as above, for the other circles: the
                // player does not come to dislike a lord by decree because
                // that lord robbed a friend of his.
                if (!mine && clan == Clan.PlayerClan) continue;

                Hero leader = clan.Leader;
                if (leader == null || !leader.IsAlive || leader == victim || leader == robber) continue;

                int cost;
                int floor;
                bool friend = victim.IsFriend(leader);

                if (friend)
                {
                    cost = friendCost;
                    floor = RobberyCost.FriendsFloor;
                }
                else if (realm != null && leader.MapFaction == realm && leader.IsLord)
                {
                    cost = kingdomCost;
                    floor = RobberyCost.KingdomFloor;
                }
                else
                {
                    continue;
                }

                cost = RobberyCost.Floored(Standing(robber, leader), cost, floor);
                if (cost >= 0) continue;

                // Loud for a friend and quiet for a kingdom, which is the
                // game's own choice: the execution model asks for a
                // notification on every circle but that one.
                if (charge) Apply(robber, leader, cost, mine, friend);

                if (friend) bill.Friends++;
                else bill.Kingdom++;
                bill.Spent -= cost;
            }

            if (charge && mine) Tell(bill);

            return bill;
        }

        /// <summary>
        /// One line for the player on how far a robbery by his house was
        /// heard.
        ///
        /// The game's own notices cover the victim's house and his friends and
        /// say nothing of his kingdom, which is the widest circle and the
        /// quietest: seven or eight houses five points colder each, and no
        /// way to learn of it but the encyclopedia. The game does the same
        /// after an execution, and then adds "The execution has hurt your
        /// relations with N clans" (CharacterRelationCampaignBehavior.
        /// OnHeroKilled). This is that sentence for a robbery, counting every
        /// house the bill reached.
        ///
        /// It is said whether the player did the robbing or one of his people
        /// did it leading a party of their own, because either way the
        /// standings that moved are his.
        /// </summary>
        private static void Tell(Bill bill)
        {
            int houses = bill.Houses;
            if (houses <= 0) return;

            try
            {
                // Set the way the game sets it for its own sentence: the
                // plural test is read from the global text variables.
                MBTextManager.SetTextVariable("IS_PLURAL", houses > 1 ? 1 : 0);

                TextObject line = new TextObject(
                    "{=hev_robbery_hurt}The robbery has hurt your standing with {COUNT} "
                    + "{?IS_PLURAL}clans{?}clan{\\?}.");
                line.SetTextVariable("COUNT", houses);

                InformationManager.DisplayMessage(new InformationMessage(line.ToString(), Colors.Red));
            }
            catch
            {
                // A message that cannot be shown must not cost the bill.
            }
        }

        /// <summary>
        /// Spends the grudge between two houses on a robbery it caused, and
        /// returns how much of it was spent.
        ///
        /// By the price of one robbery of that house, so that one answer
        /// settles one offence, and never past even (RobberyCost.Settled).
        ///
        /// Written straight to the relation rather than through
        /// ChangeRelationAction, which is the right door for every cost in
        /// this file and the wrong one here. A gain through it is scaled by
        /// Charm and its perks (DefaultDiplomacyModel.GetRelationIncreaseFactor)
        /// and pays Charm experience to whoever gained it
        /// (DefaultSkillLevelingManager.OnGainRelation): the two houses could
        /// come out of a robbery on better terms than even, and a lord would
        /// be learning charm by stripping his enemies.
        ///
        /// This is the one place the player's standing moves for something
        /// done to him, and it only ever moves up: the debt is his, and this
        /// is it being paid.
        /// </summary>
        public static int Settle(Hero captor, Hero prisoner)
        {
            if (captor == null || prisoner == null || captor == prisoner) return 0;

            ExecutionRelationModel prices = Prices();
            if (prices == null) return 0;

            int amount = -RobberyCost.Of(prices.PlayerExecutingHeroClanRelationPenalty);

            Hero first, second;
            Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(captor, prisoner,
                                                                                  out first, out second);
            if (first == null || second == null || first == second) return 0;

            int standing = CharacterRelationManager.GetHeroRelation(first, second);
            int settled = RobberyCost.Settled(standing, amount);
            if (settled == standing) return 0;

            CharacterRelationManager.SetHeroRelation(first, second, settled);
            return settled - standing;
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
        /// It is the part of the price that makes him known. The game charges
        /// an execution to every honourable noble in Calradia as relation;
        /// here that circle is his Honor instead, and honourable captors read
        /// it when they hold him (PlunderRules.JusticePerLevel). A trait level
        /// is a thousand experience either side of nought
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
            ExecutionRelationModel prices = Prices();
            int honour = prices != null ? RobberyCost.Of(prices.PlayerExecutingHeroHonorPenalty) : 0;

            return PlunderRules.AfterReprisal(honour, hadItComing);
        }

        /// <summary>
        /// The standing between two heroes as the game stores it: between
        /// whoever answers for each of them, before the few points their
        /// characters add or take.
        /// </summary>
        internal static int Standing(Hero one, Hero other)
        {
            if (one == null || other == null || one == other) return 0;
            if (Campaign.Current == null || Campaign.Current.Models == null) return 0;

            Hero first, second;
            Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(one, other,
                                                                                  out first, out second);
            if (first == null || second == null || first == second) return 0;

            return CharacterRelationManager.GetHeroRelation(first, second);
        }

        private static void Apply(Hero robber, Hero other, int cost, bool mine, bool loud)
        {
            if (cost == 0) return;

            // The player's house through the player's own door, whichever of
            // his people did it: it is the same number either way, and this is
            // the door that tells him.
            if (mine) ChangeRelationAction.ApplyPlayerRelation(other, cost, true, loud);
            else ChangeRelationAction.ApplyRelationChangeBetweenHeroes(robber, other, cost, false);
        }

        private static ExecutionRelationModel Prices()
        {
            if (Campaign.Current == null || Campaign.Current.Models == null) return null;
            return Campaign.Current.Models.ExecutionRelationModel;
        }
    }
}
