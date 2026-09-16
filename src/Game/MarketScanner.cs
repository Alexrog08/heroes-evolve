using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using HeroesEvolve.Core;

namespace HeroesEvolve
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
        /// The settlement's sellable stock, read and measured once per visit.
        ///
        /// Read once and passed to each slot's scan rather than re-read per
        /// slot: a hero has eleven slots, and this runs on every lord entering
        /// every town. Empty entries are dropped here so the scans below never
        /// see them, and so are damaged pieces -- see ItemGrade.IsDamaged.
        ///
        /// The tiers are worked out here too, which is what makes the scans
        /// cheap -- see StockEntry for why they are anything but free to ask.
        /// </summary>
        public static List<StockEntry> Stock(Settlement settlement)
        {
            return Stock(settlement != null ? settlement.ItemRoster : null);
        }

        /// <summary>
        /// The same measuring, for any roster at all.
        ///
        /// A town's shelves are one caller; a captor's saddlebags after he has
        /// stripped a prisoner are the other. What a lord may put on out of his
        /// own loot is the same question as what he may buy, minus the money,
        /// so it is asked with the same code rather than a second copy that
        /// would drift.
        /// </summary>
        public static List<StockEntry> Stock(ItemRoster roster)
        {
            List<StockEntry> stock = new List<StockEntry>();
            if (roster == null) return stock;

            for (int i = 0; i < roster.Count; i++)
            {
                ItemRosterElement element = roster.GetElementCopyAtIndex(i);
                if (element.Amount <= 0) continue;

                ItemObject item = element.EquipmentElement.Item;
                if (item == null) continue;

                // Rusty, bent, worn: never an upgrade, however well the item
                // itself scores. Dropped here, so a purchase, a captor's pick from
                // his loot and every census dry run all see the same shelf.
                if (ItemGrade.IsDamaged(GradeOf(element.EquipmentElement))) continue;

                StockEntry entry = new StockEntry();
                entry.Element = element.EquipmentElement;
                entry.Item = item;
                entry.Type = item.ItemType;
                // One tier read for both numbers: Tierf is not cached, and
                // asking the item twice ran the game's tier model twice per line.
                int tier = TierOf(item);
                entry.Tier = tier;
                entry.FineTier = QualityValue.Apply(FineTierOf(item, tier),
                                                    QualityValueOf(element.EquipmentElement));
                stock.Add(entry);
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
        public static List<MarketOffer> Weapons(List<StockEntry> stock, Settlement settlement, Hero hero,
                                                WeaponCategory category, CultureObject culture,
                                                int ceiling, int wornFine, bool wornOwnCulture,
                                                SkillProfile skills, bool mounted,
                                                WeaponCategory avoidAlsoServing, bool preferAxeOrMace)
        {
            List<MarketOffer> offers = new List<MarketOffer>();
            if (stock == null || category == WeaponCategory.None) return offers;

            for (int i = 0; i < stock.Count; i++)
            {
                StockEntry entry = stock[i];
                bool own = ItemCatalog.IsOwnCulture(entry.Item, culture);
                if (!MarketRules.IsUpgrade(wornFine, wornOwnCulture, entry.FineTier, own,
                                           entry.Tier, ceiling)) continue;

                ItemObject item = entry.Item;
                if (!ItemCatalog.IsEligible(item, category, culture, ceiling, skills, hero, mounted, false)) continue;
                if (avoidAlsoServing != WeaponCategory.None
                    && ItemClassifier.AlsoServesTwoHanded(item, avoidAlsoServing)) continue;

                WeaponCategory offered = ItemClassifier.Classify(item);
                bool favoured = preferAxeOrMace
                    ? CategoryRules.IsAxeOrMace(offered)
                    : offered == category;

                Offer(offers, settlement, hero, entry, favoured, own);
            }

            offers.Sort(MarketOfferOrder.Instance);
            return offers;
        }

        /// <summary>Offers that would upgrade one armour slot.</summary>
        public static List<MarketOffer> Armor(List<StockEntry> stock, Settlement settlement, Hero hero,
                                              ItemObject.ItemTypeEnum wanted, CultureObject culture,
                                              int ceiling, int wornFine, bool wornOwnCulture)
        {
            List<MarketOffer> offers = new List<MarketOffer>();
            if (stock == null) return offers;

            for (int i = 0; i < stock.Count; i++)
            {
                StockEntry entry = stock[i];
                if (entry.Type != wanted) continue;
                bool own = ItemCatalog.IsOwnCulture(entry.Item, culture);
                if (!MarketRules.IsUpgrade(wornFine, wornOwnCulture, entry.FineTier, own,
                                           entry.Tier, ceiling)) continue;
                if (!ItemCatalog.PassesMarketFilters(entry.Item, culture, ceiling)) continue;

                Offer(offers, settlement, hero, entry, true, own);
            }

            offers.Sort(MarketOfferOrder.Instance);
            return offers;
        }

        /// <summary>
        /// Offers that would upgrade the hero's mount. Pack animals are excluded
        /// by the same test the grant uses -- a lord who owns a warhorse must
        /// not be sold a mule, however high its tier.
        /// </summary>
        public static List<MarketOffer> Mounts(List<StockEntry> stock, Settlement settlement, Hero hero,
                                               CultureObject culture, int ceiling, int wornFine,
                                               bool wornOwnCulture, SkillProfile skills)
        {
            List<MarketOffer> offers = new List<MarketOffer>();
            if (stock == null) return offers;

            for (int i = 0; i < stock.Count; i++)
            {
                StockEntry entry = stock[i];
                if (entry.Type != ItemObject.ItemTypeEnum.Horse) continue;
                bool own = ItemCatalog.IsOwnCulture(entry.Item, culture);
                if (!MarketRules.IsUpgrade(wornFine, wornOwnCulture, entry.FineTier, own,
                                           entry.Tier, ceiling)) continue;

                ItemObject item = entry.Item;
                if (!ItemCatalog.IsWarMount(item)) continue;
                if (!ItemCatalog.PassesMarketFilters(item, culture, ceiling)) continue;
                if (!ItemClassifier.MeetsDifficulty(item, skills)) continue;

                Offer(offers, settlement, hero, entry, true, own);
            }

            offers.Sort(MarketOfferOrder.Instance);
            return offers;
        }

        /// <summary>
        /// Offers that would upgrade the harness, matched to the family of the
        /// mount the hero is actually riding: a harness modelled for a horse
        /// cannot dress a camel.
        /// </summary>
        public static List<MarketOffer> Harnesses(List<StockEntry> stock, Settlement settlement, Hero hero,
                                                  ItemObject mount, CultureObject culture, int ceiling,
                                                  int wornFine, bool wornOwnCulture)
        {
            List<MarketOffer> offers = new List<MarketOffer>();
            if (stock == null) return offers;
            if (mount == null || !mount.HasHorseComponent || mount.HorseComponent.Monster == null) return offers;

            int family = mount.HorseComponent.Monster.FamilyType;

            for (int i = 0; i < stock.Count; i++)
            {
                StockEntry entry = stock[i];
                if (entry.Type != ItemObject.ItemTypeEnum.HorseHarness) continue;
                bool own = ItemCatalog.IsOwnCulture(entry.Item, culture);
                if (!MarketRules.IsUpgrade(wornFine, wornOwnCulture, entry.FineTier, own,
                                           entry.Tier, ceiling)) continue;

                ItemObject item = entry.Item;
                if (!item.HasArmorComponent || item.ArmorComponent.FamilyType != family) continue;
                if (!ItemCatalog.PassesMarketFilters(item, culture, ceiling)) continue;

                Offer(offers, settlement, hero, entry, true, own);
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
                                  StockEntry entry, bool ownClass, bool ownCulture)
        {
            // No settlement means nobody is selling: this is a man looking
            // through what he already owns. The item's own worth stands in for
            // the price so the ordering still breaks ties the same way, and
            // nothing is charged for it.
            int price;
            if (settlement == null)
            {
                price = entry.Element.ItemValue;
                if (price < 0) price = 0;
            }
            else
            {
                SettlementComponent component = settlement.SettlementComponent;
                if (component == null) return;

                MobileParty party = hero != null ? hero.PartyBelongedTo : null;
                price = component.GetItemPrice(entry.Element, party, false);
                if (price <= 0) return;
            }

            offers.Add(new MarketOffer(entry.Element, price, entry.Tier, entry.FineTier, ownClass, ownCulture));
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
        /// How sound one piece is: its modifier's quality, or Common when it has
        /// none. See ItemGrade for the numbering.
        /// </summary>
        internal static int GradeOf(EquipmentElement element)
        {
            ItemModifier modifier = element.ItemModifier;
            return modifier != null ? (int)modifier.ItemQuality : ItemGrade.Common;
        }

        /// <summary>
        /// A piece's fine tier as the market rules weigh it: the item's own, with
        /// what its quality is worth counted in. The worn piece and the offered
        /// one are both measured this way, so a lord in a rusty coat sees a sound
        /// one of the same make as the upgrade it is.
        /// </summary>
        internal static int FineTierOf(EquipmentElement element)
        {
            ItemObject item = element.Item;
            if (item == null) return 0;
            return QualityValue.Apply(FineTierOf(item), QualityValueOf(element));
        }

        /// <summary>
        /// What a piece's quality is worth, in hundredths of a tier: armour by the
        /// game's own tier formula over the parts it covers, everything else by
        /// grade. Zero for a piece with no modifier.
        /// </summary>
        internal static int QualityValueOf(EquipmentElement element)
        {
            ItemModifier modifier = element.ItemModifier;
            ItemObject item = element.Item;
            if (modifier == null || item == null) return 0;

            if (item.HasArmorComponent)
            {
                ArmorComponent armor = item.ArmorComponent;
                return QualityValue.ForArmor((int)item.ItemType, armor.HeadArmor, armor.BodyArmor,
                                             armor.LegArmor, armor.ArmArmor, modifier.Armor);
            }

            return QualityValue.ForGrade((int)modifier.ItemQuality);
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
            return FineTierOf(item, TierOf(item));
        }

        /// <summary>The same, with the whole tier already in hand.</summary>
        private static int FineTierOf(ItemObject item, int tier)
        {
            if (tier < 1) return 0;

            int fine = (int)(item.Tierf * 100f + 0.5f);
            return fine < 1 ? 1 : fine;
        }
    }
}
