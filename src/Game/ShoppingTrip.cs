using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// One lord, one town, one purchase.
    ///
    /// This is the class that keeps the promise the whole mod is built on: it
    /// reads the slots the hero already fills and looks for something better of
    /// the same kind. It never runs the planner, never consults his skills to
    /// decide what he ought to carry, and never touches an empty slot. A
    /// companion equipped as a foot archer is still a foot archer in forty
    /// years. Never a spear.
    ///
    /// That is the difference from Lords Gear, and the reason this mod exists:
    /// it re-equipped heroes from their skills whenever it felt like it, so the
    /// player lost control of his own companions the moment they left his party.
    /// </summary>
    public static class ShoppingTrip
    {
        /// <summary>
        /// A slot worth spending on: what is worn there now, what the town has,
        /// and how much better it is.
        /// </summary>
        public sealed class Candidate
        {
            public EquipmentIndex Slot;
            public MarketOffer Offer;
            public int WornTier;

            /// <summary>Tiers gained. The reason this slot beats another.</summary>
            public int Gain
            {
                get { return Offer.Tier - WornTier; }
            }
        }

        /// <summary>
        /// The single best thing this hero could buy here, or null.
        ///
        /// One purchase per visit, and the biggest gap first. Buying a whole kit
        /// in one afternoon would undo the point of making lords earn their
        /// gear, and closing the worst gap first is the same discipline the
        /// skill growth uses: chase the shortfall, not the average.
        /// </summary>
        public static Candidate Best(Hero hero, Settlement settlement, int ceiling)
        {
            if (hero == null || hero.BattleEquipment == null || settlement == null) return null;

            List<ItemRosterElement> stock = MarketScanner.Stock(settlement);
            if (stock.Count == 0) return null;

            CultureObject culture = hero.Culture;
            if (culture == null && hero.Clan != null) culture = hero.Clan.Culture;

            SkillProfile skills = HeroAdapter.ReadSkills(hero);
            SlotSnapshot current = HeroAdapter.ReadEquipment(hero.BattleEquipment);
            bool mounted = hero.BattleEquipment[EquipmentIndex.Horse].Item != null;

            Candidate best = null;

            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                EquipmentIndex slot = SlotMapping.WeaponSlot(i);
                ItemObject worn = hero.BattleEquipment[slot].Item;
                if (worn == null) continue;

                // The dummy spatha is the bug's own marker, not a choice. The
                // repair replaces it; buying a better one would cement it.
                if (HeroAdapter.IsVanillaDummySword(worn)) continue;

                WeaponCategory category = ItemClassifier.Classify(worn);
                if (category == WeaponCategory.None) continue;

                WeaponCategory partner = CategoryRules.TwoHandedPartner(category);
                if (partner != WeaponCategory.None && !current.Contains(partner))
                {
                    partner = WeaponCategory.None;
                }

                List<MarketOffer> offers = MarketScanner.Weapons(stock, settlement, hero, category, culture,
                                                                 ceiling, TierOf(worn), skills, mounted, partner);
                best = Better(best, slot, offers, TierOf(worn));
            }

            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                ItemObject worn = hero.BattleEquipment[slot].Item;
                if (worn == null) continue;

                List<MarketOffer> offers = MarketScanner.Armor(stock, settlement, hero, worn.ItemType,
                                                               culture, ceiling, TierOf(worn));
                best = Better(best, slot, offers, TierOf(worn));
            }

            ItemObject mount = hero.BattleEquipment[EquipmentIndex.Horse].Item;
            if (mount != null)
            {
                List<MarketOffer> mounts = MarketScanner.Mounts(stock, settlement, hero, culture,
                                                                ceiling, TierOf(mount), skills);
                best = Better(best, EquipmentIndex.Horse, mounts, TierOf(mount));

                ItemObject harness = hero.BattleEquipment[EquipmentIndex.HorseHarness].Item;
                if (harness != null)
                {
                    List<MarketOffer> harnesses = MarketScanner.Harnesses(stock, settlement, hero, mount,
                                                                          culture, ceiling, TierOf(harness));
                    best = Better(best, EquipmentIndex.HorseHarness, harnesses, TierOf(harness));
                }
            }

            return best;
        }

        /// <summary>
        /// Buys the best thing on offer, working out the hero's ceiling from the
        /// same weights the repair uses. Returns true when gold actually moved.
        /// </summary>
        public static bool Shop(Hero hero, Settlement settlement, BudgetService budget,
                                float clanWeight, float skillWeight, int minimumTier)
        {
            if (hero == null) return false;

            int ceiling = HeroAdapter.ReadCeiling(hero, HeroAdapter.ReadSkills(hero),
                                                  clanWeight, skillWeight, minimumTier);
            return Shop(hero, settlement, ceiling, budget);
        }

        /// <summary>
        /// Buys the best thing on offer, if there is one and it can be paid for.
        /// Returns true when gold actually moved.
        /// </summary>
        public static bool Shop(Hero hero, Settlement settlement, int ceiling, BudgetService budget)
        {
            Candidate candidate = Best(hero, settlement, ceiling);
            if (candidate == null) return false;

            string failure;
            if (PurchaseService.TryBuy(hero, settlement, candidate.Slot, candidate.Offer, budget, out failure))
            {
                return true;
            }

            // Not logged at Info: a lord walking past a sword he cannot afford
            // is the ordinary case, and six hundred lords doing it daily would
            // bury the file. The diagnostic command reports it on demand.
            return false;
        }

        /// <summary>
        /// Keeps whichever of two slots is the better buy. The offers arrive
        /// already sorted best-first, so only the head of the list can win.
        /// </summary>
        private static Candidate Better(Candidate best, EquipmentIndex slot,
                                        List<MarketOffer> offers, int wornTier)
        {
            if (offers == null || offers.Count == 0) return best;

            MarketOffer offer = offers[0];
            if (best != null
                && MarketRules.Compare(offer.Tier - wornTier, offer.OwnClass, offer.Price,
                                       best.Gain, best.Offer.OwnClass, best.Offer.Price) >= 0)
            {
                return best;
            }

            Candidate candidate = new Candidate();
            candidate.Slot = slot;
            candidate.Offer = offer;
            candidate.WornTier = wornTier;
            return candidate;
        }

        /// <summary>1-based tier, as the ceiling speaks it.</summary>
        private static int TierOf(ItemObject item)
        {
            return (int)item.Tier + 1;
        }
    }
}
