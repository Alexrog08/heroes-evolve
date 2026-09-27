using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// Subscribes to the event that matters for the grant path and does the
    /// per-hero work. Every hero is wrapped in try/catch: one bad hero must
    /// never take down the tick.
    /// </summary>
    public class HeroLoadoutBehavior : CampaignBehaviorBase
    {
        // Every tunable lives in Settings, which reads settings.xml at load and
        // falls back to the shipped defaults. These forward rather than hold, so
        // the diagnostics and the console commands cannot drift from what the
        // live tick is actually using.
        internal static float ClanWeight { get { return Settings.ClanWeight; } }
        internal static float SkillWeight { get { return Settings.SkillWeight; } }
        internal static int MinimumTier { get { return Settings.MinimumTier; } }
        internal static int DominanceMargin { get { return Settings.DominanceMargin; } }

        /// <summary>
        /// The day's gear spending, per clan. Owned here because a behaviour
        /// instance is built fresh per campaign load, which is exactly the
        /// lifetime this ledger should have -- it holds no save data.
        /// </summary>
        private readonly BudgetService _budget =
            new BudgetService();

        /// <summary>
        /// Who has already been shopping today.
        ///
        /// A lord crossing three towns in a day would otherwise shop three
        /// times. Lords Gear caps the same way -- it polls hourly and clears a
        /// set of hero ids each morning.
        ///
        /// There used to be a roll as well, a 25% chance per visit, sized for
        /// the days when a trip bought one piece and the roll therefore paced
        /// the engine. A trip now buys every gap the town can fill, so the roll
        /// paced nothing: it only decided which visit counted, and it cost the
        /// man who needed the market most -- a lord just robbed, walking past
        /// three towns in his rags. What paces spending is this cap, the clan's
        /// daily share and reserve (BudgetService), the skill ceiling, the
        /// minimum gain worth a swap, and what that one town has on its
        /// shelves. A lord now stops at the first town he enters each day.
        ///
        /// Cleared with the budget ledger and, like it, never saved.
        /// </summary>
        private readonly HashSet<string> _shoppedToday = new HashSet<string>();

        public override void RegisterEvents()
        {
            // A fresh behaviour instance is built per campaign load, but the
            // diagnostics counter is static and outlives one campaign.
            // Re-read on every campaign load, not only once at startup, so a
            // player who edits settings.xml and loads a save sees the change
            // without restarting the game.
            Settings.Load();

            // And then MCM over the top of it, when the player has MCM. The
            // screen keeps writing into Settings as he moves the switches, so
            // turning capture loss off mid-campaign takes effect at the next
            // capture with no reload. See McmBridge.
            McmBridge.Attach();

            Diagnostics.ResetSession();
            CaravanWatch.ResetSession();
            CaravanPurse.ResetSession();
            PurchaseWatch.ResetSession();
            Rags.ResetSession();
            CaptivityRobberies.ResetSession();
            PendingNotices.ResetSession();
            ItemCatalog.ResetSession();
            HeroTalent.ResetSession();
            WrittenSkills.ResetSession();
            CultureProfile.Reset();
            CultureArchetypes.Reset();
            WeaponPerks.Reset();

            // Coming of age, noted and nothing more. See _freshlyMade.
            //
            // The repair cannot happen here. AgingCampaignBehavior is another
            // listener on this event, and its handler calls
            // EquipmentHelper.AssignHeroEquipmentFromEquipment twice, copying
            // every slot of the dummy set over whatever it finds. It runs AFTER
            // this mod: MbEvent.AddNonSerializedListener prepends and
            // InvokeList walks from the head, so the last listener registered
            // runs first, and a module that declares its dependencies honestly
            // always registers last. (An earlier version of this note said the
            // opposite, and its own symptom -- the grant being overwritten --
            // disproved it.) So the event only records who turned 18, and the
            // daily tick judges him once the game has finished dressing him.
            CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, OnHeroComesOfAge);

            ModLog.Info("SETTINGS in force: " + Settings.Describe());
            ItemCatalog.ReportUnknownExclusions();

            // One branch a tick, and nothing else unless a line is waiting.
            // The robbery notice has to print after the game's own capture
            // line, and no ordering of listeners can achieve that -- see
            // PendingNotices. It waits a frame instead.
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);

            CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, OnDailyTickHero);

            // Weekly, not daily. The peak a lord grows toward takes forty years
            // to arrive, so running seven times as often multiplies the work
            // without changing anything anyone could notice.
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);

            // The purchase engine. Buying happens where the goods are, so the
            // trigger is entering a town rather than any tick: what a lord can
            // buy is whatever that particular market has on its shelves.
            CampaignEvents.AfterSettlementEntered.AddNonSerializedListener(this, OnAfterSettlementEntered);

            // Only to clear the day's spending ledger. Cheap on purpose -- see
            // the note below about what a real daily sweep costs.
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

            // Losing gear. Off unless the player asks for it, so the listener
            // costs one branch per capture when it is not wanted.
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);

            // Measurement only, and it changes nothing in the campaign. A
            // caravan that dies is the numerator of the rate CaravanWatch
            // exists to produce, and it cannot be inferred from the population
            // shrinking: a caravan disbanded at its home town leaves the same
            // gap as one ridden down by bandits.
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);

            // Winning a battle should win back what was stolen from you. The
            // game's own loot pass refuses to move that gear, so this adds the
            // part it leaves behind to the same pile -- while that pile is being
            // filled, so it shows up in the loot window rather than appearing in
            // the baggage afterwards. See StolenGoods.
            CampaignEvents.OnCollectLootsItemsEvent.AddNonSerializedListener(this, OnCollectLootItems);

            // A new campaign's lords, put on the curve once before the first day.
            // Two events because a loaded save must never be touched: only a new
            // campaign raises OnNewGameCreated, and by the time the session has
            // launched the game has finished building every lord it will start
            // with. See StartingCurvePass.
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);

            // Latecomers. The starting curve runs once at world creation, so
            // anybody made afterwards kept whatever he was made with -- and
            // growth only ever raises, so nothing was going to correct it. See
            // StartingCurvePass.ApplyToLatecomer for who that actually reaches
            // and why it has to happen exactly once.
            CampaignEvents.HeroCreated.AddNonSerializedListener(this, OnHeroCreated);
            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, OnAfterSessionLaunched);

            // The daily tick above carries the ledger reset and nothing else.
            // The census is deliberately NOT run from it.
            // Measured at 480ms on a 600-lord campaign -- two thousand seven
            // hundred full sweeps of a 3500-item catalogue -- and it changes
            // nothing in the game. Half a second of freeze on the first day of
            // every load, to write a log file nobody is reading at the time, is
            // not a trade worth making. "hev.census" runs it on demand.
        }

        /// <summary>
        /// Set when this session began as a new campaign rather than a load, and
        /// cleared once the starting curve has had its one chance to run.
        /// </summary>
        private bool _newCampaign;

        /// <summary>
        /// A lord turning 18, noted for one look tomorrow. See _freshlyMade.
        /// </summary>
        private void OnHeroComesOfAge(Hero hero)
        {
            NoteFreshlyMade(hero, "cameOfAge");
        }

        /// <summary>
        /// Puts a hero made after the campaign began on the same curve the rest
        /// of the world was put on at its start.
        ///
        /// Children are skipped and do not need this: the game normalises what
        /// they inherit, and their skills are not written until they come of
        /// age anyway. It is the parentless heroes -- tavern wanderers, lords
        /// generated to fill a clan -- who arrive carrying a template's grown
        /// figures.
        ///
        /// Silent during world creation. HeroCreated fires for every lord in
        /// Calradia while the map is being built, and Apply is about to walk
        /// the whole roster a moment later; curving each of them twice would
        /// log four hundred lines to reach the same answer.
        /// </summary>
        private void OnHeroCreated(Hero hero, bool isBornNaturally)
        {
            try
            {
                // Before the curve's own guards: the kit looks at every hero the
                // game makes, and HeroCreated fires at the very end of
                // InitializeHeroFromSettings, after his gear is assigned.
                // Children are skipped in NoteFreshlyMade and seen at 18.
                NoteFreshlyMade(hero, _newCampaign ? "newCampaign" : "generated");

                if (_newCampaign) return;
                if (!Settings.EnableSkillGrowth || !Settings.StartLordsOnCurve) return;
                if (hero == null || hero.IsChild) return;

                StartingCurvePass.ApplyToLatecomer(hero);
            }
            catch (System.Exception error)
            {
                ModLog.Error("STARTCURVE latecomer failed: "
                             + error.GetType().Name + " " + error.Message);
            }
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            _newCampaign = true;
        }

        private void OnAfterSessionLaunched(CampaignGameStarter starter)
        {
            // Where the saddles stand as the session opens, loaded or new: the
            // baseline the weekly line is read against.
            try
            {
                ModLog.Info("MOUNTFIT " + Diagnostics.MountFit());
            }
            catch (System.Exception ex)
            {
                ModLog.Error("MOUNTFIT failed: " + ex.GetType().Name + " " + ex.Message);
            }
            LogKingdoms();

            if (!_newCampaign) return;
            _newCampaign = false;

            // Every lord the world was built with, for one look on his first
            // day -- whatever the curve is set to. See _freshlyMade.
            int lords = 0;
            foreach (Hero lord in Hero.AllAliveHeroes)
            {
                if (lord == null || !lord.IsLord || lord.IsChild) continue;
                NoteFreshlyMade(lord, "newCampaign");
                lords++;
            }

            // Lords apart from everyone noted. HeroCreated notes every adult the
            // world is built with, notables and wanderers too, and that total
            // printed alone read as 1704 lords waiting on the kit when there
            // were 497. Only a lord can be given one.
            ModLog.Info("KIT newCampaign lords=" + lords + " noted=" + _freshlyMade.Count);

            if (!Settings.StartLordsOnCurve) return;

            // The curve sets lords where growth will carry them from. With growth
            // off nothing would ever carry them anywhere, and every lord on the map
            // would stay where the curve left him for the rest of the campaign.
            if (!Settings.EnableSkillGrowth)
            {
                ModLog.Info("STARTCURVE skipped: skill growth is off, so lords set on the curve would never grow");
                return;
            }

            try
            {
                StartingCurvePass.Apply();
            }
            catch (System.Exception error)
            {
                // The campaign starts either way, on the sheets the generator wrote.
                ModLog.Error("STARTCURVE failed: " + error.GetType().Name + " " + error.Message);
            }
        }

        /// <summary>Nothing is stored in the save. Deliberately empty.</summary>
        public override void SyncData(IDataStore dataStore) { }

        /// <summary>
        /// Lords whose gear TaleWorlds has just made, waiting to be looked at
        /// once -- keyed by id, with the moment that put them here.
        ///
        /// The starting kit repairs one thing only: a lord whose equipment the
        /// game generated broken. So it looks at the moments the game generates
        /// it, and at nothing else.
        ///   newCampaign -- every lord, as the world is built. TaleWorlds ships
        ///     some of its own hand-written lords broken, and they will never
        ///     turn 18 inside it. Which ones is a draw: the Aserai ladies all
        ///     wear ase_bat_template_lady, whose three variants are sword and
        ///     shield, sword alone, and sword alone -- two in three come up a
        ///     weapon short, and a different two in three each campaign. A
        ///     first test repaired 27 lords on day one, Anidha and Sira among
        ///     them; Maraa drew the good variant and was left alone.
        ///   cameOfAge -- a lord turning 18, when the game hands him the gear of
        ///     an adult. This is where the bug usually shows.
        ///   generated -- an adult lord the game makes mid-campaign: a rebel
        ///     leader (RebellionsCampaignBehavior), a minor-faction lord
        ///     (HeroSpawnCampaignBehavior), a new clan's family when a companion
        ///     is raised to lead one (CompanionRolesCampaignBehavior). All come
        ///     out of HeroCreator.CreateSpecialHero, from a template.
        /// Every time, what is judged is gear TaleWorlds has just produced, and
        /// never anything the player chose.
        ///
        /// Judged on the hero's next daily tick, not inside the event.
        /// AgingCampaignBehavior also listens to HeroComesOfAgeEvent, runs after
        /// this mod, and copies the dummy set over anything granted in it. A day
        /// later the game is finished with him.
        ///
        /// Once. The entry goes the moment it is read, so no hero is looked at
        /// twice -- which is also what ends the double repair of a young lord
        /// whose first kit came up a weapon short. And never later than his
        /// next daily tick: a note kept longer is one a reload can lose.
        ///
        /// It used to be the opposite: every hero, every day, repaired whenever
        /// he looked broken. That reached what it never should. A companion
        /// sent on an errand came home re-dressed, because the game lifts him
        /// out of the party for it and the rule read that as a man out on his
        /// own.
        ///
        /// Not serialised. A save made between the moment and the next day loses
        /// the entry and that hero is not looked at; this mod keeps nothing in
        /// the save.
        /// </summary>
        private readonly Dictionary<string, string> _freshlyMade = new Dictionary<string, string>();

        /// <summary>
        /// Queues a hero for his one look. Noting is deliberately dumb -- anyone
        /// the game has just made is noted -- and the judging is done once, by
        /// HeroFilter.IsEligibleForRepair. The first moment wins, so a lord
        /// noted twice while the world is being built is still one look.
        /// </summary>
        private void NoteFreshlyMade(Hero hero, string moment)
        {
            if (hero == null || hero.IsChild) return;
            string id = IdOf(hero);
            if (id == null || _freshlyMade.ContainsKey(id)) return;
            _freshlyMade.Add(id, moment);

            // Logged for lords outside world creation, where there are a few a
            // year and each is worth seeing. The world's own five hundred are
            // counted in one KIT line instead.
            if (moment != "newCampaign" && hero.IsLord)
            {
                ModLog.Info("KIT noted hero=" + hero.Name + " moment=" + moment
                            + " age=" + (int)hero.Age);
            }
        }

        /// <summary>
        /// The hero's own object id. Not the CharacterObject's: that is unique
        /// per hero in practice, but nothing guarantees it, and a shared
        /// character would make two heroes share one look at the kit.
        /// </summary>
        private static string IdOf(Hero hero)
        {
            return hero.StringId;
        }

        private void OnTick(float dt)
        {
            // Guarded without logging, unlike the other handlers here. A throw
            // on the tick would otherwise write a line every frame, and the
            // queue clears itself before printing, so a failure costs one
            // sentence rather than repeating for ever.
            try
            {
                PendingNotices.Flush();
            }
            catch
            {
            }
        }

        private void OnDailyTickHero(Hero hero)
        {
            TryRepair(hero);
        }

        /// <summary>
        /// A man walking out of the cells, told what he walked out in. Guarded
        /// like every other per-hero path.
        /// </summary>
        private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party,
                                            IFaction capturerFaction, EndCaptivityDetail detail,
                                            bool showNotification)
        {
            try
            {
                // The game's own flag for whether this release is worth
                // announcing. A release it chose to keep quiet keeps ours
                // quiet too, rather than leaving a line with nothing above it.
                if (!showNotification) return;

                PlunderService.AnnounceRelease(prisoner, detail);
            }
            catch (System.Exception ex)
            {
                ModLog.Error("release notice failed for "
                             + (prisoner != null ? prisoner.Name : null)
                             + ": " + ex.GetType().Name + " " + ex.Message);
            }
        }

        private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyer)
        {
            CaravanWatch.OnPartyDestroyed(party, destroyer);
        }

        /// <summary>
        /// A captor going through his prisoner's kit. Guarded like every other
        /// per-hero path: one bad capture must not take down an event the whole
        /// campaign fires.
        /// </summary>
        private void OnHeroPrisonerTaken(PartyBase captor, Hero prisoner)
        {
            try
            {
                PlunderService.TryPlunder(captor, prisoner);
            }
            catch (System.Exception ex)
            {
                ModLog.Error("plunder failed for " + (prisoner != null ? prisoner.Name : null)
                             + ": " + ex.GetType().Name + " " + ex.Message);
            }
        }

        /// <summary>
        /// A winner's spoils are being counted, so whatever stolen gear the
        /// losers were carrying is divided along with them. The roster the
        /// event carries is not used: StolenGoods reaches every winner's own
        /// pile through the reward model, and this party is only the signal
        /// that the division is happening. Guarded like every other event this
        /// mod listens to.
        /// </summary>
        private void OnCollectLootItems(PartyBase winner, ItemRoster gainedLoots)
        {
            try
            {
                if (!Settings.EnableCaptureLoss) return;
                StolenGoods.Recover(winner);
            }
            catch (System.Exception ex)
            {
                ModLog.Error("stolen-goods recovery failed: " + ex.GetType().Name + " " + ex.Message);
            }
        }

        /// <summary>Forgets what every clan spent yesterday, and who shopped.</summary>
        private void OnDailyTick()
        {
            _budget.StartDay();
            _shoppedToday.Clear();

            // One walk of the party list, to answer whether a caravan earns
            // more than its leader spends on himself. It is the denominator of
            // a death rate and a snapshot of what each hero-led caravan has on
            // its back -- both of which have to be taken while the caravan is
            // alive, since MobilePartyDestroyed arrives too late to read either.
            CaravanWatch.DailyTick();

            // And the caravan masters draw their commission. After the survey,
            // so the census reads the purse as it stood when the day's gear was
            // being priced rather than a moment after it was topped up.
            CaravanPurse.DailyTick();
        }

        /// <summary>
        /// A lord walks into a town: he offloads any loot he is carrying, and,
        /// if it is the first town he has entered today, buys what it has for
        /// him.
        ///
        /// Villages are skipped: their roster is food and trade goods, and the
        /// scan would find nothing while running for every party on the map.
        ///
        /// The shopper is the party's leader when there is one, not the hero the
        /// event happens to name: a caravan's leader is the companion assigned
        /// to it, and he is who spends. Falls back to the named hero for a party
        /// with no leader hero of its own.
        /// </summary>
        private void OnAfterSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            try
            {
                // A new campaign raises this for every party placed in a town
                // before the session has launched, and so before the starting
                // curve has run: a lord shopping then is judged on the sheet the
                // generator wrote, and keeps what it lets him buy. Nothing here
                // waits on the first day.
                if (_newCampaign) return;

                if (settlement == null) return;
                if (party != null && party == MobileParty.MainParty) return;

                // A lord who holds this place looks over its cells before he
                // does anything else. Towns and castles both, which is why this
                // sits above the IsTown gate -- most prisoners are in castles.
                Hero keyholder = hero;
                if (keyholder == null && party != null) keyholder = party.LeaderHero;
                PlunderService.PlunderPrisonersOf(settlement, keyholder);

                if (!settlement.IsTown) return;

                // Offloading the loot comes first and answers to nothing else.
                // It used to sit below the once-a-day gate and the shopping
                // roll, which meant a lord carrying somebody else's cuirass sold
                // it on one town visit in four, and never at all if purchases
                // were switched off -- so stolen gear stalled in baggage trains
                // instead of circulating, which is the entire point of taking
                // it. A man walking into a market with loot sells it because he
                // is standing there, not because he felt like buying something.
                //
                // A leader is required: SellAt pays party.LeaderHero, and a
                // leaderless party would hand the goods over for nothing.
                if (Settings.EnableCaptureLoss && party != null && party.LeaderHero != null)
                {
                    StolenGoods.SellAt(settlement, party.Party);
                }

                if (!Settings.EnablePurchases) return;

                Hero shopper = hero;
                if (shopper == null && party != null) shopper = party.LeaderHero;

                if (!HeroFilter.IsEligibleToShop(shopper)) return;

                string id = shopper.StringId;
                if (id != null && _shoppedToday.Contains(id)) return;

                // Marked whether or not anything is bought. Walking out of the
                // first town empty-handed still counts as the day's trip; trying
                // again at every gate would turn the cap into no cap at all.
                if (id != null) _shoppedToday.Add(id);

                // The trip's total, not just its pieces. Every purchase already
                // logs a BUY line, but those are indistinguishable from one
                // lord shopping five days running -- and whether a trip fits a
                // man out or merely improves him by a buckle is now the thing
                // worth being able to read back.
                int bought = ShoppingTrip.Shop(shopper, settlement, _budget,
                                               ClanWeight, SkillWeight, MinimumTier);
                if (bought > 0)
                {
                    ModLog.Info("TRIP hero=" + shopper.Name
                                + " at=" + settlement.Name
                                + " bought=" + bought
                                + " gaps=" + ShoppingTrip.LastTripGaps
                                + " due=" + ShoppingTrip.LastTripDue
                                + " clanGold=" + (shopper.Clan != null ? shopper.Clan.Gold : 0));
                }
            }
            catch (System.Exception ex)
            {
                // Guarded like every other per-hero path: one bad lord must not
                // take down an event the whole campaign fires.
                ModLog.Error("shopping failed for " + (hero != null ? hero.Name : null)
                             + ": " + ex.GetType().Name + " " + ex.Message);
            }
        }

        /// <summary>
        /// Each kingdom's parties, limits and armies, as the session opens and
        /// weekly. See Diagnostics.Kingdoms.
        /// </summary>
        private static void LogKingdoms()
        {
            try
            {
                List<string> kingdoms = Diagnostics.Kingdoms();
                for (int i = 0; i < kingdoms.Count; i++) ModLog.Info("KINGDOM " + kingdoms[i]);
            }
            catch (System.Exception ex)
            {
                ModLog.Error("KINGDOM failed: " + ex.GetType().Name + " " + ex.Message);
            }
        }

        private void OnWeeklyTick()
        {
            // Saddles on the wrong beast, weekly whatever the switches say, so a
            // save from before the fix can be watched mending. See
            // Diagnostics.MountFit.
            try
            {
                ModLog.Info("MOUNTFIT " + Diagnostics.MountFit());
            }
            catch (System.Exception ex)
            {
                ModLog.Error("MOUNTFIT failed: " + ex.GetType().Name + " " + ex.Message);
            }
            LogKingdoms();

            if (!Settings.EnableSkillGrowth) return;

            int grown = 0;

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (SkillGrowthService.GrowWeekly(hero)) grown++;
                }
                catch (System.Exception ex)
                {
                    // Guarded per hero, as everywhere: one bad hero must not
                    // take the tick down with it.
                    ModLog.Error("skill growth failed for " + hero.Name
                                 + ": " + ex.GetType().Name + " " + ex.Message);
                }
            }

            // Logged once a week rather than per hero: six hundred lines every
            // seven days would bury everything else in the file. The count is
            // the heroes actually grown, not every living hero the loop walked
            // past -- AllAliveHeroes carries wanderers, notables and templates,
            // and calling those grown made the line a fiction.
            // Asked against delivered. Hero.AddSkillXp multiplies the request by
            // the generic XP multiplier and by the hero's focus factor, so the
            // annual clamp in SkillGrowth caps what is requested and not what
            // arrives. The ratio is the only honest way to know how far apart
            // those two are -- see SkillGrowthService.TakeDelivered.
            int delivered, asked;
            SkillGrowthService.TakeDelivered(out delivered, out asked);

            // The calendar too, because a growth rate means nothing without it.
            // Every rate in SkillGrowth is annual and divided by the cycles a
            // year actually holds, which is the fix for an old bug: FastMode
            // runs fewer, longer weeks, and a per-cycle ceiling let it deliver a
            // third of what stock delivered. The fix has never been watched
            // working. If this number is 12 in a stock campaign and smaller
            // under FastMode, and asked-per-lord holds steady between them, it
            // is doing its job.
            // And what focus did to the order of their weapons (WeaponRank),
            // every figure against slot order, the rule before it. In a save
            // grown under that rule the AI has spent years of focus following
            // the slots, so primaryByFocus starts small there. Two readings
            // would be a bug rather than a campaign: noFocus near multiWeapon
            // means focus is not being read, and primaryByFocus at zero in a
            // new campaign means it is not being used.
            ModLog.Info("GROWTH weekly pass grew " + grown + " lords"
                        + " asked=" + asked + " delivered=" + delivered
                        + " cyclesPerYear=" + (int)SkillGrowthService.CyclesPerYear()
                        + " weapons " + SkillGrowthService.TakeWeaponOrder().Describe());
        }

        /// <summary>
        /// The starting kit.
        ///
        /// When: once per lord, on the day after TaleWorlds made his gear -- at
        /// a new campaign, when he turns 18, or when the game creates him later.
        /// See _freshlyMade; nothing else ever reaches this.
        ///
        /// Under what conditions: he is a lord and the player's switch lets the
        /// mod near his clan (HeroFilter.IsEligibleForRepair), and he looks
        /// broken (GrantService.NeedsGrant: fewer than two weapons, or clothing
        /// on his chest).
        ///
        /// A robbed man is not this. He is re-dressed by the rags, in the
        /// instant he is stripped; see Rags.
        /// </summary>
        private void TryRepair(Hero hero)
        {
            try
            {
                if (hero == null) return;
                string id = IdOf(hero);
                if (id == null) return;

                // The one trigger, read once and forgotten.
                string moment;
                if (!_freshlyMade.TryGetValue(id, out moment)) return;
                _freshlyMade.Remove(id);

                // No prisoner check, on purpose, and an audit that finds the old
                // one missing should leave it out. A lord robbed in a cell stands
                // in his rags, handed him in the instant of the robbery, so
                // NeedsGrant leaves him alone. One TaleWorlds made broken is just
                // as broken in a cell and is fixed there, which undoes no
                // robbery. Holding the note until he walked free would keep it
                // in memory for weeks, and a reload in between -- nothing here
                // is saved -- would lose his one look for good.

                if (!Settings.EnableRepair) return;
                if (!HeroFilter.IsEligibleForRepair(hero)) return;
                if (!GrantService.NeedsGrant(hero)) return;

                ModLog.Info("REPAIR hero=" + hero.Name + " moment=" + moment);
                int granted = GrantService.Grant(hero, ClanWeight, SkillWeight,
                                                 MinimumTier, DominanceMargin);

                if (granted > 0)
                {
                    // Counted only when something was actually placed: the
                    // census reports this as repairs already made, and an
                    // attempt that changed nothing is not one.
                    Diagnostics.NoteRepair();

                    // Skills as well as kit. The failure that leaves a hero
                    // without weapons leaves him without skills too, and a
                    // well-dressed lord with zero in everything is still useless
                    // and still capped at tier 1 for life.
                    int seeded = SkillSeeding.Seed(hero);
                    if (seeded > 0)
                    {
                        ModLog.Info("SEED hero=" + hero.Name + " skills=" + seeded
                                    + " talent=" + (int)(HeroTalent.For(hero) * 100));
                    }
                }
                else
                {
                    ModLog.Info("GIVEUP hero=" + hero.Name + " id=" + id
                                + " (nothing could be granted)");
                }
            }
            catch (System.Exception ex)
            {
                if (hero == null)
                {
                    ModLog.Error("TryRepair failed for <null>: " + ex.GetType().Name + " " + ex.Message);
                }
                else
                {
                    // hero.Name is a TextObject and can itself be null on a
                    // partially-initialised hero. Calling .ToString() on that
                    // null throws, which would let the NRE escape this catch
                    // into the daily tick -- exactly what the catch exists to
                    // prevent. "+" on a TextObject is null-safe (see the
                    // ModLog.Info call above), so build the message that way
                    // here too, instead of hero.Name.ToString().
                    ModLog.Error("TryRepair failed for " + hero.Name
                                 + ": " + ex.GetType().Name + " " + ex.Message);
                }
            }
        }

    }
}
