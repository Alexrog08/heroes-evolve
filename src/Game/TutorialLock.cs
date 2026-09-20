using System;
using System.Runtime.CompilerServices;
using StoryMode.StoryModePhases;

namespace HeroesEvolve
{
    /// <summary>
    /// Whether the tutorial still has the player shut out of the towns.
    ///
    /// One question, asked by one caller: PlunderService.CanBeStripped, which
    /// lets a captor take everything the player is wearing on the reasoning
    /// that he can go and dress himself. That reasoning is sound for the whole
    /// of a campaign except its first hour. The story mode's opening phase
    /// refuses him entry to every town until he finishes it, and the fight it
    /// sends him to -- Radagos in his hideout -- is one he can lose. Bandits
    /// rob half their prisoners at the default rate, so a new campaign had a
    /// coin-flip chance of leaving him with nothing and no shop to fix it in,
    /// for as long as the tutorial lasted.
    ///
    /// The comment above CanBeStripped already described exactly this and drew
    /// the line in the wrong place: it kept a wandering troubadour safe because
    /// nothing in the mod could redress him, and left the player out because he
    /// could redress himself. He can, once the map opens. Until then he is the
    /// troubadour.
    ///
    /// Isolated behind NoInlining and a catch for the same reason McmBridge is.
    /// StoryMode is a declared dependency and so is always loaded beside us, but
    /// a campaign can be started without a tutorial at all and TutorialPhase
    /// has no instance then. A throw here must read as "nothing is locked"
    /// rather than take the robbery system down with it.
    /// </summary>
    public static class TutorialLock
    {
        /// <summary>
        /// True while the opening phase is running and unfinished. False for a
        /// sandbox campaign, for a tutorial the player skipped, for one he has
        /// completed, and for anything this cannot read.
        /// </summary>
        public static bool LocksTheMap()
        {
            try
            {
                return Locked();
            }
            catch (Exception ex)
            {
                // Once would be enough, but this runs on capture rather than on
                // a tick, so the log will not fill with it.
                ModLog.Info("tutorial phase unreadable (" + ex.GetType().Name
                            + "); treating the map as open");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Locked()
        {
            TutorialPhase phase = TutorialPhase.Instance;
            if (phase == null) return false;

            // Either flag ends it. Skipping is the player saying he wants the
            // ordinary campaign, and the towns open with it.
            return !phase.IsCompleted && !phase.IsSkipped;
        }
    }
}
