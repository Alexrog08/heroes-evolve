using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// One lord, one town, and whatever he can afford there.
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
    /// Gear a merchant would not have sold him is never taken off him either.
    /// What TaleWorlds hung on a specific character is that character -- and
    /// unlike an ordinary sword, it cannot be bought back once it is gone. See
    /// ItemCatalog.IsIrreplaceable.
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
            public bool WornOwnCulture;

            /// <summary>
            /// Whole tiers gained, as this hero values them. The reason this
            /// slot beats another.
            ///
            /// Effective on both sides, so the culture preference competes on
            /// the term the ordering actually leads on. Left raw, a foreign
            /// piece one tier better would outrank a same-tier piece in his own
            /// colours every time, and the preference would never once decide
            /// anything -- see MarketRules.CulturePreference.
            /// </summary>
            public int Gain
            {
                get
                {
                    return MarketRules.EffectiveTier(Offer.Tier, Offer.OwnCulture)
                         - MarketRules.EffectiveTier(WornTier, WornOwnCulture);
                }
            }

            /// <summary>The same gain in hundredths, which breaks ties.</summary>
            public int FineGain
            {
                get
                {
                    return MarketRules.Effective(Offer.FineTier, Offer.OwnCulture)
                         - MarketRules.Effective(WornFine, WornOwnCulture);
                }
            }
        }

        /// <summary>
        /// The single best thing this hero could buy here, or null.
        ///
        /// The biggest gap first, which is the same discipline the skill growth
        /// uses: chase the shortfall, not the average. Shop calls this until it
        /// comes back empty, so a lord closes his worst gap, then his next
        /// worst, for as long as the town and his purse allow.
        /// </summary>
        public static Candidate Best(Hero hero, Settlement settlement, int ceiling, BudgetService budget)
        {
            if (settlement == null) return null;

            // What one purchase may cost him. Read once: it does not move until
            // something is actually bought.
            int limit = budget == null ? int.MaxValue : budget.Available(hero);

            return Best(hero, settlement, MarketScanner.Stock(settlement), ceiling, limit);
        }

        /// <summary>
        /// The best thing in a given pile that would improve a slot he already
        /// fills, or null.
        ///
        /// Split out so a town's shelves and a captor's saddlebags run through
        /// the same rules. A null settlement means nothing is for sale and the
        /// pile is his own, which is what taking a prisoner's kit leaves him
        /// with; a limit of int.MaxValue means nothing is being charged.
        /// </summary>
        public static Candidate Best(Hero hero, Settlement settlement, List<StockEntry> stock,
                                     int ceiling, int limit)
        {
            if (hero == null || hero.BattleEquipment == null) return null;
            if (stock == null || stock.Count == 0) return null;

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

                if (ItemCatalog.IsIrreplaceable(worn)) continue;

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
                                                                 ceiling, FineOf(worn),
                                                                 ItemCatalog.IsOwnCulture(worn, culture),
                                                                 skills, mounted, partner,
                                                                 prefersBlunt);
                best = Better(best, slot, offers, worn, culture, limit);
            }

            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                ItemObject worn = hero.BattleEquipment[slot].Item;
                if (worn == null) continue;
                if (ItemCatalog.IsIrreplaceable(worn)) continue;

                List<MarketOffer> offers = MarketScanner.Armor(stock, settlement, hero, worn.ItemType,
                                                               culture, ceiling, FineOf(worn),
                                                               ItemCatalog.IsOwnCulture(worn, culture));
                best = Better(best, slot, offers, worn, culture, limit);
            }

            ItemObject mount = hero.BattleEquipment[EquipmentIndex.Horse].Item;
            if (mount != null)
            {
                if (!ItemCatalog.IsIrreplaceable(mount))
                {
                    List<MarketOffer> mounts = MarketScanner.Mounts(stock, settlement, hero, culture,
                                                                    ceiling, FineOf(mount),
                                                                    ItemCatalog.IsOwnCulture(mount, culture),
                                                                    skills);
                    best = Better(best, EquipmentIndex.Horse, mounts, mount, culture, limit);
                }

                // The harness hangs off the mount only for the family match -- a
                // saddle cut for a horse cannot dress a camel. It is a separate
                // slot, so a lord riding a noble horse he may not sell can still
                // buy a better saddle for it. Nesting this inside the mount's
                // own guard froze both, and noble horses are common enough that
                // it would have frozen most of the map's saddles.
                ItemObject harness = hero.BattleEquipment[EquipmentIndex.HorseHarness].Item;
                if (harness != null && !ItemCatalog.IsIrreplaceable(harness))
                {
                    List<MarketOffer> harnesses = MarketScanner.Harnesses(stock, settlement, hero, mount,
                                                                          culture, ceiling, FineOf(harness),
                                                                          ItemCatalog.IsOwnCulture(harness, culture));
                    best = Better(best, EquipmentIndex.HorseHarness, harnesses, harness, culture, limit);
                }
            }

            return best;
        }

        /// <summary>
        /// Buys what this town has for him, working out his ceiling from the
        /// same weights the repair uses. Returns how many pieces he bought.
        /// </summary>
        public static int Shop(Hero hero, Settlement settlement, BudgetService budget,
                               float clanWeight, float skillWeight, int minimumTier)
        {
            if (hero == null) return 0;

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
        public static int Shop(Hero hero, Settlement settlement, int ceiling, BudgetService budget)
        {
            int bought = 0;

            // He shops until there is nothing here worth buying or nothing left
            // to buy it with. There used to be a hard stop at one item, and it
            // was the wrong instrument: gradual improvement is supposed to come
            // from a market that does not stock everything, a purse that does
            // not stretch, and a skill ceiling that rises slowly -- three real
            // constraints that were already doing the work. A counter on top of
            // them just made a man who could afford a helmet walk out without
            // one because he had already bought boots.
            //
            // Bounded by the number of slots because that is genuinely all he
            // can wear, and because a loop that buys is a loop that must be
            // provably finite whatever the market does.
            for (int pass = 0; pass < MaximumPurchasesPerTrip; pass++)
            {
                // Asked again each time rather than ranked once. The affordable
                // limit shrinks with every purchase -- Better picks the best
                // offer UNDER it -- so a list chosen against the opening budget
                // would keep proposing what he could afford before he started
                // spending, and skip what he can afford now.
                Candidate candidate = Best(hero, settlement, ceiling, budget);
                if (candidate == null) break;

                string failure;
                if (!PurchaseService.TryBuy(hero, settlement, candidate.Slot, candidate.Offer,
                                            budget, out failure))
                {
                    // Not logged at Info: a lord walking past a sword he cannot
                    // afford is the ordinary case, and six hundred lords doing
                    // it daily would bury the file. The diagnostic command
                    // reports it on demand. Stop rather than continue -- the
                    // same candidate would be chosen again next pass.
                    break;
                }

                bought++;
            }

            return bought;
        }

        /// <summary>
        /// The ceiling on one shopping trip: eleven, every slot a lord has.
        ///
        /// Not a balance figure. Nothing in the rules can offer him a twelfth
        /// purchase -- each one improves a slot he already fills, and improving
        /// it puts what he now wears beyond the reach of anything cheaper on
        /// the same shelf -- so this is the bound that makes the loop provably
        /// terminate, not a limit anybody is expected to hit.
        /// </summary>
        private const int MaximumPurchasesPerTrip = 11;

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
                                        List<MarketOffer> offers, ItemObject worn,
                                        CultureObject culture, int limit)
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
            bool wornOwn = ItemCatalog.IsOwnCulture(worn, culture);

            MarketOffer offer = offers[chosen];
            int gain = MarketRules.EffectiveTier(offer.Tier, offer.OwnCulture)
                     - MarketRules.EffectiveTier(wornTier, wornOwn);
            int fineGain = MarketRules.Effective(offer.FineTier, offer.OwnCulture)
                         - MarketRules.Effective(wornFine, wornOwn);

            if (best != null
                && MarketRules.Compare(gain, offer.OwnClass, fineGain, offer.Price,
                                       best.Gain, best.Offer.OwnClass, best.FineGain, best.Offer.Price) >= 0)
            {
                return best;
            }

            Candidate candidate = new Candidate();
            candidate.Slot = slot;
            candidate.Offer = offer;
            candidate.WornTier = wornTier;
            candidate.WornFine = wornFine;
            candidate.WornOwnCulture = wornOwn;
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
