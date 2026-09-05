using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// Everything GrantService decided for one hero, with nothing written yet.
    /// This exists so the diagnostic dry-run and the real grant share one code
    /// path: a diagnostic that computes its answer separately would drift from
    /// the behaviour it claims to report on, which is worse than no diagnostic.
    /// </summary>
    public sealed class ResolvedGrant
    {
        public ResolvedGrant()
        {
            Slots = new List<ResolvedSlot>();
        }

        public int ClanTier;
        public int MaxCombatSkill;
        public int Ceiling;
        public bool CultureFieldsMountedElites;
        public bool WantsMount;

        /// <summary>What the planner assumed: already mounted, or about to be.</summary>
        public bool Mounted;

        public MountedRangedAvailability Availability;
        public CultureObject Culture;

        /// <summary>How many weapon placements the planner produced.</summary>
        public int PlannedWeaponCount;

        /// <summary>
        /// The archetype the planner chose, before reconciliation, as text.
        /// Logged because a plan can only be judged against what it intended:
        /// a slot holding a shield is correct or wrong depending entirely on
        /// whether the target asked for one.
        /// </summary>
        public string TargetWeapons;

        /// <summary>The categories actually placed, in slot order.</summary>
        public string PlacedWeapons;

        /// <summary>What the hero's weapon slots looked like going in.</summary>
        public string CurrentWeapons;

        public List<ResolvedSlot> Slots { get; private set; }

        /// <summary>Slots that would actually receive an item.</summary>
        public int WouldGrantCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Slots.Count; i++)
                {
                    if (Slots[i].WouldWrite) n++;
                }
                return n;
            }
        }
    }

    /// <summary>One slot's decision: what we want there and what we found.</summary>
    public sealed class ResolvedSlot
    {
        /// <summary>Short slot name for the log: w0..w3, Horse, Harness, Head...</summary>
        public string Label;

        /// <summary>The category or armour type wanted, as text.</summary>
        public string Want;

        public EquipmentIndex Slot;

        /// <summary>The item that would be placed, or null when none was found.</summary>
        public ItemObject Item;

        /// <summary>Name of whatever occupies the slot right now, or null.</summary>
        public string Existing;

        /// <summary>
        /// Set when the slot is deliberately left alone -- already filled, or
        /// the plan does not want anything here. Carries the reason.
        /// </summary>
        public string SkipReason;

        public bool WouldWrite
        {
            get { return SkipReason == null && Item != null; }
        }
    }
}
