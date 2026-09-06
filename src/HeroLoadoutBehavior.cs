using TaleWorlds.CampaignSystem;

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

        public override void RegisterEvents()
        {
            // A fresh behaviour instance is built per campaign load, but the
            // diagnostics counter is static and outlives one campaign.
            Diagnostics.ResetSession();
            CultureProfile.Reset();

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
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        /// <summary>
        /// Runs the diagnostic census exactly once per session, on the first
        /// daily tick rather than at load: by the time a day ticks, every
        /// campaign object and the item catalogue are fully initialised.
        /// </summary>
        private bool _censusDone;

        private void OnDailyTick()
        {
            if (_censusDone) return;
            _censusDone = true;

            try
            {
                Diagnostics.RunCensus(ClanWeight, SkillWeight, MinimumTier, DominanceMargin);
            }
            catch (System.Exception ex)
            {
                ModLog.Error("census failed: " + ex.GetType().Name + " " + ex.Message);
            }
        }

        /// <summary>Nothing is stored in the save. Deliberately empty.</summary>
        public override void SyncData(IDataStore dataStore) { }

        private void OnDailyTickHero(Hero hero)
        {
            TryRepair(hero, "daily_tick");
        }

        private void TryRepair(Hero hero, string reason)
        {
            try
            {
                if (!HeroFilter.IsEligible(hero)) return;
                if (!GrantService.NeedsGrant(hero)) return;

                ModLog.Info("REPAIR hero=" + hero.Name + " reason=" + reason);
                GrantService.Grant(hero, ClanWeight, SkillWeight, MinimumTier, DominanceMargin);
                Diagnostics.NoteRepair();
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
