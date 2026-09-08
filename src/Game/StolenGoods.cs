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
        /// the first market. The town pays out of its own purse, so a poor town
        /// buys less, and the item lands on the shelf where anybody may find it.
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

                    // The town pays what it holds and no more. A market that has
                    // spent its purse takes the goods cheap, exactly as it does
                    // for anything else sold into it.
                    if (price > component.Gold) price = component.Gold;

                    baggage.AddToCounts(element, -1);
                    shelf.AddToCounts(element, 1);

                    if (price > 0 && seller != null)
                    {
                        GiveGoldAction.ApplyForSettlementToCharacter(settlement, seller, price, true);
                    }

                    sold++;
                }
            }

            ModLog.Info("STOLENSOLD party=" + party.Name + " town=" + settlement.Name + " pieces=" + sold);
            return sold;
        }

        /// <summary>
        /// Hands the losers' stranded goods to the winner, which is what the
        /// game would already do if its loot filter let it.
        ///
        /// Only the items vanilla refuses to move. Everything else in that
        /// baggage train has already been distributed by
        /// MapEvent.LootDefeatedPartyItems, by its own rules and its own
        /// chances; this does not touch it, and does not try to be a second
        /// loot system.
        ///
        /// All of it to the winning leader rather than shared out. A stolen
        /// helm is one object and the man who took the field takes it -- and
        /// when that man is the player it goes through his party like any other
        /// spoil, which is the whole point of opening this door.
        /// </summary>
        public static int Recover(MapEvent mapEvent)
        {
            if (mapEvent == null || !mapEvent.HasWinner) return 0;

            PartyBase winner = mapEvent.GetLeaderParty(mapEvent.WinningSide);
            if (winner == null || winner.ItemRoster == null) return 0;

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
                    winner.ItemRoster.AddToCounts(element, amount);
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
