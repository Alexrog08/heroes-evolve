using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// What the market has been changing, not only how much it has bought.
    ///
    /// Written because the census had no way to see the worst bug the market
    /// ever had. It counted purchases, prices and tiers behind, and meanwhile 119
    /// lords were turned from spearmen into javelin throwers, because nothing
    /// asked what a purchase had done to the man who made it. It was found by a
    /// script reading the log against every item file on the disk, which is not
    /// a check anybody should have to repeat by hand.
    ///
    /// Three questions, each with an answer the design commits to in advance:
    ///
    ///   weapons   did a purchase change what kind of fighter he is?
    ///             otherFamily and thrownLineCrossed must read zero.
    ///   repeats   did a trip pay twice for one slot?
    ///             Must read zero.
    ///   armour    is the order in GearBalance dressing lords evenly, and is it
    ///             holding anyone back for long?
    ///
    /// Session-local, like CaravanWatch: nothing goes into the save, the tallies
    /// start empty on load, and the report says which day they started. The
    /// armour snapshot is read fresh at census time and needs no history.
    /// </summary>
    public static class PurchaseWatch
    {
        /// <summary>
        /// Trips in a row held back with no armour bought before a lord is
        /// worth naming. Not a rule, only where the report starts pointing: a
        /// lord shops at most once a day, so five such trips is most of a week
        /// spent waiting on one piece.
        /// </summary>
        private const int StallTrips = 5;

        private sealed class Streak
        {
            public string Name;
            public int Current;
            public int Longest;
        }

        private static int _firstDay = -1;
        private static int _trips;
        private static int _tripsThatBought;
        private static int _pieces;
        private static readonly Dictionary<string, int> _bySlot = new Dictionary<string, int>();

        private static readonly int[] _weaponKinds = new int[SwapRules.KindCount];
        private static readonly int[] _lootKinds = new int[SwapRules.KindCount];
        private static readonly Dictionary<string, int> _classChanges = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> _typeChanges = new Dictionary<string, int>();

        private static readonly Dictionary<string, int> _lastBoughtDay = new Dictionary<string, int>();
        private static int _slotTwiceSameDay;

        private static int _tripsArmorOnOffer;
        private static int _tripsBoughtArmor;
        private static int _tripsHeldBack;
        private static int _tripsHeldBackNoArmor;
        private static int _piecesHeldBack;
        private static readonly Dictionary<string, Streak> _held = new Dictionary<string, Streak>();

        /// <summary>Called when a campaign is loaded; statics outlive one campaign.</summary>
        public static void ResetSession()
        {
            _firstDay = -1;
            _trips = 0;
            _tripsThatBought = 0;
            _pieces = 0;
            _bySlot.Clear();
            System.Array.Clear(_weaponKinds, 0, _weaponKinds.Length);
            System.Array.Clear(_lootKinds, 0, _lootKinds.Length);
            _classChanges.Clear();
            _typeChanges.Clear();
            _lastBoughtDay.Clear();
            _slotTwiceSameDay = 0;
            _tripsArmorOnOffer = 0;
            _tripsBoughtArmor = 0;
            _tripsHeldBack = 0;
            _tripsHeldBackNoArmor = 0;
            _piecesHeldBack = 0;
            _held.Clear();
        }

        /// <summary>
        /// One trip through a market, bought or not.
        ///
        /// armorHeldBack is what the armour order kept him from as he walked in:
        /// pieces on the shelf that would have improved a slot, refused because
        /// a poorer piece comes first. A trip that held some back and then sold
        /// him no armour at all is the order waiting on a piece this town does
        /// not have -- the behaviour it was built to have, and the one that must
        /// not go on for long.
        /// </summary>
        public static void RecordTrip(Hero hero, int bought, int armorOnOffer, int armorHeldBack,
                                      bool boughtArmor)
        {
            try
            {
                Stamp();
                _trips++;
                if (bought > 0) _tripsThatBought++;
                if (armorOnOffer > 0) _tripsArmorOnOffer++;
                if (boughtArmor) _tripsBoughtArmor++;

                bool waited = armorHeldBack > 0 && !boughtArmor;
                if (armorHeldBack > 0)
                {
                    _tripsHeldBack++;
                    _piecesHeldBack += armorHeldBack;
                    if (waited) _tripsHeldBackNoArmor++;
                }

                if (hero == null || hero.StringId == null) return;

                Streak streak;
                _held.TryGetValue(hero.StringId, out streak);

                if (waited)
                {
                    if (streak == null)
                    {
                        streak = new Streak();
                        streak.Name = hero.Name != null ? hero.Name.ToString() : "?";
                        _held[hero.StringId] = streak;
                    }
                    streak.Current++;
                    if (streak.Current > streak.Longest) streak.Longest = streak.Current;
                }
                else if (boughtArmor && streak != null)
                {
                    // A trip with nothing held back and nothing bought says
                    // nothing about the order either way, so only armour
                    // actually bought ends a wait.
                    streak.Current = 0;
                }
            }
            catch
            {
                // A tally must never cost a purchase.
            }
        }

        /// <summary>One piece bought, and what it replaced.</summary>
        public static void RecordPurchase(Hero hero, EquipmentIndex slot, ItemObject sold, ItemObject bought)
        {
            try
            {
                Stamp();
                _pieces++;
                Bump(_bySlot, SlotMapping.NameOf(slot));

                // Same hero, same slot, same day. A lord gets one trip a day, so
                // this is a trip that paid twice for one slot -- which the trip
                // itself now forbids, and this is what notices if it ever stops.
                if (hero != null && hero.StringId != null)
                {
                    int today = Today();
                    string key = hero.StringId + "|" + SlotMapping.NameOf(slot);
                    int last;
                    if (_lastBoughtDay.TryGetValue(key, out last) && last == today) _slotTwiceSameDay++;
                    _lastBoughtDay[key] = today;
                }

                Classify(_weaponKinds, true, hero, slot, sold, bought);
            }
            catch
            {
                // Same reason as above.
            }
        }

        /// <summary>
        /// One piece a captor put on out of a prisoner's kit. The same rules
        /// choose it as choose a purchase, so the same line must never be
        /// crossed here either -- and a regression in the rules would show up
        /// here with nobody looking at the market.
        /// </summary>
        public static void RecordLootSwap(Hero hero, EquipmentIndex slot, ItemObject gaveUp, ItemObject took)
        {
            try
            {
                Stamp();
                Classify(_lootKinds, false, hero, slot, gaveUp, took);
            }
            catch
            {
                // Same reason as above.
            }
        }

        private static void Classify(int[] kinds, bool purchase, Hero hero, EquipmentIndex slot,
                                     ItemObject sold, ItemObject bought)
        {
            if (!SlotMapping.IsWeapon(slot) || sold == null || bought == null) return;

            WeaponCategory was = ItemClassifier.Classify(sold);
            WeaponCategory now = ItemClassifier.Classify(bought);
            SwapKind kind = SwapRules.Classify(was, now, ItemClassifier.Supports(bought, was));
            kinds[(int)kind]++;

            if (purchase)
            {
                if (was != now) Bump(_classChanges, was + ">" + now);

                // Read off the item types, which nothing in the market's rules
                // decides. If the rules themselves go wrong again, this is the
                // tally that does not go wrong with them.
                if (sold.ItemType != bought.ItemType) Bump(_typeChanges, sold.ItemType + ">" + bought.ItemType);
            }

            // Named the moment it happens. The count says a bug exists; the
            // items say where to look.
            if (SwapRules.IsNeverByDesign(kind))
            {
                ModLog.Info("SWAPWARN source=" + (purchase ? "buy" : "loot")
                            + " kind=" + kind
                            + " hero=" + (hero != null ? hero.Name : null)
                            + " slot=" + SlotMapping.NameOf(slot)
                            + " gaveUp=" + sold.StringId + " (" + was + ")"
                            + " took=" + bought.StringId + " (" + now + ")");
            }
        }

        /// <summary>The census section.</summary>
        public static void Report(float clanWeight, float skillWeight, int minimumTier)
        {
            ModLog.Info("--- PURCHASES ---");

            try
            {
                ModLog.Info("PURCHASE watchedFromDay=" + (_firstDay < 0 ? "none" : _firstDay.ToString())
                            + " today=" + Today()
                            + " trips=" + _trips
                            + " tripsThatBought=" + _tripsThatBought
                            + " pieces=" + _pieces);
                ModLog.Info("PURCHASE bySlot " + BySlot());
                ModLog.Info("PURCHASE weapons " + Kinds(_weaponKinds));
                ModLog.Info("PURCHASE weaponClassChanges " + Top(_classChanges, 12));
                ModLog.Info("PURCHASE weaponTypeChanges " + Top(_typeChanges, 12));
                ModLog.Info("PURCHASE slotBoughtTwiceSameDay=" + _slotTwiceSameDay + " (expect 0)");
                ModLog.Info("LOOTFIT weapons " + Kinds(_lootKinds));

                ModLog.Info("ARMOURORDER tripsWithArmourOnOffer=" + _tripsArmorOnOffer
                            + " boughtArmour=" + _tripsBoughtArmor
                            + " heldBack=" + _tripsHeldBack
                            + " heldBackBoughtNoArmour=" + _tripsHeldBackNoArmor
                            + " piecesHeldBack=" + _piecesHeldBack);
                ModLog.Info("ARMOURORDER " + Streaks());
            }
            catch
            {
                // Partial is better than none.
            }

            ReportArmourSpread(clanWeight, skillWeight, minimumTier);
        }

        /// <summary>
        /// How evenly every shopper is dressed right now, by the measure the
        /// armour order itself uses.
        ///
        /// Spread is how far apart his most and least neglected pieces are, in
        /// tiers behind what each could be. Within a tier is what the order
        /// aims for. The last column is the complaint that started it, asked
        /// exactly: gloves or boots more than a tier nearer their best than his
        /// body armour is to its own.
        ///
        /// Only pieces the market could replace are counted, and only lords
        /// wearing two or more of them: an empty slot or a noble's helm is not
        /// behind or ahead of anything.
        /// </summary>
        private static void ReportArmourSpread(float clanWeight, float skillWeight, int minimumTier)
        {
            EquipmentIndex[] slots = SlotMapping.ArmorSlots;
            int body = IndexOf(slots, EquipmentIndex.Body);
            int gloves = IndexOf(slots, EquipmentIndex.Gloves);
            int legs = IndexOf(slots, EquipmentIndex.Leg);

            int shoppers = 0, measured = 0, even = 0, apart = 0, extremitiesAhead = 0;
            List<int> spreads = new List<int>();
            int[] total = new int[slots.Length];
            int[] count = new int[slots.Length];

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligibleToShop(hero) || hero.BattleEquipment == null) continue;
                    shoppers++;

                    int ceiling = HeroAdapter.ReadCeiling(hero, HeroAdapter.ReadSkills(hero),
                                                          clanWeight, skillWeight, minimumTier);

                    int[] shortfall = new int[slots.Length];
                    bool[] has = new bool[slots.Length];
                    int pieces = 0, most = 0, least = int.MaxValue;

                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (!ShoppingTrip.IsTradeable(hero.BattleEquipment[slots[i]].Item)) continue;

                        has[i] = true;
                        shortfall[i] = ShoppingTrip.ArmorShortfall(hero, slots[i], ceiling);
                        pieces++;
                        if (shortfall[i] > most) most = shortfall[i];
                        if (shortfall[i] < least) least = shortfall[i];
                    }

                    if (pieces < 2) continue;

                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (!has[i]) continue;
                        total[i] += shortfall[i];
                        count[i]++;
                    }

                    measured++;
                    int spread = most - least;
                    spreads.Add(spread);
                    if (spread <= GearBalance.Tolerance) even++; else apart++;

                    if (has[body] && (has[gloves] || has[legs]))
                    {
                        int extremity = int.MaxValue;
                        if (has[gloves]) extremity = shortfall[gloves];
                        if (has[legs] && shortfall[legs] < extremity) extremity = shortfall[legs];
                        if (shortfall[body] - extremity > GearBalance.Tolerance) extremitiesAhead++;
                    }
                }
                catch
                {
                    // One unreadable hero must not cost the survey.
                }
            }

            ModLog.Info("ARMOUR shoppers=" + shoppers
                        + " measured=" + measured
                        + " withinATier=" + even + " (" + Percent(even, measured) + ")"
                        + " twoOrMoreApart=" + apart + " (" + Percent(apart, measured) + ")"
                        + " glovesOrBootsAheadOfBody=" + extremitiesAhead
                        + " (" + Percent(extremitiesAhead, measured) + ")");
            ModLog.Info("ARMOUR spread " + Diagnostics.Percentiles(spreads));

            StringBuilder mean = new StringBuilder("ARMOUR meanTiersBehind");
            for (int i = 0; i < slots.Length; i++)
            {
                mean.Append(' ').Append(SlotMapping.NameOf(slots[i])).Append('=').Append(Hundredths(total[i], count[i]));
            }
            ModLog.Info(mean.ToString());
        }

        private static string Kinds(int[] kinds)
        {
            int total = 0;
            for (int i = 0; i < kinds.Length; i++) total += kinds[i];

            return "n=" + total
                   + " sameClass=" + kinds[(int)SwapKind.SameClass]
                   + " otherClassSameFamily=" + kinds[(int)SwapKind.OtherClassSameFamily]
                   + " throughSecondaryUsage=" + kinds[(int)SwapKind.ThroughSecondaryUsage]
                   + " otherFamily=" + kinds[(int)SwapKind.OtherFamily] + " (expect 0)"
                   + " thrownLineCrossed=" + kinds[(int)SwapKind.ThrownLineCrossed] + " (expect 0)";
        }

        private static string Streaks()
        {
            int longest = 0, stalled = 0;
            List<Streak> all = new List<Streak>(_held.Values);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Longest > longest) longest = all[i].Longest;
                if (all[i].Current >= StallTrips) stalled++;
            }

            all.Sort(ByCurrentDescending);
            StringBuilder worst = new StringBuilder();
            for (int i = 0; i < all.Count && i < 3; i++)
            {
                if (all[i].Current <= 0) break;
                if (worst.Length > 0) worst.Append(", ");
                worst.Append(all[i].Name).Append(" (").Append(all[i].Current).Append(')');
            }

            return "longestHeldStreak=" + longest
                   + " heldAtLeast" + StallTrips + "TripsRunning=" + stalled
                   + " waitingLongestNow=" + (worst.Length > 0 ? worst.ToString() : "<none>");
        }

        /// <summary>Every slot in a fixed order, so two censuses line up column for column.</summary>
        private static string BySlot()
        {
            List<EquipmentIndex> order = new List<EquipmentIndex>(SlotMapping.ArmorSlots);
            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++) order.Add(SlotMapping.WeaponSlot(i));
            order.Add(EquipmentIndex.Horse);
            order.Add(EquipmentIndex.HorseHarness);

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < order.Count; i++)
            {
                string name = SlotMapping.NameOf(order[i]);
                int n;
                _bySlot.TryGetValue(name, out n);
                if (text.Length > 0) text.Append(' ');
                text.Append(name).Append('=').Append(n);
            }
            return text.ToString();
        }

        private static string Top(Dictionary<string, int> counts, int max)
        {
            if (counts.Count == 0) return "<none>";

            List<KeyValuePair<string, int>> pairs = new List<KeyValuePair<string, int>>(counts);
            pairs.Sort(ByCountDescending);

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < pairs.Count && i < max; i++)
            {
                if (text.Length > 0) text.Append(' ');
                text.Append(pairs[i].Key).Append('=').Append(pairs[i].Value);
            }
            if (pairs.Count > max) text.Append(" (+").Append(pairs.Count - max).Append(" more)");
            return text.ToString();
        }

        private static int ByCountDescending(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
        {
            return b.Value.CompareTo(a.Value);
        }

        private static int ByCurrentDescending(Streak a, Streak b)
        {
            return b.Current.CompareTo(a.Current);
        }

        private static int IndexOf(EquipmentIndex[] slots, EquipmentIndex slot)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == slot) return i;
            }
            return -1;
        }

        private static void Bump(Dictionary<string, int> counts, string key)
        {
            int n;
            counts.TryGetValue(key, out n);
            counts[key] = n + 1;
        }

        private static string Percent(int part, int whole)
        {
            return whole > 0 ? (part * 100 / whole) + "%" : "-";
        }

        /// <summary>A mean in hundredths, printed without floats.</summary>
        private static string Hundredths(int total, int count)
        {
            if (count == 0) return "-";
            int scaled = total * 100 / count;
            return (scaled / 100) + "." + (scaled % 100 < 10 ? "0" : "") + (scaled % 100);
        }

        private static int Today()
        {
            return (int)CampaignTime.Now.ToDays;
        }

        private static void Stamp()
        {
            if (_firstDay < 0) _firstDay = Today();
        }
    }
}
