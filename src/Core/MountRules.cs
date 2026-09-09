namespace HeroesEvolve.Core
{
    /// <summary>
    /// Whether a weapon can be used from horseback, decided from the names of
    /// the ways it can be wielded.
    ///
    /// The game knows this exactly and will not say. Each weapon mode carries an
    /// item usage set id, and Native's item_usage_sets.xml gives some of those
    /// sets a "requires_no_mount" flag -- a pike is braced against the ground,
    /// a long bow cannot be drawn past a horse's neck. But the string to flags
    /// lookup lives in native code: WeaponComponentData exposes only ItemUsage,
    /// the bare string, and nothing in the managed API maps it to the flag. So
    /// the flag has to be mirrored here, read out of the game's own data file.
    ///
    /// Read from item_usage_sets.xml (v1.4.8), which defines 58 sets; these are
    /// every one carrying requires_no_mount, counting the flag a set inherits
    /// through base_set -- which is how long_bow gets it while plain bow does
    /// not. A modded set with its own dismounted-only name would be missed,
    /// which is a far smaller failure than the one this fixes and the only one
    /// available without parsing every module's XML at load.
    /// </summary>
    public static class MountRules
    {
        /// <summary>
        /// The item usage sets the game forbids on a mount.
        ///
        /// Ordered as the data file lists them. The first was found the
        /// expensive way -- a mounted Vlandian king holding a noble_long_bow he
        /// could not draw -- and the other four came from reading the file that
        /// would have said so all along.
        /// </summary>
        private static readonly string[] Dismounted = new string[]
        {
            "long_bow",
            "onehanded_shield_dagger",
            "polearm_bracing",
            "polearm_pike",
            "polearm_thrown",
        };

        /// <summary>
        /// The item usage sets the game allows ONLY from a mount.
        ///
        /// One set, and the same file says so: a couched lance is braced against
        /// the horse's momentum and means nothing standing still. Kept separate
        /// from the list above rather than folded into a single table, because
        /// these two are not opposites -- a weapon may be neither, and a lance
        /// that also thrusts is both couchable and perfectly usable on foot.
        /// </summary>
        private static readonly string[] MountedOnly = new string[]
        {
            "polearm_couch",
        };

        /// <summary>Whether this one way of wielding the weapon needs both feet on the ground.</summary>
        public static bool IsDismountedOnly(string usage)
        {
            if (string.IsNullOrEmpty(usage)) return false;

            for (int i = 0; i < Dismounted.Length; i++)
            {
                if (Dismounted[i] == usage) return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a rider can use this weapon at all, given every mode it
        /// offers.
        ///
        /// One usable mode is enough, and that is the whole subtlety. A weapon
        /// is not one thing: the game's Javelin template builds an item that is
        /// both a thrown weapon and a short spear, and a two-handed polearm can
        /// come out of the forge couchable and braceable at once. Asking
        /// whether ANY mode is dismounted-only would strip a horseman of his
        /// javelins because they can also be poked with on foot. The question
        /// is whether EVERY mode needs the ground.
        ///
        /// An unnamed usage counts as usable. Some weapons carry an empty usage
        /// string, and refusing what cannot be read would ban gear on the
        /// strength of not knowing -- the wrong direction to fail in, since the
        /// cost of a wrong yes is one awkward weapon and the cost of a wrong no
        /// is a whole class of gear silently withheld.
        /// </summary>
        public static bool AllowsMounted(string[] usages)
        {
            if (usages == null || usages.Length == 0) return true;

            bool sawOne = false;
            for (int i = 0; i < usages.Length; i++)
            {
                if (usages[i] == null) continue;
                sawOne = true;
                if (!IsDismountedOnly(usages[i])) return true;
            }

            return !sawOne;
        }

        /// <summary>Whether this one way of wielding the weapon needs a mount under it.</summary>
        public static bool IsMountedOnly(string usage)
        {
            if (string.IsNullOrEmpty(usage)) return false;

            for (int i = 0; i < MountedOnly.Length; i++)
            {
                if (MountedOnly[i] == usage) return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a man on foot can use this weapon at all.
        ///
        /// The mirror of AllowsMounted and the same shape: one usable mode is
        /// enough, so an ordinary lance -- couchable and also a thrusting spear
        /// -- passes, and only a weapon with nothing but a couch would not.
        ///
        /// Added on principle rather than on evidence. No vanilla item is known
        /// to offer the couch and nothing else, so this is expected to reject
        /// nothing at all today; it exists because the rule it completes is
        /// "give a man a weapon he can use", and a rule enforced in one
        /// direction only is a rule half kept.
        /// </summary>
        public static bool AllowsOnFoot(string[] usages)
        {
            if (usages == null || usages.Length == 0) return true;

            bool sawOne = false;
            for (int i = 0; i < usages.Length; i++)
            {
                if (usages[i] == null) continue;
                sawOne = true;
                if (!IsMountedOnly(usages[i])) return true;
            }

            return !sawOne;
        }
    }
}
