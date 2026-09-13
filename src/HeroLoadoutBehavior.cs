using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
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
        /// How likely a lord is to go shopping on any one town visit.
        ///
        /// A probability rather than a cooldown, so the spending of a clan's
        /// lords spreads out on its own instead of all landing the day a timer
        /// expires. At one purchase per trip it also sets the pace of the whole
        /// engine: a lord converges on the gear he deserves over years, paying
        /// for it, which is the point.
        /// </summary>
        internal static float ShopChancePerVisit { get { return Settings.ShopChancePerVisit; } }

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
        /// A lord crossing three towns in a day would otherwise get three rolls
        /// and could buy three times. Lords Gear caps the same way -- it polls
        /// hourly and clears a set of hero ids each morning -- and the cap is
        /// what actually sets the pace, since the probability alone only decides
        /// which visit counts.
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
            ItemCatalog.ResetSession();
            CultureProfile.Reset();
            CultureArchetypes.Reset();
            WeaponPerks.Reset();

            // Deliberately NOT subscribed to CampaignEvents.HeroComesOfAgeEvent.
            //
            // Verified from the game's IL: MbEvent<T>.AddNonSerializedListener
            // PREPENDS to the listener chain, and InvokeList walks head -> Next,
            // so the LAST listener registered runs FIRST. Our SubModule.xml
            // correctly declares dependencies on Native/SandBoxCore/Sandbox/
            // StoryMode, which means those modules' listeners are registered
            // after ours and therefore always run before ours on this event --
            // a module that plays by the dependency-declaration rules can never
            // win a race against vanilla here. AgingCampaignBehavior is itself a
            // listener on HeroComesOfAgeEvent, and its handler calls
            // EquipmentHelper.AssignHeroEquipmentFromEquipment twice, copying
            // every slot from the dummy set over whatever we had just granted.
            // There is no ordering trick from this side of the event that
            // fixes it. The DailyTickHeroEvent path below repairs the same
            // heroes one in-game day later instead, which actually works, so
            // the dead subscription is removed rather than fought.
            ModLog.Info("SETTINGS in force: " + Settings.Describe());
            ItemCatalog.ReportUnknownExclusions();

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

            // The daily tick above carries the ledger reset and nothing else.
            // The census is deliberately NOT run from it.
            // Measured at 480ms on a 600-lord campaign -- two thousand seven
            // hundred full sweeps of a 3500-item catalogue -- and it changes
            // nothing in the game. Half a second of freeze on the first day of
            // every load, to write a log file nobody is reading at the time, is
            // not a trade worth making. "hlf.census" runs it on demand.
        }

        /// <summary>Nothing is stored in the save. Deliberately empty.</summary>
        public override void SyncData(IDataStore dataStore) { }

        /// <summary>
        /// Heroes a repair could not help. NeedsGrant asks whether a hero looks
        /// wrong; it cannot know whether anything can be done about it, and the
        /// two disagree in real cases. A lord whose ceiling is tier 1 and whose
        /// body armour is tier 1 is reported broken -- tier 1 is the civilian
        /// clothing the bug leaves -- while the armour pass refuses to swap one
        /// tier-1 robe for another, so nothing changes. Likewise a hero whose
        /// culture and tier leave the catalogue with nothing to offer.
        ///
        /// Without this, such a hero is re-resolved every single in-game day for
        /// the rest of the campaign: thirteen full sweeps of a 3500-item
        /// catalogue, daily, to achieve nothing, and a REPAIR line in the log
        /// each time. Recording the failure and not trying again is the general
        /// fix; special-casing the armour tier would leave every other
        /// unsatisfiable combination looping.
        ///
        /// Not serialised: a fresh attempt on the next load is harmless and
        /// costs one resolve, and the alternative is save data this mod has so
        /// far avoided entirely.
        /// </summary>
        private readonly HashSet<string> _beyondRepair = new HashSet<string>();

        /// <summary>
        /// The hero's own object id. Not the CharacterObject's: that is unique
        /// per hero in practice, but nothing guarantees it, and a shared
        /// character would make two heroes share a give-up entry.
        /// </summary>
        private static string IdOf(Hero hero)
        {
            return hero.StringId;
        }

        private void OnDailyTickHero(Hero hero)
        {
            TryRepair(hero, "daily_tick");
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
        /// A lord walks into a town: he offloads any loot he is carrying, and
        /// may buy one thing.
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

                if (MBRandom.RandomFloat > ShopChancePerVisit) return;

                // Marked whether or not anything is bought. The roll is the
                // shopping trip; walking out empty-handed still used it up, and
                // re-rolling at the next gate would quietly multiply the rate.
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

        private void OnWeeklyTick()
        {
            if (!Settings.EnableSkillGrowth) return;

            // A hero on the give-up list was unrepairable at the ceiling he had
            // then. Ceilings rise with skill, and skill is exactly what this
            // tick moves, so the list is cleared here rather than held for the
            // session: a lord who could not be helped a year ago may be
            // helpable now, and the daily saving costs only one re-examination
            // a week to keep honest.
            _beyondRepair.Clear();

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
            ModLog.Info("GROWTH weekly pass grew " + grown + " lords"
                        + " asked=" + asked + " delivered=" + delivered
                        + " cyclesPerYear=" + (int)SkillGrowthService.CyclesPerYear());
        }

        private void TryRepair(Hero hero, string reason)
        {
            try
            {
                if (!Settings.EnableRepair) return;
                if (!HeroFilter.IsEligible(hero)) return;

                // Not while he is somebody's prisoner. Re-equipping a man in a
                // dungeon would undo a capture within a day of it happening,
                // and a lord who has just been stripped is exactly the hero
                // NeedsGrant is loudest about. He is repaired when he gets out.
                if (hero.IsPrisoner) return;

                // Repair reads the same boundary the shopping filter does. It
                // is checked here rather than in HeroFilter.IsEligible because
                // that one also gates skill growth and the census, and neither
                // should change because a player would rather pick his
                // brother's armour himself.
                if (!Settings.ManageOwnClan && hero.Clan == Clan.PlayerClan) return;

                // Asked before NeedsGrant, not after. A hero on this list is one
                // nothing can be done for, and he is looked at again every day
                // for the rest of the campaign -- reading his whole equipment
                // first, only to consult the set that says not to bother, gave
                // back most of what the set was added to save.
                string id = IdOf(hero);
                if (id != null && _beyondRepair.Contains(id)) return;

                if (!GrantService.NeedsGrant(hero)) return;

                ModLog.Info("REPAIR hero=" + hero.Name + " reason=" + reason);
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
                                    + " talent=" + (int)(Core.Talent.For(hero.StringId) * 100));
                    }
                }

                if (granted == 0 && id != null)
                {
                    _beyondRepair.Add(id);
                    ModLog.Info("GIVEUP hero=" + hero.Name + " id=" + id
                                + " (nothing could be granted; not retried this session)");
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
