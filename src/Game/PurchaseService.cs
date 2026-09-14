using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// The transaction: one item, out of a town's stock and onto a lord, with
    /// the gold actually moving.
    ///
    /// Nothing here is created or destroyed. The item leaves the settlement's
    /// roster and the displaced piece goes back into it; the gold moves between
    /// the hero, his clan leader and the town through the game's own
    /// GiveGoldAction. A mod that mints equipment out of nothing is why the
    /// existing ones distort a campaign's economy, and it is the one thing this
    /// engine must not do.
    ///
    /// It also never decides what a hero should be. The caller names the slot
    /// and the offer; this only moves them. Repair decides what you are, buying
    /// decides how good you are.
    /// </summary>
    public static class PurchaseService
    {
        /// <summary>
        /// Buys one offer for one slot. Returns false with a reason when
        /// anything stops it, having changed nothing.
        ///
        /// The reason is not decoration: without it a campaign shows only that
        /// nobody bought anything, and "did not buy because it should not" is
        /// indistinguishable from "did not buy because it is broken". That
        /// confusion cost this project four bugs already.
        /// </summary>
        public static bool TryBuy(Hero hero, Settlement settlement, EquipmentIndex slot,
                                  MarketOffer offer, BudgetService budget, out string failure)
        {
            failure = null;

            if (hero == null || hero.BattleEquipment == null) { failure = "no hero"; return false; }
            if (settlement == null) { failure = "no settlement"; return false; }
            if (offer.Item == null) { failure = "no item"; return false; }
            if (budget == null) { failure = "no budget"; return false; }

            ItemRoster roster = settlement.ItemRoster;
            SettlementComponent component = settlement.SettlementComponent;
            if (roster == null || component == null) { failure = "no market"; return false; }

            // The exact entry, modifier included: a fine sword and a rusty one
            // are the same ItemObject at different prices, and the hero must be
            // handed the one that was priced for him.
            int index = roster.FindIndexOfElement(offer.Element);
            if (index < 0) { failure = "gone from stock"; return false; }
            if (roster.GetElementNumber(index) <= 0) { failure = "out of stock"; return false; }

            int heroPart, clanPart;
            if (!budget.TrySplit(hero, offer.Price, out heroPart, out clanPart))
            {
                failure = "cannot afford " + offer.Price;
                return false;
            }

            Hero leader = hero.Clan != null ? hero.Clan.Leader : null;
            if (clanPart > 0 && leader == null) { failure = "no clan to pay the clan share"; return false; }

            EquipmentElement displaced = hero.BattleEquipment[slot];

            // Whether the shelf was actually touched. Without this the rollback
            // below could put back an item it never took, which would mint one
            // out of nothing -- the single thing this whole service exists to
            // avoid -- if the removal were what threw.
            bool removed = false;

            try
            {
                roster.AddToCounts(offer.Element, -1);
                removed = true;

                if (heroPart > 0) GiveGoldAction.ApplyForCharacterToSettlement(hero, settlement, heroPart, true);
                if (clanPart > 0) GiveGoldAction.ApplyForCharacterToSettlement(leader, settlement, clanPart, true);

                hero.BattleEquipment[slot] = offer.Element;
            }
            catch (System.Exception error)
            {
                // Put the shelf back the way it was and leave the hero as he
                // was. Gold already moved stays moved -- it went to the town,
                // not into nothing -- and the log says so rather than hiding it.
                if (removed) roster.AddToCounts(offer.Element, 1);
                hero.BattleEquipment[slot] = displaced;
                failure = "transaction failed: " + error.Message;
                ModLog.Info("BUY FAILED hero=" + hero.Name + " slot=" + SlotMapping.NameOf(slot)
                            + " item=" + offer.Item.StringId + " reason=" + error.Message);
                return false;
            }

            int refundedToClan = SellBack(hero, settlement, component, roster, displaced,
                                          heroPart, clanPart, offer.Price);

            budget.Record(hero, clanPart - refundedToClan);

            ModLog.Info("BUY hero=" + hero.Name
                        + " slot=" + SlotMapping.NameOf(slot)
                        + " item=" + offer.Item.StringId
                        + " tier=" + offer.Tier
                        + " price=" + offer.Price
                        + " hero=" + heroPart + " clan=" + clanPart
                        + " sold=" + (displaced.Item != null ? displaced.Item.StringId : "<nothing>"));

            PurchaseWatch.RecordPurchase(hero, slot, displaced.Item, offer.Item);

            return true;
        }

        /// <summary>
        /// Whether the town still holds the exact entry an offer was priced
        /// from.
        ///
        /// A trip reads the shelves once, so a second slot can be offered the
        /// unit the first has just bought. TryBuy refuses that as well, but a
        /// refusal ends the trip; asking first lets the trip close that one
        /// slot and carry on.
        /// </summary>
        internal static bool InStock(Settlement settlement, MarketOffer offer)
        {
            if (settlement == null || offer.Item == null) return false;

            ItemRoster roster = settlement.ItemRoster;
            if (roster == null) return false;

            int index = roster.FindIndexOfElement(offer.Element);
            return index >= 0 && roster.GetElementNumber(index) > 0;
        }

        /// <summary>
        /// Puts the displaced piece back on the town's shelf and pays for it,
        /// returning the part of that money the clan got back.
        ///
        /// Split in the same proportion the purchase was, so a lord whose house
        /// covered sixty percent does not pocket the whole resale: that would
        /// quietly move wealth from the leader to every lord under him, once per
        /// purchase, for the length of a campaign.
        ///
        /// The town pays only what it holds. A settlement that has run its purse
        /// down buys the item for less, or for nothing -- which is how the game
        /// itself treats a merchant with no money, and better than inventing
        /// gold to keep the books tidy.
        /// </summary>
        private static int SellBack(Hero hero, Settlement settlement, SettlementComponent component,
                                    ItemRoster roster, EquipmentElement displaced,
                                    int heroPart, int clanPart, int price)
        {
            if (displaced.Item == null) return 0;

            roster.AddToCounts(displaced, 1);

            MobileParty party = hero.PartyBelongedTo;
            int resale = component.GetItemPrice(displaced, party, true);
            if (resale <= 0) return 0;

            int merchantGold = component.Gold;
            if (resale > merchantGold) resale = merchantGold;
            if (resale <= 0) return 0;

            int toHero = price > 0 ? (int)((long)resale * heroPart / price) : resale;
            int toClan = resale - toHero;

            if (toHero > 0) GiveGoldAction.ApplyForSettlementToCharacter(settlement, hero, toHero, true);

            Hero leader = hero.Clan != null ? hero.Clan.Leader : null;
            if (toClan > 0 && leader != null)
            {
                GiveGoldAction.ApplyForSettlementToCharacter(settlement, leader, toClan, true);
                return toClan;
            }

            // No leader to receive the clan's share: it goes to the hero rather
            // than evaporating, since the item did leave his hands.
            if (toClan > 0) GiveGoldAction.ApplyForSettlementToCharacter(settlement, hero, toClan, true);
            return 0;
        }
    }
}
