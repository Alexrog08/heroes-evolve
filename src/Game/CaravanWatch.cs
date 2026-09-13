using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// Whether a caravan earns more than its leader spends on himself.
    ///
    /// Written for one question that cannot be answered from constants. Once
    /// companions leading a party were allowed to shop, a caravan master became
    /// a hero who buys gear out of the clan purse -- and for the player's clan
    /// that purse is literally the player's own gold, since Clan.Gold is
    /// Clan.Leader.Gold. If the kit he accumulates is worth more than the
    /// caravan will earn before bandits take him, running caravans becomes a
    /// way to lose money, and the mod would have caused that.
    ///
    /// Two numbers decide it and neither is written down anywhere in the game:
    ///
    ///   what a caravan earns    emergent from the trade simulation
    ///   how often one dies      emergent from bandit spawns and AI targeting
    ///
    /// The income at least has a readable shape. DefaultClanFinanceModel.
    /// AddIncomeFromParty pays the owner (PartyTradeGold - 10000) / 10 every
    /// day and takes it out of the caravan's purse, so at equilibrium the daily
    /// payment IS the caravan's daily trading profit and the trade gold settles
    /// at 10000 + 10x that. Which means one reading of PartyTradeGold gives the
    /// earning rate without waiting for anything.
    ///
    /// Mortality has no such shortcut, so it is counted. Every caravan alive
    /// contributes one caravan-day per day observed, and deaths are divided by
    /// that -- a rate per caravan per year, which is what the break-even needs.
    /// Counting AI caravans as well as the player's is the point rather than
    /// padding: only the player's caravans carry a hero (CaravansCampaignBehavior
    /// .SpawnCaravan passes null for the leader), so he may own two and lose
    /// none all session while two hundred heroless ones give the same road the
    /// same bandits and a number worth trusting.
    ///
    /// Session-local, like every other counter here. This mod writes nothing
    /// into the save by design, so the ledger starts empty on load and the
    /// report says how many days it has been watching. A rate from four days is
    /// not a rate, and the line has to make that visible rather than hide it
    /// behind a confident-looking percentage.
    /// </summary>
    public static class CaravanWatch
    {
        /// <summary>
        /// Days in a campaign year, asked of the game rather than assumed.
        ///
        /// Eighty-four is the stock answer -- four seasons of twenty-one -- and
        /// writing it down as a constant is a mistake this project has already
        /// made once and fixed once, in SkillGrowthService.CyclesPerYear.
        /// CampaignTime derives its calendar from static fields any mod may
        /// change, and FastMode cuts a season to a single week. A death rate
        /// computed per stock year on a FastMode calendar would be wrong by a
        /// factor of three and would look perfectly reasonable while being so.
        /// </summary>
        private const float StockDaysPerYear = 84f;

        private static float DaysPerYear()
        {
            float days = CampaignTime.DaysInYear;
            return days > 0f ? days : StockDaysPerYear;
        }

        /// <summary>The threshold and divisor AddIncomeFromParty applies, read from it.</summary>
        private const int IncomeFloor = 10000;
        private const int IncomeDivisor = 10;

        /// <summary>
        /// What each hero-led caravan was worth last time anyone looked.
        ///
        /// Kept because MobilePartyDestroyed arrives when the party is already
        /// being torn down, and reading a leader and his harness out of it then
        /// is not something to rely on. The snapshot is taken while the caravan
        /// is plainly alive and read back afterwards.
        ///
        /// Small by construction -- only the player's caravans have a hero to
        /// record, so this holds however many caravans he has running, not one
        /// entry per caravan on the map.
        /// </summary>
        private sealed class Seen
        {
            public string Leader;
            public string Owner;
            public int KitWorth;
        }

        private static readonly Dictionary<string, Seen> _withHero = new Dictionary<string, Seen>();

        private static int _lost;
        private static int _lostWithHero;
        private static int _lostToBandits;
        private static long _worthLost;
        private static long _caravanDays;
        private static int _daysObserved;

        /// <summary>Called when a campaign is loaded; statics outlive one campaign.</summary>
        public static void ResetSession()
        {
            _withHero.Clear();
            _lost = 0;
            _lostWithHero = 0;
            _lostToBandits = 0;
            _worthLost = 0;
            _caravanDays = 0;
            _daysObserved = 0;
        }

        /// <summary>
        /// One pass over the caravans, every day: the exposure ledger and the
        /// denominator of the death rate.
        ///
        /// Cheap enough to sit on the daily tick. It is a single walk of
        /// MobileParty.All reading two properties, against a census that costs
        /// 480ms and is deliberately kept off this tick -- and the kit valuation,
        /// which is the only part that touches equipment, runs for the handful
        /// of caravans that have a hero at all.
        /// </summary>
        public static void DailyTick()
        {
            try
            {
                int alive = 0;
                _withHero.Clear();

                foreach (MobileParty party in MobileParty.All)
                {
                    if (party == null || !party.IsCaravan) continue;
                    alive++;

                    Hero leader = party.LeaderHero;
                    if (leader == null) continue;

                    string id = party.StringId;
                    if (id == null) continue;

                    Seen seen = new Seen();
                    seen.Leader = leader.Name != null ? leader.Name.ToString() : "?";
                    seen.Owner = OwnerName(party);
                    seen.KitWorth = Worth(leader);
                    _withHero[id] = seen;
                }

                _caravanDays += alive;
                _daysObserved++;
            }
            catch
            {
                // A survey that throws must not take the campaign's daily tick
                // with it. A missed day costs one sample.
            }
        }

        /// <summary>
        /// A caravan has died. Recorded here rather than inferred from the
        /// population shrinking, because a caravan that reaches its home town
        /// and is disbanded looks identical to one that was ridden down.
        /// </summary>
        public static void OnPartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            try
            {
                if (party == null || !party.IsCaravan) return;

                _lost++;

                bool bandit = destroyer != null
                              && destroyer.MobileParty != null
                              && destroyer.MobileParty.IsBandit;
                if (bandit) _lostToBandits++;

                string id = party.StringId;
                Seen seen;
                if (id == null || !_withHero.TryGetValue(id, out seen)) return;

                _lostWithHero++;
                _worthLost += seen.KitWorth;

                ModLog.Info("CARAVANLOSS owner=" + seen.Owner
                            + " leader=" + seen.Leader
                            + " kitWorth=" + seen.KitWorth
                            + " killedBy=" + (destroyer != null && destroyer.Name != null
                                              ? destroyer.Name.ToString() : "?")
                            + " bandits=" + bandit);
            }
            catch
            {
                // Same reason as above: a lost sample is cheaper than a lost
                // campaign.
            }
        }

        /// <summary>
        /// The census section: what caravans are earning, what their leaders
        /// are wearing, and how fast they die.
        /// </summary>
        public static void Report()
        {
            ModLog.Info("--- CARAVANS ---");

            int alive = 0;
            int withHero = 0;
            long tradeGold = 0;
            long income = 0;

            try
            {
                foreach (MobileParty party in MobileParty.All)
                {
                    if (party == null || !party.IsCaravan) continue;

                    alive++;
                    int gold = party.PartyTradeGold;
                    tradeGold += gold;
                    income += DailyIncome(gold);

                    Hero leader = party.LeaderHero;
                    if (leader == null) continue;
                    withHero++;

                    // Named in full, because there are only ever a few and they
                    // are the ones the decision is about.
                    int combat = BestCombatSkill(leader);
                    ModLog.Info("CARAVAN owner=" + OwnerName(party)
                                + " leader=" + leader.Name
                                + " clan=" + (leader.Clan != null ? leader.Clan.Name.ToString() : "none")
                                + " naval=" + party.IsCurrentlyAtSea
                                + " men=" + (party.MemberRoster != null
                                             ? party.MemberRoster.TotalManCount : 0)
                                + " strength=" + (int)(party.Party != null
                                                       ? party.Party.EstimatedStrength : 0f)
                                + " tradeGold=" + gold
                                + " dailyIncome=" + DailyIncome(gold)
                                + " kitWorth=" + Worth(leader)
                                + " bestCombat=" + combat
                                + " ceiling=" + TierCeiling.Compute(
                                      leader.Clan != null ? leader.Clan.Tier : 0, combat,
                                      Settings.ClanWeight, Settings.SkillWeight, Settings.MinimumTier)
                                + " shops=" + HeroFilter.IsEligibleToShop(leader));
                }
            }
            catch
            {
                // Partial is better than none; the totals below say how many
                // were actually read.
            }

            ModLog.Info("CARAVANS alive=" + alive
                        + " withHero=" + withHero
                        + " tradeGold=" + tradeGold
                        + " dailyIncome=" + income
                        + " meanIncomePerCaravan=" + (alive > 0 ? income / alive : 0));

            ReportMortality(alive, alive > 0 ? income / alive : 0);
        }

        /// <summary>
        /// The death rate and what it means for the gear budget.
        ///
        /// The arithmetic the whole class exists for. A caravan earns its owner
        /// some amount a year; its leader's kit is exposed to being taken when
        /// the caravan is ridden down and the captor rolls for it. Set those
        /// against each other and the answer is a number rather than an
        /// intuition: the kit that a caravan's own earnings will cover.
        ///
        /// Losing the gear is not the same as burning it -- PlunderService moves
        /// it into the captor's baggage and killing him gets it back -- so the
        /// figure below is the pessimistic reading, the one where none of it is
        /// ever recovered. The generous reading is the same number divided by
        /// however often the player hunts down the band that took it, which is
        /// not something this can know.
        /// </summary>
        private static void ReportMortality(int alive, long meanIncome)
        {
            float year = DaysPerYear();
            float perCaravanYear = _caravanDays > 0
                ? _lost * year / _caravanDays
                : 0f;

            ModLog.Info("CARAVANDEATHS observedDays=" + _daysObserved
                        + " daysPerYear=" + (int)year
                        + " caravanDays=" + _caravanDays
                        + " lost=" + _lost
                        + " toBandits=" + _lostToBandits
                        + " withHero=" + _lostWithHero
                        + " worthLost=" + _worthLost
                        + " lossRatePerCaravanPerYear=" + (int)(perCaravanYear * 100f) + "%");

            // One season of whatever calendar this campaign is running.
            if (_daysObserved < (int)(year / 4f))
            {
                ModLog.Info("CARAVANDEATHS (watched " + _daysObserved
                            + " days of a " + (int)(year / 4f)
                            + "-day season; too few to trust a rate -- keep playing)");
            }

            if (perCaravanYear <= 0f || meanIncome <= 0) return;

            // What a caravan brings in against what its leader stands to lose.
            long yearlyIncome = (long)(meanIncome * year);
            float robbed = perCaravanYear * Settings.PlunderChance;
            if (robbed <= 0f) return;

            ModLog.Info("CARAVANBREAKEVEN yearlyIncomePerCaravan=" + yearlyIncome
                        + " chanceRobbedPerYear=" + (int)(robbed * 100f) + "%"
                        + " kitPaidForByOneYear=" + (int)(yearlyIncome / robbed)
                        + " (a kit worth more than that loses money at this death rate,"
                        + " counting nothing recovered by beating the robbers)");
        }

        /// <summary>Denars a day this caravan pays its owner, by the engine's own rule.</summary>
        private static int DailyIncome(int partyTradeGold)
        {
            if (partyTradeGold <= IncomeFloor) return 0;
            return (partyTradeGold - IncomeFloor) / IncomeDivisor;
        }

        /// <summary>Everything he is wearing, priced as PlunderService would take it.</summary>
        private static int Worth(Hero hero)
        {
            if (hero == null || hero.BattleEquipment == null) return 0;

            int worth = 0;
            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                worth += SlotWorth(hero, SlotMapping.WeaponSlot(i));
            }
            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                worth += SlotWorth(hero, slot);
            }
            worth += SlotWorth(hero, EquipmentIndex.Horse);
            worth += SlotWorth(hero, EquipmentIndex.HorseHarness);
            return worth;
        }

        private static int SlotWorth(Hero hero, EquipmentIndex slot)
        {
            EquipmentElement worn = hero.BattleEquipment[slot];
            // ItemValue and not Item.Value, so a fine sword prices as the fine
            // sword it is -- the same figure PlunderService books when it takes
            // one, which is what makes the two comparable.
            return worn.Item != null ? worn.ItemValue : 0;
        }

        /// <summary>The best of the six weapon skills, which is what feeds the ceiling.</summary>
        private static int BestCombatSkill(Hero hero)
        {
            if (hero == null) return 0;

            SkillProfile skills = HeroAdapter.ReadSkills(hero);
            int best = 0;
            for (int i = 0; i < 6; i++)
            {
                int v = skills.Get((SkillKind)i);
                if (v > best) best = v;
            }
            return best;
        }

        private static string OwnerName(MobileParty party)
        {
            if (party == null || party.Party == null) return "?";
            Hero owner = party.Party.Owner;
            return owner != null && owner.Name != null ? owner.Name.ToString() : "none";
        }
    }
}
