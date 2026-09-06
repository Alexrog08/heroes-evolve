using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// Subscribes to the event that matters for the grant path and does the
    /// per-hero work. Every hero is wrapped in try/catch: one bad hero must
    /// never take down the tick.
    /// </summary>
    public class HeroLoadoutBehavior : CampaignBehaviorBase
    {
        // Defaults from the design spec, section 6 and 11.
        internal const float ClanWeight = 0.5f;
        internal const float SkillWeight = 1.0f;
        internal const int MinimumTier = 1;
        internal const int DominanceMargin = 30;

        /// <summary>
        /// How likely a lord is to go shopping on any one town visit.
        ///
        /// A probability rather than a cooldown, so the spending of a clan's
        /// lords spreads out on its own instead of all landing the day a timer
        /// expires. At one purchase per trip it also sets the pace of the whole
        /// engine: a lord converges on the gear he deserves over years, paying
        /// for it, which is the point.
        /// </summary>
        internal const float ShopChancePerVisit = 0.25f;

        /// <summary>
        /// The day's gear spending, per clan. Owned here because a behaviour
        /// instance is built fresh per campaign load, which is exactly the
        /// lifetime this ledger should have -- it holds no save data.
        /// </summary>
        private readonly BudgetService _budget = new BudgetService();

        public override void RegisterEvents()
        {
            // A fresh behaviour instance is built per campaign load, but the
            // diagnostics counter is static and outlives one campaign.
            Diagnostics.ResetSession();
            CultureProfile.Reset();
            CultureArchetypes.Reset();

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

            // Deliberately NOT subscribed to DailyTickEvent to run the census.
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

        /// <summary>Forgets what every clan spent yesterday.</summary>
        private void OnDailyTick()
        {
            _budget.StartDay();
        }

        /// <summary>
        /// A lord walks into a town and may buy one thing.
        ///
        /// Villages are skipped: their roster is food and trade goods, and the
        /// scan would find nothing while running for every party on the map.
        /// </summary>
        private void OnAfterSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            try
            {
                if (settlement == null || !settlement.IsTown) return;
                if (!HeroFilter.IsEligibleToShop(hero)) return;
                if (MBRandom.RandomFloat > ShopChancePerVisit) return;

                ShoppingTrip.Shop(hero, settlement, _budget, ClanWeight, SkillWeight, MinimumTier);
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
            int grown = 0;

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    SkillGrowthService.GrowWeekly(hero);
                    grown++;
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
            // seven days would bury everything else in the file.
            ModLog.Info("GROWTH weekly pass over " + grown + " heroes");
        }

        private void TryRepair(Hero hero, string reason)
        {
            try
            {
                if (!HeroFilter.IsEligible(hero)) return;
                if (!GrantService.NeedsGrant(hero)) return;

                string id = IdOf(hero);
                if (id != null && _beyondRepair.Contains(id)) return;

                ModLog.Info("REPAIR hero=" + hero.Name + " reason=" + reason);
                int granted = GrantService.Grant(hero, ClanWeight, SkillWeight,
                                                 MinimumTier, DominanceMargin);

                // Counted only when something was actually placed: the census
                // reports this as repairs already made, and an attempt that
                // changed nothing is not one.
                if (granted > 0) Diagnostics.NoteRepair();

                if (granted > 0)
                {
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
