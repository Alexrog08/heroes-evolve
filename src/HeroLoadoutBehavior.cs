using System.Collections.Generic;
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

        private static string IdOf(Hero hero)
        {
            return hero.CharacterObject != null && hero.CharacterObject.StringId != null
                ? hero.CharacterObject.StringId
                : hero.StringId;
        }

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

                string id = IdOf(hero);
                if (id != null && _beyondRepair.Contains(id)) return;

                ModLog.Info("REPAIR hero=" + hero.Name + " reason=" + reason);
                int granted = GrantService.Grant(hero, ClanWeight, SkillWeight,
                                                 MinimumTier, DominanceMargin);
                Diagnostics.NoteRepair();

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
