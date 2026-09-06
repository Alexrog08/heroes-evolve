using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// What a particular town actually has on its shelves for a particular
    /// hero, priced at that town's own rates.
    ///
    /// The pool is the settlement's ItemRoster, never the global catalogue, and
    /// that is the whole point of the class. ItemCatalog scans all 3500 items
    /// deterministically and returns the same answer every time, so two lords of
    /// one culture and one ceiling would buy the identical sword for the rest of
    /// the campaign. A town's stock differs from its neighbour's, and buying
    /// removes the item from it -- so the second lord through the gate gets the
    /// next best thing. The market is where variety comes from.
    ///
    /// Nothing here decides anything: it reports what is available and what it
    /// costs. Whether a hero can afford it, and whether he should, belong to the
    /// budget and purchase services.
    /// </summary>
    public static class MarketScanner
    {
        /// <summary>
        /// The settlement's sellable stock, read once per visit.
        ///
        /// Read once and passed to each slot's scan rather than re-read per
        /// slot: a hero has eleven slots, and this runs on every lord entering
        /// every town. Empty entries are dropped here so the scans below never
        /// see them.
        /// </summary>
        public static List<ItemRosterElement> Stock(Settlement settlement)
        {
            List<ItemRosterElement> stock = new List<ItemRosterElement>();
            if (settlement == null) return stock;

            ItemRoster roster = settlement.ItemRoster;
            if (roster == null) return stock;

            for (int i = 0; i < roster.Count; i++)
            {
                ItemRosterElement element = roster.GetElementCopyAtIndex(i);
                if (element.Amount <= 0) continue;
                if (element.EquipmentElement.Item == null) continue;
                stock.Add(element);
            }

            return stock;
        }

        /// <summary>
        /// Offers that would upgrade one weapon slot: the same category the hero
        /// already carries there, a whole tier better, within his ceiling and
        /// usable with his skills.
        ///
        /// The category is the caller's -- read from what the hero is wearing,
        /// not planned afresh. A lord equipped as a crossbowman stays one.
        ///
        /// preferAxeOrMace redefines which offers count as "his own class" for
        /// the ordering. A hero holding the axes-and-maces perk has said what he
        /// fights with more deliberately than the sword in his hand does, so for
        /// him the favoured class is the axe or the mace and the sword is the
        /// drift. Without the perk it means exactly what it did: keep what he
        /// carries.
        /// </summary>
        public static List<MarketOffer> Weapons(List<ItemRosterElement> stock, Settlement settlement, Hero hero,
                                                WeaponCategory category, CultureObject culture,
                                                int ceiling, int wornFine, SkillProfile skills, bool mounted,
                                                WeaponCategory avoidAlsoServing, bool preferAxeOrMace)
        {
            List<MarketOffer> offers = new List<MarketOffer>();
            if (stock == null || category == WeaponCategory.None) return offers;

            for (int i = 0; i < stock.Count; i++)
            {
                ItemObject item = stock[i].EquipmentElement.Item;
                if (!MarketRules.IsUpgrade(wornFine, FineTierOf(item), TierOf(item), ceiling)) continue;
                if (!ItemCatalog.IsEligible(item, category, culture, ceiling, skills, hero, mounted)) continue;
                if (avoidAlsoServing != WeaponCategory.None
                    && ItemClassifier.AlsoServesTwoHanded(item, avoidAlsoServing)) continue;

                WeaponCategory offered = ItemClassifier.Classify(item);
                bool favoured = preferAxeOrMace
                    ? CategoryRules.IsAxeOrMace(offered)
                    : offered == category;

                Offer(offers, settlement, hero, stock[i], favoured);
            }

            offers.Sort(MarketOfferOrder.Instance);
            return offers;
        }

        /// <summary>Offers that would upgrade one armour slot.</summary>
        public static List<MarketOffer> Armor(List<ItemRosterElement> stock, Settlement settlement, Hero hero,
                                              ItemObject.ItemTypeEnum wanted, CultureObject culture,
                                              int ceiling, int wornFine)
        {
            List<MarketOffer> offers = new List<MarketOffer>();
            if (stock == null) return offers;

            for (int i = 0; i < stock.Count; i++)
            {
                ItemObject item = stock[i].EquipmentElement.Item;
                if (item.ItemType != wanted) continue;
                if (!MarketRules.IsUpgrade(wornFine, FineTierOf(item), TierOf(item), ceiling)) continue;
                if (!ItemCatalog.PassesCommonFilters(item, culture, ceiling)) continue;

                Offer(offers, settlement, hero, stock[i], true);
            }

            offers.Sort(MarketOfferOrder.Instance);
            return offers;
        }

        /// <summary>
        /// Offers that would upgrade the hero's mount. Pack animals are excluded
        /// by the same test the grant uses -- a lord who owns a warhorse must
        /// not be sold a mule, however high its tier.
        /// </summary>
        public static List<MarketOffer> Mounts(List<ItemRosterElement> stock, Settlement settlement, Hero hero,
                                               CultureObject culture, int ceiling, int wornFine, SkillProfile skills)
        {
            List<MarketOffer> offers = new List<MarketOffer>();
            if (stock == null) return offers;

            for (int i = 0; i < stock.Count; i++)
            {
                ItemObject item = stock[i].EquipmentElement.Item;
                if (item.ItemType != ItemObject.ItemTypeEnum.Horse) continue;
                if (!MarketRules.IsUpgrade(wornFine, FineTierOf(item), TierOf(item), ceiling)) continue;
                if (!ItemCatalog.IsWarMount(item)) continue;
                if (!ItemCatalog.PassesCommonFilters(item, culture, ceiling)) continue;
                if (!ItemClassifier.MeetsDifficulty(item, skills)) continue;

                Offer(offers, settlement, hero, stock[i], true);
            }

            offers.Sort(MarketOfferOrder.Instance);
            return offers;
        }

        /// <summary>
        /// Offers that would upgrade the harness, matched to the family of the
        /// mount the hero is actually riding: a harness modelled for a horse
        /// cannot dress a camel.
        /// </summary>
        public static List<MarketOffer> Harnesses(List<ItemRosterElement> stock, Settlement settlement, Hero hero,
                                                  ItemObject mount, CultureObject culture, int ceiling, int wornFine)
        {
            List<MarketOffer> offers = new List<MarketOffer>();
            if (stock == null) return offers;
            if (mount == null || !mount.HasHorseComponent || mount.HorseComponent.Monster == null) return offers;

            int family = mount.HorseComponent.Monster.FamilyType;

            for (int i = 0; i < stock.Count; i++)
            {
                ItemObject item = stock[i].EquipmentElement.Item;
                if (item.ItemType != ItemObject.ItemTypeEnum.HorseHarness) continue;
                if (!MarketRules.IsUpgrade(wornFine, FineTierOf(item), TierOf(item), ceiling)) continue;
                if (!item.HasArmorComponent || item.ArmorComponent.FamilyType != family) continue;
                if (!ItemCatalog.PassesCommonFilters(item, culture, ceiling)) continue;

                Offer(offers, settlement, hero, stock[i], true);
            }

            offers.Sort(MarketOfferOrder.Instance);
            return offers;
        }

        /// <summary>
        /// Prices one roster entry for this hero and records it.
        ///
        /// The price comes from the settlement rather than ItemObject.Value
        /// because that is what the hero will actually be charged: town markets
        /// price on their own supply, and the trading party's Trade skill moves
        /// the number. A non-positive price means something is wrong with the
        /// entry, not that the item is free, so it is dropped.
        /// </summary>
        private static void Offer(List<MarketOffer> offers, Settlement settlement, Hero hero,
                                  ItemRosterElement element, bool ownClass)
        {
            SettlementComponent component = settlement != null ? settlement.SettlementComponent : null;
            if (component == null) return;

            MobileParty party = hero != null ? hero.PartyBelongedTo : null;
            int price = component.GetItemPrice(element.EquipmentElement, party, false);
            if (price <= 0) return;

            offers.Add(new MarketOffer(element.EquipmentElement, price,
                                       TierOf(element.EquipmentElement.Item),
                                       FineTierOf(element.EquipmentElement.Item), ownClass));
        }

        /// <summary>
        /// 1-based tier. ItemObject.Tier is the 0-based ItemTiers enum, and the
        /// ceiling speaks 1..6; converting here keeps the two vocabularies from
        /// meeting raw, exactly as ItemCatalog does.
        /// </summary>
        internal static int TierOf(ItemObject item)
        {
            return (int)item.Tier + 1;
        }

        /// <summary>
        /// The game's fractional tier in hundredths, on the same 1-based scale:
        /// disassembled, Tier is Clamp(Round(Tierf), 0, 6) - 1, so the 1-based
        /// whole tier is exactly round(Tierf).
        ///
        /// Zero for anything the game scores below tier 1 -- consumables like
        /// naphtha pots -- so the callers' "below the scale" guard keeps working
        /// off a single number.
        /// </summary>
        internal static int FineTierOf(ItemObject item)
        {
            if (TierOf(item) < 1) return 0;

            int fine = (int)(item.Tierf * 100f + 0.5f);
            return fine < 1 ? 1 : fine;
        }
    }
}
