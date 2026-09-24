using System.Collections.Generic;

namespace HeroesEvolve.Core
{
    /// <summary>
    /// Ammunition built for something other than a battle.
    ///
    /// The NavalDLC ships stealth_arrow, "Whisper Arrows", for its stealth
    /// missions: they fit any bow and carry a +2 damage bonus, and they fly at
    /// half the speed of every other arrow and bolt in the game (5 against
    /// 10). Missile speed is range and it is most of the damage a shot lands
    /// with, so in a field battle they are worse arrows than the cheapest
    /// quiver on sale. The market read only the +2, rated them an upgrade, and
    /// lords were buying them -- 87 purchases in one log.
    ///
    /// Judged against the other ammunition installed, not against a number.
    /// Mods that rework ballistics -- Realistic Battle Mod is the popular one
    /// -- change every arrow's speed together, so a fixed threshold would
    /// refuse nothing or everything. Measured against the median of what is
    /// installed, a stealth arrow is at half of ordinary whatever the scale,
    /// and a heavy war arrow a little slower than the rest is nowhere near it.
    /// </summary>
    public static class MissileRules
    {
        /// <summary>
        /// Refused at or below this share of the ordinary speed. A half: the
        /// one such item in the game sits exactly there, and no arrow meant for
        /// a battle comes anywhere close.
        /// </summary>
        public const int SlowSharePercent = 50;

        /// <summary>
        /// Whether a missile this fast is too slow for a battle, given how fast
        /// ordinary ammunition flies. A speed of zero means the item states
        /// none, and says nothing either way.
        /// </summary>
        public static bool IsSlow(int speed, int ordinary)
        {
            if (speed <= 0 || ordinary <= 0) return false;
            return speed * 100 <= ordinary * SlowSharePercent;
        }

        /// <summary>
        /// The ordinary speed: the median of the speeds given, ignoring any
        /// that are not stated. Zero when nothing is.
        ///
        /// The median rather than the mean, so that one special arrow cannot
        /// drag down the line it is being measured against.
        /// </summary>
        public static int Ordinary(IEnumerable<int> speeds)
        {
            List<int> known = new List<int>();
            if (speeds != null)
            {
                foreach (int speed in speeds)
                {
                    if (speed > 0) known.Add(speed);
                }
            }

            if (known.Count == 0) return 0;

            known.Sort();
            return known[known.Count / 2];
        }
    }
}
