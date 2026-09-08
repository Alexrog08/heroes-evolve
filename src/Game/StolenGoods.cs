using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// Keeping stolen gear moving, because vanilla will not.
    ///
    /// Taking a prisoner's kit puts things into a lord's saddlebags that the
    /// game has no way of getting out again. Items flagged NotMerchandise --
    /// which is most of what makes a noble look like one -- are excluded from
    /// MapEvent.LootDefeatedPartyItems, so beating that lord in the field does
    /// not win them back, and no merchant stocks them, so he has no reason to
    /// part with them either. Left alone, a gilded helm taken off Caladog sits
    /// in some Battanian's baggage train until the end of the campaign.
    ///
    /// Which is the opposite of the point. Unique gear was made takeable so it
    /// would circulate, and circulation needs two doors the game does not open:
    /// he must be able to sell it, and you must be able to beat him for it.
    /// Both are opened here, and only for the items vanilla strands -- ordinary
    /// merchandise already has both.
    ///
    /// Worth knowing, and checked rather than assumed: of the twelve places the
    /// game reads NotMerchandise, none is the trade screen. The flag stops an
    /// item reaching a shop through production, loot or caravan stock; it does
    /// not stop anyone buying one that is already sitting on the shelf. So a
    /// stolen helm sold into a town really is there for its owner, for a rival,
    /// or for the player who walks in first.
    /// </summary>
    public static class StolenGoods
    {
        /// <summary>
        /// Sells everything in this party's baggage that no merchant would have
        /// stocked, at the town it has just entered.
        ///
        /// The lord has no use for a cuirass of the wrong culture and no way to
        /// carry it forever, so he does what anyone would: turns it into coin at
        /// the first market.
        ///
        /// A town that cannot afford a piece does not get it. Paying what little
        /// it holds and taking the goods anyway would have lords handing over
        /// forty-thousand-denar harnesses for nothing, which is not a sale, and
        /// the piece is better off staying in the baggage until a richer market
        /// comes along. The purse is drawn down as it goes, so one visit cannot
        /// empty a town twice over.
        /// </summary>
        public static int SellAt(Settlement settlement, PartyBase party)
        {
            if (settlement == null || party == null) return 0;

            SettlementComponent component = settlement.SettlementComponent;
            ItemRoster shelf = settlement.ItemRoster;
            ItemRoster baggage = party.ItemRoster;
            if (component == null || shelf == null || baggage == null) return 0;

            List<ItemRosterElement> stranded = Stranded(baggage);
            if (stranded.Count == 0) return 0;

            Hero seller = party.LeaderHero;
            int sold = 0;

            for (int i = 0; i < stranded.Count; i++)
            {
                EquipmentElement element = stranded[i].EquipmentElement;
                int amount = stranded[i].Amount;

                for (int n = 0; n < amount; n++)
                {
                    int price = component.GetItemPrice(element, party.MobileParty, true);
                    if (price < 0) price = 0;

                    // No money, no sale. He keeps it and tries the next town.
                    if (price > component.Gold) break;

                    baggage.AddToCounts(element, -1);
                    shelf.AddToCounts(element, 1);

                    if (price > 0 && seller != null)
                    {
                        GiveGoldAction.ApplyForSettlementToCharacter(settlement, seller, price, true);
                    }

                    sold++;
                }
            }

            if (sold > 0)
            {
                ModLog.Info("STOLENSOLD party=" + party.Name + " town=" + settlement.Name
                            + " pieces=" + sold);
            }

            return sold;
        }

        /// Puts the losers' stranded goods through the game's own division of
        /// spoils, which is what would have happened to them already if a
        /// filter had not skipped them first.
        ///
        /// The distinction matters and it is the whole design. These items are
        /// not outside the division because the division rejected them; they
        /// are outside it because MapEvent.LootDefeatedPartyItems filters the
        /// loser's baggage before dividing anything, and the filter drops
        /// everything NotMerchandise. Vanilla's rule for who gets what never
        /// saw them. So this does not decide who gets them -- it hands them to
        /// BattleRewardModel.GetLootItemChancesForWinnerParties and abides by
        /// the answer, exactly as the ordinary spoils did.
        ///
        /// Which settles a question no invented rule could have settled
        /// honestly. The game divides by ContributionToBattle -- a number that
        /// starts at zero and grows only in OnTroopScoreHit, one hit at a time
        /// (ResetContributionToBattleToStrength exists but nothing in the
        /// shipped game calls it). Ride in another man's army and you are paid
        /// for what your men did, not for your rank; join a battle already
        /// under way and you are paid for the part you fought; stand and watch
        /// and you are paid nothing. Garrisons and militia never share, and
        /// when the defeated party is a settlement the model returns no shares
        /// at all, so a stormed town's stores stay where they are. All of that
        /// comes free by asking rather than deciding.
        ///
        /// Into the loot pile itself, and that is why this hangs off
        /// OnCollectLootItems. MapEventParty.RosterToReceiveLootItems is not
        /// one roster but two: for an NPC it is the party's own ItemRoster, and
        /// for the player -- IsNpcParty is literally "Party != MainParty" -- it
        /// is PlayerEncounter.Current.RosterToReceiveLootItems, the object
        /// DoLootInventory hands to InventoryScreenHelper.OpenScreenAsLoot. So
        /// a recovered helm lands in the player's loot window beside the
        /// ordinary spoils, or in a lord's baggage, from one line of code. The
        /// first attempt hung off MapEventEnded, which runs after that window
        /// is built and would have dropped the gear in unannounced.
        ///
        /// Runs once per battle without being told to. The event fires for each
        /// winner, and whichever fires first empties the losers of stranded
        /// goods, so the rest find nothing to move twice.
        /// </summary>
        public static int Recover(PartyBase winner)
        {
            if (winner == null) return 0;

            MapEvent mapEvent = winner.MapEvent;
            if (mapEvent == null || !mapEvent.HasWinner) return 0;

            MapEventSide winners = mapEvent.GetMapEventSide(mapEvent.WinningSide);
            MapEventSide losers = mapEvent.GetMapEventSide(mapEvent.DefeatedSide);
            if (winners == null || losers == null) return 0;
            if (winners.Parties == null || losers.Parties == null) return 0;

            BattleRewardModel model = Campaign.Current.Models.BattleRewardModel;
            if (model == null) return 0;

            int taken = 0;

            for (int i = 0; i < losers.Parties.Count; i++)
            {
                PartyBase loser = losers.Parties[i].Party;
                if (loser == null || loser.ItemRoster == null) continue;

                List<ItemRosterElement> stranded = Stranded(loser.ItemRoster);
                if (stranded.Count == 0) continue;

                // Asked once per defeated party, as the game asks it: the
                // shares depend on who was beaten, not only on who won.
                List<KeyValuePair<MapEventParty, float>> shares =
                    model.GetLootItemChancesForWinnerParties(winners.Parties, loser);
                if (shares == null || shares.Count == 0) continue;

                for (int s = 0; s < stranded.Count; s++)
                {
                    EquipmentElement element = stranded[s].EquipmentElement;

                    // One draw per piece rather than one for the lot. A pile
                    // taken off three lords should be able to end up in three
                    // saddlebags, which is how the ordinary spoils behave.
                    for (int n = 0; n < stranded[s].Amount; n++)
                    {
                        MapEventParty taker = Draw(shares);
                        if (taker == null) break;

                        ItemRoster pile = taker.RosterToReceiveLootItems;
                        if (pile == null) continue;

                        loser.ItemRoster.AddToCounts(element, -1);
                        pile.AddToCounts(element, 1);
                        taken++;
                    }
                }
            }

            if (taken > 0)
            {
                ModLog.Info("STOLENRECOVERED battle=" + mapEvent.EventType + " pieces=" + taken);
            }

            return taken;
        }

        /// <summary>
        /// One winner, drawn against the weights the reward model returned.
        ///
        /// The model gives a chance per party rather than a share that sums to
        /// one -- Roguery bonuses are multiplied in afterwards, so the numbers
        /// do not add up to anything in particular. Normalising by their total
        /// is therefore the only reading that makes sense of them as a
        /// division, and it keeps the relative weighting the model intended.
        /// </summary>
        private static MapEventParty Draw(List<KeyValuePair<MapEventParty, float>> shares)
        {
            float total = 0f;
            MapEventParty last = null;

            for (int i = 0; i < shares.Count; i++)
            {
                if (shares[i].Value <= 0f) continue;
                total += shares[i].Value;
                last = shares[i].Key;
            }

            if (last == null) return null;

            float roll = MBRandom.RandomFloat * total;
            for (int i = 0; i < shares.Count; i++)
            {
                if (shares[i].Value <= 0f) continue;
                roll -= shares[i].Value;
                if (roll <= 0f) return shares[i].Key;
            }

            // Floating point can leave a sliver unspent. The last party holding
            // a positive weight is the honest answer to that, not an error.
            return last;
        }

        /// <summary>
        /// The lines in a baggage train that the game has no way of moving on.
        ///
        /// Exactly the test MapEvent.LootDefeatedPartyItems uses to decide what
        /// it will NOT take, inverted -- so what this handles and what vanilla
        /// handles are two halves of one whole, with nothing counted twice and
        /// nothing missed. Quest items and banners are left where they are:
        /// they are stranded by design, not by accident.
        /// </summary>
        private static List<ItemRosterElement> Stranded(ItemRoster roster)
        {
            List<ItemRosterElement> found = new List<ItemRosterElement>();

            for (int i = 0; i < roster.Count; i++)
            {
                ItemRosterElement element = roster.GetElementCopyAtIndex(i);
                if (element.Amount <= 0) continue;

                ItemObject item = element.EquipmentElement.Item;
                if (item == null || !item.NotMerchandise) continue;
                if (element.EquipmentElement.IsQuestItem || item.IsBannerItem) continue;

                found.Add(element);
            }

            return found;
        }
    }
}
