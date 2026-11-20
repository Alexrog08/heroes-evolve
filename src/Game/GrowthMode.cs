using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace HeroesEvolve
{
    /// <summary>
    /// Which skill growth runs in this campaign: the mod's, by age and talent,
    /// or the game's own, by focus points. Fixed when the campaign loads.
    ///
    /// The two cannot share a campaign. Bannerlord v1.5.7 added
    /// HeroDailyXpCampaignBehavior, which gives every lord, companion and
    /// wanderer experience each day in every skill with focus, and the mod's
    /// curve run on top of it means nothing: a young lord the curve sets at
    /// his age keeps the focus and attributes his sheet was written with, sits
    /// far below his learning limit, and the game carries him back to that
    /// sheet in a decade. Simulated from the game's own formulas, a lord of 20
    /// written at 217 and set to 116 is back at 218 by 30.
    ///
    /// So the player chooses, and the choice is kept for the whole session.
    /// The game's behaviour can only be taken out while the campaign is being
    /// built: SandBoxManager.Initialize adds it before any module's
    /// OnGameStart, and the behaviour manager registers its events after. Read
    /// live, a switch moved mid-campaign would leave both systems running, or
    /// neither, until the next load. Read once, it does what the options screen
    /// says: it takes effect when a campaign is loaded.
    ///
    /// Taking it out is safe for a save. Its SyncData is empty, it does nothing
    /// but listen to the daily hero tick, and no other code in the game or its
    /// DLC asks for it. A campaign loaded later without this mod simply gets it
    /// back.
    /// </summary>
    public static class GrowthMode
    {
        /// <summary>True when the mod's growth runs in this campaign.</summary>
        public static bool HeroesEvolve { get; private set; } = true;

        /// <summary>
        /// Settles the mode for the campaign being built, and takes the game's
        /// daily experience out when the mod's growth replaces it.
        /// </summary>
        public static void Apply(CampaignGameStarter starter)
        {
            HeroesEvolve = Settings.EnableSkillGrowth;
            if (starter == null) return;

            try
            {
                if (HeroesEvolve) starter.RemoveBehaviors<HeroDailyXpCampaignBehavior>();

                ModLog.Info("GROWTH mode=" + Name() + " gameDailyXp="
                            + (Present(starter) ? "kept" : "removed"));
            }
            catch (System.Exception ex)
            {
                // A campaign that cannot load is the one outcome worse than two
                // growth systems at once. The census line shows which it was.
                ModLog.Error("GROWTH could not settle the mode: " + ex.GetType().Name + " " + ex.Message);
            }
        }

        /// <summary>The mode on one line, for the census.</summary>
        public static string Describe()
        {
            bool present = Campaign.Current != null
                           && Campaign.Current.GetCampaignBehavior<HeroDailyXpCampaignBehavior>() != null;

            // The two halves must disagree: the mod's growth with the game's
            // still running, or the game's mode with it gone, is a broken load.
            return "mode=" + Name() + " gameDailyXp=" + (present ? "running" : "removed")
                   + " (expect " + (HeroesEvolve ? "removed" : "running") + ")";
        }

        private static string Name()
        {
            return HeroesEvolve ? "heroesEvolve" : "game";
        }

        private static bool Present(CampaignGameStarter starter)
        {
            foreach (CampaignBehaviorBase behavior in starter.CampaignBehaviors)
            {
                if (behavior is HeroDailyXpCampaignBehavior) return true;
            }
            return false;
        }
    }
}
