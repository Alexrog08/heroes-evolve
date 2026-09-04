namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// Whether the market or catalogue holds a bow / crossbow this hero could
    /// actually use from horseback. Computed outside the planner because it
    /// depends on item flags and perks, which the pure core must not see.
    /// </summary>
    public struct MountedRangedAvailability
    {
        public bool BowViable;
        public bool CrossbowViable;

        public MountedRangedAvailability(bool bowViable, bool crossbowViable)
        {
            BowViable = bowViable;
            CrossbowViable = crossbowViable;
        }

        /// <summary>Everything is viable. Correct for a hero on foot.</summary>
        public static MountedRangedAvailability All()
        {
            return new MountedRangedAvailability(true, true);
        }
    }
}
