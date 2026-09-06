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
    ///
    /// Unique gear is never touched, in either direction. What TaleWorlds hung
    /// on a specific character is that character: Caladog's gilded armour and
    /// horned helm are the whole of how he reads on a battlefield, and trading
    /// them for a statistically better cuirass destroys something no amount of
    /// armour points buys back.
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
            public int WornFine;

            /// <summary>Whole tiers gained. The reason this slot beats another.</summary>
            public int Gain
            {
                get { return Offer.Tier - WornTier; }
            }

            /// <summary>The same gain in hundredths, which breaks ties.</summary>
            public int FineGain
            {
                get { return Offer.FineTier - WornFine; }
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
        public static Candidate Best(Hero hero, Settlement settlement, int ceiling, BudgetService budget)
        {
            if (hero == null || hero.BattleEquipment == null || settlement == null) return null;

            // What one purchase may cost him. Read once: it does not move until
            // something is actually bought.
            int limit = budget == null ? int.MaxValue : budget.Available(hero);

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

                if (worn.IsUniqueItem) continue;

                WeaponCategory category = ItemClassifier.Classify(worn);
                if (category == WeaponCategory.None) continue;

                WeaponCategory partner = CategoryRules.TwoHandedPartner(category);
                if (partner != WeaponCategory.None && !current.Contains(partner))
                {
                    partner = WeaponCategory.None;
                }

                // The perk outranks the sword in his hand as a statement of
                // what he fights with: he chose it, the sword was handed to him.
                bool prefersBlunt = WeaponPerks.FavoursAxeOrMace(hero, category);

                List<MarketOffer> offers = MarketScanner.Weapons(stock, settlement, hero, category, culture,
                                                                 ceiling, FineOf(worn), skills, mounted, partner,
                                                                 prefersBlunt);
                best = Better(best, slot, offers, worn, limit);
            }

            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                ItemObject worn = hero.BattleEquipment[slot].Item;
                if (worn == null) continue;
                if (worn.IsUniqueItem) continue;

                List<MarketOffer> offers = MarketScanner.Armor(stock, settlement, hero, worn.ItemType,
                                                               culture, ceiling, FineOf(worn));
                best = Better(best, slot, offers, worn, limit);
            }

            ItemObject mount = hero.BattleEquipment[EquipmentIndex.Horse].Item;
            if (mount != null && !mount.IsUniqueItem)
            {
                List<MarketOffer> mounts = MarketScanner.Mounts(stock, settlement, hero, culture,
                                                                ceiling, FineOf(mount), skills);
                best = Better(best, EquipmentIndex.Horse, mounts, mount, limit);

                ItemObject harness = hero.BattleEquipment[EquipmentIndex.HorseHarness].Item;
                if (harness != null && !harness.IsUniqueItem)
                {
                    List<MarketOffer> harnesses = MarketScanner.Harnesses(stock, settlement, hero, mount,
                                                                          culture, ceiling, FineOf(harness));
                    best = Better(best, EquipmentIndex.HorseHarness, harnesses, harness, limit);
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

        /// <summary>The best affordable buy, or null. Kept for diagnostics.</summary>
        public static Candidate Best(Hero hero, Settlement settlement, int ceiling)
        {
            return Best(hero, settlement, ceiling, null);
        }

        /// <summary>
        /// Buys the best thing on offer, if there is one and it can be paid for.
        /// Returns true when gold actually moved.
        /// </summary>
        public static bool Shop(Hero hero, Settlement settlement, int ceiling, BudgetService budget)
        {
            Candidate candidate = Best(hero, settlement, ceiling, budget);
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
        /// Keeps whichever of two slots is the better buy, considering only what
        /// the hero can pay for.
        ///
        /// The offers arrive sorted best-first, so the first one within the
        /// limit is the best affordable one and the walk stops there. Taking the
        /// head of the list unconditionally was fine while money was free; the
        /// moment a spending share binds, it would have a lord fixate on the
        /// splendid sword he cannot buy and walk out with nothing, while a
        /// perfectly good cheaper upgrade sat on the same shelf.
        /// </summary>
        private static Candidate Better(Candidate best, EquipmentIndex slot,
                                        List<MarketOffer> offers, ItemObject worn, int limit)
        {
            if (offers == null || offers.Count == 0) return best;

            int chosen = -1;
            for (int i = 0; i < offers.Count; i++)
            {
                if (offers[i].Price <= limit) { chosen = i; break; }
            }
            if (chosen < 0) return best;

            int wornTier = TierOf(worn);
            int wornFine = FineOf(worn);

            MarketOffer offer = offers[chosen];
            if (best != null
                && MarketRules.Compare(offer.Tier - wornTier, offer.OwnClass,
                                       offer.FineTier - wornFine, offer.Price,
                                       best.Gain, best.Offer.OwnClass, best.FineGain, best.Offer.Price) >= 0)
            {
                return best;
            }

            Candidate candidate = new Candidate();
            candidate.Slot = slot;
            candidate.Offer = offer;
            candidate.WornTier = wornTier;
            candidate.WornFine = wornFine;
            return candidate;
        }

        /// <summary>1-based tier, as the ceiling speaks it.</summary>
        private static int TierOf(ItemObject item)
        {
            return MarketScanner.TierOf(item);
        }

        /// <summary>The same tier in hundredths, as the upgrade rule speaks it.</summary>
        private static int FineOf(ItemObject item)
        {
            return MarketScanner.FineTierOf(item);
        }
    }
}
