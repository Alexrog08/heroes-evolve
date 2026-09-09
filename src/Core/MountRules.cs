namespace HeroesEvolve.Core
{
    /// <summary>
    /// Whether a weapon can be used from horseback, decided from the names of
    /// the ways it can be wielded.
    ///
    /// The game knows this exactly and will not say. Each way of wielding a
    /// weapon carries an item usage set id, and Native's item_usage_sets.xml
    /// gives some of those sets a "requires_no_mount" flag. But the string to
    /// flags lookup lives in native code -- WeaponComponentData exposes only
    /// ItemUsage, the bare string -- so the flag is mirrored here, read out of
    /// the game's own data.
    ///
    /// The flag marks a MODE, not a weapon, and that distinction is the whole
    /// rule. Bracing a spear against the ground is something you do standing
    /// still; couching one is something you do at a gallop. Neither says
    /// anything about the weapon as a whole, and a spear that can be braced is
    /// still a perfectly good spear on a horse -- you simply cannot brace from
    /// up there. So the question is never whether a weapon HAS a dismounted
    /// mode. It is whether it has anything else.
    ///
    /// That is why the base game has exactly one foot-only weapon family. Of
    /// its twelve crafting templates only Pike offers nothing but dismounted
    /// modes, pike and bracing; TwoHandedPolearm offers couch, bracing and
    /// thrown alongside ordinary thrusts and is fine on a horse. Checked
    /// template by template against the game's own flags: twelve of twelve
    /// agree.
    ///
    /// The rule below is the one the game's own interface uses to draw the
    /// "cannot use on horseback" icon -- CampaignUIHelper.GetItemUsageSetFlagDetails
    /// tests RequiresNoMount and then lets the Bow.HorseMaster perk lift it.
    ///
    /// A modded set with its own dismounted-only name would be missed, which is
    /// the only failure available without parsing every module's XML at load.
    /// </summary>
    public static class MountRules
    {
        /// <summary>
        /// The item usage sets the game forbids on a mount.
        ///
        /// Ordered as the data file lists them. The first was found the
        /// expensive way -- a mounted Vlandian king holding a noble_long_bow he
        /// could not draw -- and the other four came from reading the file that
        /// would have said so all along. Four of the five are modes that other
        /// modes sit beside, so on their own they bar almost nothing: only the
        /// pike, whose every mode is here, is refused a rider outright.
        /// </summary>
        private static readonly string[] Dismounted = new string[]
        {
            "long_bow",
            "onehanded_shield_dagger",
            "polearm_bracing",
            "polearm_pike",
            "polearm_thrown",
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

    }
}
