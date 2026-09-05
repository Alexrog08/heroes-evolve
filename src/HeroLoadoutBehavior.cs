using TaleWorlds.CampaignSystem;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// Subscribes to the two events that matter for the grant path and does the
    /// per-hero work. Every hero is wrapped in try/catch: one bad hero must
    /// never take down the tick.
    /// </summary>
    public class HeroLoadoutBehavior : CampaignBehaviorBase
    {
        // Defaults from the design spec, section 6 and 11.
        private const float ClanWeight = 0.5f;
        private const float SkillWeight = 1.0f;
        private const int MinimumTier = 1;
        private const int DominanceMargin = 30;

        public override void RegisterEvents()
        {
            CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, OnHeroComesOfAge);
            CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, OnDailyTickHero);
        }

        /// <summary>Nothing is stored in the save. Deliberately empty.</summary>
        public override void SyncData(IDataStore dataStore) { }

        private void OnHeroComesOfAge(Hero hero)
        {
            TryRepair(hero, "came_of_age");
        }

        private void OnDailyTickHero(Hero hero)
        {
            TryRepair(hero, "daily_tick");
        }

        private void TryRepair(Hero hero, string reason)
        {
            try
            {
                if (!IsEligible(hero)) return;
                if (!GrantService.NeedsGrant(hero)) return;

                ModLog.Info("REPAIR hero=" + hero.Name + " reason=" + reason);
                GrantService.Grant(hero, ClanWeight, SkillWeight, MinimumTier, DominanceMargin);
            }
            catch (System.Exception ex)
            {
                ModLog.Error("TryRepair failed for "
                             + (hero == null ? "<null>" : hero.Name.ToString())
                             + ": " + ex.GetType().Name + " " + ex.Message);
            }
        }

        private static bool IsEligible(Hero hero)
        {
            if (hero == null) return false;
            if (hero.IsDead) return false;
            if (hero.IsHumanPlayerCharacter) return false;
            if (hero == Hero.MainHero) return false;
            if (hero.IsChild) return false;
            if (!hero.IsLord) return false;
            return true;
        }
    }
}
