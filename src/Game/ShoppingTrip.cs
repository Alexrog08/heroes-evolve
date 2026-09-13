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

            // In the order a trip buys, so the census cannot report a lord about
            // to buy the gloves his coat is holding back.
            int limit = budget == null ? int.MaxValue : budget.Available(hero);
            return BestOf(Due(hero, Candidates(hero, settlement, MarketScanner.Stock(settlement),
                                               ceiling, limit), ceiling, null));
        }

        /// <summary>The best single thing in a given pile, kept for the callers
        /// that hand over a captor's saddlebags rather than a town.</summary>
        public static Candidate Best(Hero hero, Settlement settlement, List<StockEntry> stock,
                                     int ceiling, int limit)
        {
            return BestOf(Candidates(hero, settlement, stock, ceiling, limit));
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
        public static List<Candidate> Candidates(Hero hero, Settlement settlement,
                                                 List<StockEntry> stock, int ceiling, int limit)
        {
            if (hero == null || hero.BattleEquipment == null) return new List<Candidate>();
            if (stock == null || stock.Count == 0) return new List<Candidate>();

            CultureObject culture = hero.Culture;
            if (culture == null && hero.Clan != null) culture = hero.Clan.Culture;

            SkillProfile skills = HeroAdapter.ReadSkills(hero);
            SlotSnapshot current = HeroAdapter.ReadEquipment(hero.BattleEquipment);
            bool mounted = hero.BattleEquipment[EquipmentIndex.Horse].Item != null;

            List<Candidate> found = new List<Candidate>();

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
                Add(found, slot, offers, worn, culture, limit);
            }

            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                ItemObject worn = hero.BattleEquipment[slot].Item;
                if (worn == null) continue;
                if (ItemCatalog.IsIrreplaceable(worn)) continue;

                List<MarketOffer> offers = MarketScanner.Armor(stock, settlement, hero, worn.ItemType,
                                                               culture, ceiling, FineOf(worn),
                                                               ItemCatalog.IsOwnCulture(worn, culture));
                Add(found, slot, offers, worn, culture, limit);
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
                    Add(found, EquipmentIndex.Horse, mounts, mount, culture, limit);
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
                    Add(found, EquipmentIndex.HorseHarness, harnesses, harness, culture, limit);
                }
            }

            return found;
        }

        /// <summary>
        /// The single best of them, by the same comparison that used to run
        /// inline as the slots were walked.
        /// </summary>
        private static Candidate BestOf(List<Candidate> found)
        {
            Candidate best = null;
            for (int i = 0; i < found.Count; i++)
            {
                Candidate c = found[i];
                if (best != null
                    && MarketRules.Compare(c.Gain, c.Offer.OwnClass, c.FineGain, c.Offer.Price,
                                           best.Gain, best.Offer.OwnClass, best.FineGain,
                                           best.Offer.Price) >= 0)
                {
                    continue;
                }
                best = c;
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
            if (settlement == null) return 0;

            // Scanned once and reused. The shelves do not change while he is
            // standing at them, and rebuilding this list for every purchase was
            // the whole cost of letting him make more than one.
            List<StockEntry> stock = MarketScanner.Stock(settlement);
            if (stock == null || stock.Count == 0) return 0;

            // What the trip may cost, and how many ways it has to stretch.
            //
            // Counted with no price limit, so the count is of slots this town
            // could improve at all rather than of slots he happens to be able
            // to afford first. A man who needs eleven things divides by eleven
            // and comes back in middling gear; a man who needs one spends the
            // lot on it. That progression -- fewer pieces, better each time --
            // is the whole point of budgeting the trip instead of the piece,
            // and it falls out of the division without anything having to
            // decide it.
            //
            // Only slots that are due count (see Due). Armour held back behind
            // a poorer piece is not a way for this trip's money to stretch, so
            // the piece holding it back takes that share as well: that is how
            // the coat that kept losing to the gloves comes to win.
            int pot = budget == null ? int.MaxValue : budget.TripBudget(hero);

            // Slots already bought for on this trip. Struck off by name,
            // because the count is taken afresh every pass rather than ticked
            // down once per purchase.
            List<EquipmentIndex> filled = new List<EquipmentIndex>();
            int gapsAtStart = -1;

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
                // His share of what is left, for the slots that are left. A
                // piece bought under its slice leaves the remainder to the
                // others, so a cheap helmet buys a better cloak rather than
                // being quietly forfeited.
                //
                // Recounted every pass, because a purchase can open slots as
                // well as close one: once the coat catches up, the gloves that
                // were waiting on it are due, and they need a share of what is
                // left.
                int gaps = Due(hero, Candidates(hero, settlement, stock, ceiling, int.MaxValue),
                               ceiling, filled).Count;
                if (gapsAtStart < 0) gapsAtStart = gaps;
                if (gaps <= 0) break;

                int slice = pot / gaps;
                if (slice <= 0) break;

                Candidate candidate = BestOf(Due(hero, Candidates(hero, settlement, stock, ceiling, slice),
                                                 ceiling, filled));
                if (candidate == null) break;

                int price = candidate.Offer.Price;

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
                filled.Add(candidate.Slot);
                pot -= price;
                if (pot <= 0) break;
            }

            LastTripGaps = gapsAtStart > 0 ? gapsAtStart : 0;
            return bought;
        }

        /// <summary>
        /// The candidates a trip may still spend on: none for a slot already
        /// bought for on this trip, and no armour more than a tier ahead of the
        /// poorest piece he wears (see GearBalance). Weapons, the horse and its
        /// harness pass untouched.
        ///
        /// A slot bought for stays closed because the slices grow as a trip
        /// goes on. Without that, a helmet bought on a thin early share could
        /// be traded straight back in for a better one the moment a later share
        /// allowed it, and he would pay twice for one head.
        ///
        /// The poorest piece is read off what he wears, not off what this town
        /// sells. A town with no coat for him sells him none of the gloves the
        /// coat is holding back either, and he keeps the money for a town that
        /// has one.
        /// </summary>
        private static List<Candidate> Due(Hero hero, List<Candidate> found, int ceiling,
                                           List<EquipmentIndex> filled)
        {
            if (found.Count == 0) return found;

            int worst = WorstArmorShortfall(hero, ceiling);

            List<Candidate> due = new List<Candidate>(found.Count);
            for (int i = 0; i < found.Count; i++)
            {
                Candidate candidate = found[i];
                if (filled != null && filled.Contains(candidate.Slot)) continue;

                if (SlotMapping.IsArmor(candidate.Slot)
                    && !GearBalance.IsDue(ArmorShortfall(hero, candidate.Slot, ceiling), worst))
                {
                    continue;
                }

                due.Add(candidate);
            }

            return due;
        }

        /// <summary>How far behind the poorest armour he could trade is.</summary>
        private static int WorstArmorShortfall(Hero hero, int ceiling)
        {
            int worst = 0;
            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                int shortfall = ArmorShortfall(hero, slot, ceiling);
                if (shortfall > worst) worst = shortfall;
            }
            return worst;
        }

        /// <summary>
        /// Tiers one armour slot is behind the best it could hold. Zero for an
        /// empty slot and for gear the market never takes off him: neither can
        /// be bought for, so neither may be the piece the rest wait on.
        /// </summary>
        private static int ArmorShortfall(Hero hero, EquipmentIndex slot, int ceiling)
        {
            ItemObject worn = hero.BattleEquipment[slot].Item;
            if (worn == null || ItemCatalog.IsIrreplaceable(worn)) return 0;

            return GearBalance.Shortfall(TierOf(worn), ceiling, ItemCatalog.BestBuyableTier(worn.ItemType));
        }

        /// <summary>
        /// How many slots the last trip had to divide its money between, as
        /// first counted: armour waiting on a poorer piece is not among them.
        ///
        /// Reported so the log can say whether a small purchase was a lord with
        /// little to fix or a lord whose share came out too thin to buy
        /// anything with. Those look identical in a count of pieces and want
        /// opposite responses, and telling them apart took a separate
        /// measurement every time until this existed.
        ///
        /// It answered on the first campaign that logged it, and the answer was
        /// the first: of 297 trips, 262 bought every gap the lord had. The
        /// average trip filled 1.51 slots out of 1.71 available, so the budget
        /// binds on about one trip in eight and the modest piece count is a
        /// population with little left to fix rather than a purse held shut.
        /// Worth keeping in mind before anyone reads a low average as the share
        /// being too small and raises it.
        /// </summary>
        public static int LastTripGaps;

        /// <summary>
        /// The ceiling on one shopping trip: eleven, every slot a lord has.
        ///
        /// Not a balance figure. Nothing in the rules can offer him a twelfth
        /// purchase -- each one improves a slot he already fills, and a slot
        /// bought for is closed for the rest of the trip -- so this is the
        /// bound that makes the loop provably terminate, not a limit anybody
        /// is expected to hit.
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
        private static void Add(List<Candidate> found, EquipmentIndex slot,
                                List<MarketOffer> offers, ItemObject worn,
                                CultureObject culture, int limit)
        {
            if (offers == null || offers.Count == 0) return;

            // Offers arrive best-first, so this is the best he can afford here.
            // The limit chooses his quality, not his total: it is the reason a
            // trip budget has to be divided between the slots before it is
            // spent rather than handed to the first purchase whole.
            int chosen = -1;
            for (int i = 0; i < offers.Count; i++)
            {
                if (offers[i].Price <= limit) { chosen = i; break; }
            }
            if (chosen < 0) return;

            int wornTier = TierOf(worn);
            int wornFine = FineOf(worn);
            bool wornOwn = ItemCatalog.IsOwnCulture(worn, culture);

            MarketOffer offer = offers[chosen];
            int gain = MarketRules.EffectiveTier(offer.Tier, offer.OwnCulture)
                     - MarketRules.EffectiveTier(wornTier, wornOwn);
            int fineGain = MarketRules.Effective(offer.FineTier, offer.OwnCulture)
                         - MarketRules.Effective(wornFine, wornOwn);

            Candidate candidate = new Candidate();
            candidate.Slot = slot;
            candidate.Offer = offer;
            candidate.WornTier = wornTier;
            candidate.WornFine = wornFine;
            candidate.WornOwnCulture = wornOwn;
            found.Add(candidate);
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
