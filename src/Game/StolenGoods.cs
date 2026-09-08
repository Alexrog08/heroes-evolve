using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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

        /// <summary>
        /// Adds the losers' stranded goods to the pile a winner is being
        /// handed, which is what the game would already do if its loot filter
        /// let it.
        ///
        /// Into the loot pile itself, not around it, and that is the whole
        /// reason this hangs off OnCollectLootItems. The roster the event
        /// carries is MapEventParty.RosterToReceiveLootItems, and that property
        /// is not one roster but two: for an NPC it returns the party's own
        /// ItemRoster, and for the player -- IsNpcParty is literally
        /// "Party != MainParty" -- it returns
        /// PlayerEncounter.Current.RosterToReceiveLootItems, which is the exact
        /// object DoLootInventory hands to InventoryScreenHelper.OpenScreenAsLoot.
        /// So adding here puts a recovered helm in the player's loot window
        /// beside the ordinary spoils, and in a lord's baggage when the winner
        /// is a lord, from one line of code. Vanilla mutates the same roster at
        /// the same moment: the Metallurgy perk strips modifiers off looted
        /// gear from its own handler on this event.
        ///
        /// The earlier attempt hung off MapEventEnded, which runs after the
        /// window has been built and would have dropped the gear into his
        /// baggage unannounced.
        ///
        /// Only the items vanilla refuses to move. Its filter, read off
        /// LootDefeatedPartyItems, keeps anything NotMerchandise, quest-flagged
        /// or a banner; Stranded is that test inverted, so what this handles and
        /// what vanilla handles are two halves of one whole. Everything else has
        /// already been distributed by the game's own chances and is not touched
        /// here. This is not a second loot system.
        /// </summary>
        public static int Recover(PartyBase winner, ItemRoster loot)
        {
            if (winner == null || loot == null) return 0;

            MapEvent mapEvent = winner.MapEvent;
            if (mapEvent == null || !mapEvent.HasWinner) return 0;
            if (!Claims(winner, mapEvent)) return 0;

            MapEventSide losers = mapEvent.GetMapEventSide(mapEvent.DefeatedSide);
            if (losers == null || losers.Parties == null) return 0;

            int taken = 0;

            for (int i = 0; i < losers.Parties.Count; i++)
            {
                PartyBase loser = losers.Parties[i].Party;
                if (loser == null || loser == winner || loser.ItemRoster == null) continue;

                List<ItemRosterElement> stranded = Stranded(loser.ItemRoster);
                for (int s = 0; s < stranded.Count; s++)
                {
                    EquipmentElement element = stranded[s].EquipmentElement;
                    int amount = stranded[s].Amount;

                    loser.ItemRoster.AddToCounts(element, -amount);
                    loot.AddToCounts(element, amount);
                    taken += amount;
                }
            }

            if (taken > 0)
            {
                ModLog.Info("STOLENRECOVERED winner=" + winner.Name + " pieces=" + taken);
            }

            return taken;
        }

        /// <summary>
        /// Whether this winner is the one who ends up with the stranded gear.
        ///
        /// It goes to one party rather than being shared out, because a stolen
        /// helm is one object and the man who took the field takes it. Which
        /// party is worth getting right, though, because the event fires once
        /// per winner and the first answer decides.
        ///
        /// The player, whenever he fought on the winning side. His own harness
        /// should come back to him and not to whichever ally an army happens to
        /// list first, and his party is the one the game guarantees this event
        /// fires for -- the guard in LootDefeatedPartyItems skips a winner whose
        /// loot roster is empty unless that winner is MainParty. Keying on the
        /// side's leader instead, which is what this did first, quietly robbed
        /// him again whenever he rode in somebody else's army.
        ///
        /// Otherwise the first winner handed loot takes it. Between two lords
        /// with nobody watching it hardly matters which, and the transfer
        /// empties the losers' baggage, so the next winner through finds nothing
        /// left to take twice.
        /// </summary>
        private static bool Claims(PartyBase winner, MapEvent mapEvent)
        {
            PartyBase player = PartyBase.MainParty;

            if (player != null && player.MapEvent == mapEvent
                && mapEvent.PlayerSide == mapEvent.WinningSide)
            {
                return winner == player;
            }

            return true;
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
