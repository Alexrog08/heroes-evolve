using TaleWorlds.CampaignSystem;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// The game's answers about a captive, handed to the pure ledger.
    ///
    /// The episode comes from Hero.CaptivityStartTime, the same figure
    /// PlunderService seeds its castle draw with: TakePrisonerAction writes it
    /// on every capture, so it identifies this spell in the cells and not the
    /// last one. Hours because NumTicks is not public and ToHours is.
    ///
    /// See RobberyLedger for why any of this is kept at all.
    /// </summary>
    public static class CaptivityRobberies
    {
        public static void ResetSession()
        {
            RobberyLedger.Clear();
        }

        public static void Record(Hero prisoner)
        {
            long episode;
            if (!EpisodeOf(prisoner, out episode)) return;
            RobberyLedger.Record(prisoner.StringId, episode);
        }

        public static bool Consume(Hero prisoner)
        {
            long episode;
            if (!EpisodeOf(prisoner, out episode)) return false;
            return RobberyLedger.Consume(prisoner.StringId, episode);
        }

        private static bool EpisodeOf(Hero prisoner, out long episode)
        {
            episode = 0L;
            if (prisoner == null || prisoner.StringId == null) return false;

            try
            {
                episode = (long)prisoner.CaptivityStartTime.ToHours;
                return true;
            }
            catch
            {
                // A hero the game cannot date is a hero we say nothing about.
                return false;
            }
        }
    }
}
