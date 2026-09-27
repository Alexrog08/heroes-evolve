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
            /// Every offer for this slot, best first, of which Offer is the
            /// first inside the limit the candidate was built with. Kept so a
            /// trip can choose again under a smaller share without reading the
            /// shelves again.
            /// </summary>
            public List<MarketOffer> Offers;

            /// <summary>
            /// Whole tiers gained, as this hero values them. The reason this
            /// slot beats another.
            ///
            /// Effective on both sides, so the culture preference competes on
            /// the term the ordering actually leads on. Left raw, a foreign
            /// piece one tier better would outrank a same-tier piece in his own
            /// colours every time, and the preference would never once decide
            /// anything -- see MarketRules.CultureShare.
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

            CultureObject culture = PreferredCulture(hero);

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

                int wornFine = FineOf(hero.BattleEquipment[slot]);
                int wornTier = TierOf(worn);

                // A weapon he cannot use from the saddle is worth nothing to a
                // man on a horse, so it counts as the least a piece can be and
                // the first of its kind he can use mounted replaces it -- the
                // offers are already only those (ItemCatalog.IsEligible). Only
                // while he rides: on foot a long bow is a long bow. The rags
                // no longer hand these out, but earlier robberies did, and
                // TaleWorlds' own sheets put a few on horseback.
                if (mounted && !ItemClassifier.IsUsableMounted(worn, hero))
                {
                    wornFine = 1;
                    wornTier = 1;
                }

                List<MarketOffer> offers = MarketScanner.Weapons(stock, settlement, hero, category, culture,
                                                                 ceiling, wornFine,
                                                                 ItemCatalog.IsOwnCulture(worn, culture),
                                                                 skills, mounted, partner,
                                                                 prefersBlunt);
                Add(found, slot, offers, worn, wornTier, wornFine, culture, limit);
            }

            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                ItemObject worn = hero.BattleEquipment[slot].Item;
                if (worn == null) continue;
                if (ItemCatalog.IsIrreplaceable(worn)) continue;

                int wornFine = FineOf(hero.BattleEquipment[slot]);
                List<MarketOffer> offers = MarketScanner.Armor(stock, settlement, hero, worn.ItemType,
                                                               culture, ceiling, wornFine,
                                                               ItemCatalog.IsOwnCulture(worn, culture));
                Add(found, slot, offers, worn, wornFine, culture, limit);
            }

            ItemObject mount = hero.BattleEquipment[EquipmentIndex.Horse].Item;
            if (mount != null)
            {
                if (!ItemCatalog.IsIrreplaceable(mount))
                {
                    int mountFine = FineOf(hero.BattleEquipment[EquipmentIndex.Horse]);
                    List<MarketOffer> mounts = MarketScanner.Mounts(stock, settlement, hero, culture,
                                                                    ceiling, mountFine,
                                                                    ItemCatalog.IsOwnCulture(mount, culture),
                                                                    skills);
                    Add(found, EquipmentIndex.Horse, mounts, mount, mountFine, culture, limit);
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
                    int harnessFine = FineOf(hero.BattleEquipment[EquipmentIndex.HorseHarness]);
                    int harnessTier = TierOf(harness);

                    // A saddle cut for another beast is worth nothing on this
                    // one, so it counts as the least a harness can be and the
                    // first that fits his mount replaces it. The market no
                    // longer makes these -- a lord keeps the beast he rides, see
                    // MountFamilyRules -- but saves from before are full of
                    // camels in horse harness, and TaleWorlds' own sheets may
                    // hold a few.
                    if (!MarketScanner.HarnessFits(harness, mount))
                    {
                        harnessFine = 1;
                        harnessTier = 1;
                    }

                    List<MarketOffer> harnesses = MarketScanner.Harnesses(stock, settlement, hero, mount,
                                                                          culture, ceiling, harnessFine,
                                                                          ItemCatalog.IsOwnCulture(harness, culture));
                    Add(found, EquipmentIndex.HorseHarness, harnesses, harness, harnessTier, harnessFine,
                        culture, limit);
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
            // Only slots that are due share the money (see Due). Armour held
            // back behind a poorer piece takes no share, so what it would have
            // had goes to the slots that are due -- the piece holding it back
            // among them, which is how the coat that kept losing to the gloves
            // comes to win. Whatever nothing due here can use stays in his
            // purse.
            int pot = budget == null ? int.MaxValue : budget.TripBudget(hero);

            // The slots this trip is finished with -- bought for, or found sold
            // out -- and the ones actually bought. Struck off by name, because
            // the count is taken afresh every pass rather than ticked down once
            // per purchase.
            List<EquipmentIndex> closed = new List<EquipmentIndex>();
            List<EquipmentIndex> filled = new List<EquipmentIndex>();
            bool counted = false;
            int improvable = 0, dueAtStart = 0, armorOnOffer = 0, armorHeldBack = 0;
            string heldBehind = null;

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
                // Read once a pass, with no price limit, and recounted every
                // pass because a purchase can open slots as well as close one:
                // once the coat catches up, the gloves that were waiting on it
                // are due, and they need a share of what is left.
                List<Candidate> onOffer = Candidates(hero, settlement, stock, ceiling, int.MaxValue);
                List<Candidate> due = Due(hero, onOffer, ceiling, closed);

                if (!counted)
                {
                    counted = true;
                    improvable = onOffer.Count;
                    dueAtStart = due.Count;

                    // What the armour order held back as he walked in, and
                    // behind which piece, for the census (see PurchaseWatch).
                    armorOnOffer = CountArmor(onOffer);
                    armorHeldBack = armorOnOffer - CountArmor(due);
                    if (armorHeldBack > 0)
                    {
                        EquipmentIndex behind;
                        WorstArmorShortfall(hero, ceiling, out behind);
                        heldBehind = SlotMapping.NameOf(behind);
                    }
                }

                int gaps = due.Count;
                if (gaps <= 0) break;

                // His share of what is left, for the slots that are left. A
                // piece bought under its slice leaves the remainder to the
                // others, so a cheap helmet buys a better cloak rather than
                // being quietly forfeited.
                int slice = pot / gaps;
                if (slice <= 0) break;

                Candidate candidate = BestOf(Within(due, slice));
                if (candidate == null) break;

                // The shelves were read once, so a second slot can be offered
                // the unit the first has just bought -- two quivers after the
                // town's last stack of arrows. That closes the one slot; it
                // used to end the whole trip.
                if (!PurchaseService.InStock(settlement, candidate.Offer))
                {
                    closed.Add(candidate.Slot);
                    continue;
                }

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
                closed.Add(candidate.Slot);
                filled.Add(candidate.Slot);
                pot -= price;
                if (pot <= 0) break;
            }

            LastTripGaps = improvable;
            LastTripDue = dueAtStart;
            PurchaseWatch.RecordTrip(hero, bought, armorOnOffer, armorHeldBack, heldBehind,
                                     CountArmor(filled) > 0);
            return bought;
        }

        /// <summary>
        /// The candidates a trip may still spend on: none for a slot this trip
        /// has closed, and no armour more than a tier ahead of the poorest
        /// piece he wears (see GearBalance). Weapons, the horse and its harness
        /// pass untouched.
        ///
        /// A slot bought for stays closed because the slices grow as a trip
        /// goes on. Without that, a helmet bought on a thin early share could
        /// be traded straight back in for a better one the moment a later share
        /// allowed it, and he would pay twice for one head.
        ///
        /// The poorest piece is read off what he wears, not off what this town
        /// sells. A town with no coat for him sells him none of the gloves the
        /// coat is holding back either: their share goes to whatever else is
        /// due here, and what nothing due can use stays in his purse for a town
        /// that has a coat.
        /// </summary>
        private static List<Candidate> Due(Hero hero, List<Candidate> found, int ceiling,
                                           List<EquipmentIndex> closed)
        {
            if (found.Count == 0) return found;

            EquipmentIndex worstSlot;
            int worst = WorstArmorShortfall(hero, ceiling, out worstSlot);

            List<Candidate> due = new List<Candidate>(found.Count);
            for (int i = 0; i < found.Count; i++)
            {
                Candidate candidate = found[i];
                if (closed != null && closed.Contains(candidate.Slot)) continue;

                if (SlotMapping.IsArmor(candidate.Slot)
                    && !GearBalance.IsDue(Shortfall(hero, candidate.Slot, ceiling), worst))
                {
                    continue;
                }

                due.Add(candidate);
            }

            return due;
        }

        /// <summary>
        /// The same candidates chosen again under a smaller limit: each slot's
        /// best offer that fits, and no slot at all where none does.
        ///
        /// Exactly what building them afresh at that limit would give -- the
        /// offers do not depend on the limit, only the choice among them does
        /// -- without reading the shelves a second time on every pass.
        /// </summary>
        private static List<Candidate> Within(List<Candidate> found, int limit)
        {
            List<Candidate> fitting = new List<Candidate>(found.Count);
            for (int i = 0; i < found.Count; i++)
            {
                Candidate candidate = found[i];
                List<MarketOffer> offers = candidate.Offers;
                if (offers == null) continue;

                for (int j = 0; j < offers.Count; j++)
                {
                    if (offers[j].Price > limit) continue;

                    Candidate pick = new Candidate();
                    pick.Slot = candidate.Slot;
                    pick.Offer = offers[j];
                    pick.WornTier = candidate.WornTier;
                    pick.WornFine = candidate.WornFine;
                    pick.WornOwnCulture = candidate.WornOwnCulture;
                    pick.Offers = offers;
                    fitting.Add(pick);
                    break;
                }
            }
            return fitting;
        }

        /// <summary>How far behind the poorest armour he could trade is, and which piece that is.</summary>
        private static int WorstArmorShortfall(Hero hero, int ceiling, out EquipmentIndex worstSlot)
        {
            int worst = 0;
            worstSlot = EquipmentIndex.None;
            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                int shortfall = Shortfall(hero, slot, ceiling);
                if (shortfall > worst)
                {
                    worst = shortfall;
                    worstSlot = slot;
                }
            }
            return worst;
        }

        /// <summary>
        /// Whether the armour order keeps this slot from being bought for right
        /// now, and behind which piece.
        ///
        /// For the census and the dry run. Without it both could see gloves on
        /// the shelf beside a lord who bought nothing, and could only blame
        /// the wrong thing for it.
        /// </summary>
        internal static bool IsHeldBack(Hero hero, EquipmentIndex slot, int ceiling,
                                        out EquipmentIndex behind, out int shortfall, out int worst)
        {
            behind = EquipmentIndex.None;
            shortfall = 0;
            worst = 0;
            if (hero == null || hero.BattleEquipment == null || !SlotMapping.IsArmor(slot)) return false;

            worst = WorstArmorShortfall(hero, ceiling, out behind);
            shortfall = Shortfall(hero, slot, ceiling);
            return !GearBalance.IsDue(shortfall, worst);
        }

        /// <summary>
        /// Whether anything he wears is behind the best its slot could hold, by
        /// the same Shortfall the armour order and the census use. A caravan
        /// master for whom this is false has nothing a commission could buy.
        /// </summary>
        internal static bool HasRoomToImprove(Hero hero, int ceiling)
        {
            if (hero == null || hero.BattleEquipment == null) return false;

            int[] shortfalls = new int[SlotSnapshot.WeaponSlotCount + SlotMapping.ArmorSlots.Length + 2];
            int n = 0;
            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                shortfalls[n++] = Shortfall(hero, SlotMapping.WeaponSlot(i), ceiling);
            }
            for (int i = 0; i < SlotMapping.ArmorSlots.Length; i++)
            {
                shortfalls[n++] = Shortfall(hero, SlotMapping.ArmorSlots[i], ceiling);
            }
            shortfalls[n++] = Shortfall(hero, EquipmentIndex.Horse, ceiling);
            shortfalls[n] = Shortfall(hero, EquipmentIndex.HorseHarness, ceiling);

            return GearBalance.AnythingBehind(shortfalls);
        }

        /// <summary>
        /// Tiers one slot is behind the best it could hold. Zero for an empty
        /// slot, for gear the market never takes off him, and for anything the
        /// game ranks below tier 1: none of those can be bought for, so none
        /// may be the piece the rest wait on.
        ///
        /// Internal so the census measures with this exact number rather than
        /// a copy of it: headroom for every slot, evenness for the armour.
        /// </summary>
        internal static int Shortfall(Hero hero, EquipmentIndex slot, int ceiling)
        {
            EquipmentElement piece = hero.BattleEquipment[slot];
            ItemObject worn = piece.Item;
            if (!IsTradeable(worn)) return 0;

            // Damage counts: a Worn cuirass is further behind than a sound one of
            // the same make. Quality never lifts a piece. See QualityValue.OrderTier.
            int tier = QualityValue.OrderTier(TierOf(worn), MarketScanner.FineTierOf(piece));
            return GearBalance.Shortfall(tier, ceiling, ItemCatalog.BestBuyableTier(worn.ItemType));
        }

        /// <summary>
        /// Whether the market could ever replace what is worn: something is
        /// there, a merchant could have sold it, and the game ranks it on the
        /// tier scale at all.
        /// </summary>
        internal static bool IsTradeable(ItemObject worn)
        {
            return worn != null && !ItemCatalog.IsIrreplaceable(worn) && TierOf(worn) >= 1;
        }

        private static int CountArmor(List<Candidate> candidates)
        {
            int n = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (SlotMapping.IsArmor(candidates[i].Slot)) n++;
            }
            return n;
        }

        private static int CountArmor(List<EquipmentIndex> slots)
        {
            int n = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (SlotMapping.IsArmor(slots[i])) n++;
            }
            return n;
        }

        /// <summary>
        /// How many slots the town could improve when the last trip began,
        /// before the armour order held any back. LastTripDue is how many of
        /// them the money was first divided between.
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
        /// How many of LastTripGaps were due when the trip began. Fewer means
        /// the armour order held pieces back. A trip may still buy more than
        /// this, since pieces held back reopen once the one holding them back
        /// has been bought.
        /// </summary>
        public static int LastTripDue;

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
                                List<MarketOffer> offers, ItemObject worn, int wornFine,
                                CultureObject culture, int limit)
        {
            Add(found, slot, offers, worn, TierOf(worn), wornFine, culture, limit);
        }

        /// <summary>As above, with the worn piece's tier given rather than read.</summary>
        private static void Add(List<Candidate> found, EquipmentIndex slot,
                                List<MarketOffer> offers, ItemObject worn, int wornTier, int wornFine,
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

            bool wornOwn = ItemCatalog.IsOwnCulture(worn, culture);

            MarketOffer offer = offers[chosen];

            Candidate candidate = new Candidate();
            candidate.Slot = slot;
            candidate.Offer = offer;
            candidate.WornTier = wornTier;
            candidate.WornFine = wornFine;
            candidate.WornOwnCulture = wornOwn;
            candidate.Offers = offers;
            found.Add(candidate);
        }

        /// <summary>
        /// The culture a lord favours at market, as Settings.ShoppingCulture
        /// chooses: his clan's or his own, each falling back to the other where
        /// it is missing, or null for no preference -- which ItemCatalog.IsOwnCulture
        /// reads as "nothing is his own", so culture drops out of every
        /// comparison. Asked in one place so the purchase, the census counter and
        /// the dry runs cannot favour different colours for the same man.
        /// </summary>
        internal static CultureObject PreferredCulture(Hero hero)
        {
            if (hero == null) return null;

            Clan clan = hero.Clan;
            switch (Settings.ShoppingCulture)
            {
                case CultureChoice.None:
                    return null;

                case CultureChoice.Clan:
                    if (clan != null && clan.Culture != null) return clan.Culture;
                    return hero.Culture;

                default:
                    if (hero.Culture != null) return hero.Culture;
                    return clan != null ? clan.Culture : null;
            }
        }

        /// <summary>1-based tier, as the ceiling speaks it.</summary>
        private static int TierOf(ItemObject item)
        {
            return MarketScanner.TierOf(item);
        }

        /// <summary>
        /// The worn piece's tier in hundredths, as the upgrade rule speaks it:
        /// the piece rather than the item, so its quality counts exactly as the
        /// offer's does.
        /// </summary>
        private static int FineOf(EquipmentElement piece)
        {
            return MarketScanner.FineTierOf(piece);
        }
    }
}
