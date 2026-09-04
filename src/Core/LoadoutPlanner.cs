namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// Decides what a hero should carry. Pure: no game types, fully testable.
    /// </summary>
    public static class LoadoutPlanner
    {
        /// <summary>
        /// Step one of the algorithm: the ideal loadout for this hero, computed
        /// from skills alone and independent of what they currently carry.
        /// </summary>
        public static LoadoutTarget PlanTarget(SkillProfile skills, SlotSnapshot current,
                                               MountedRangedAvailability availability,
                                               int dominanceMargin, bool cultureIsMounted)
        {
            LoadoutTarget target = new LoadoutTarget();
            target.WantsMount = skills.RidingInTopTwo() || cultureIsMounted;

            bool mounted = current.HasMount || target.WantsMount;

            WeaponCategory ranged = BestViableRanged(skills, availability, mounted);
            int rangedSkill = ranged == WeaponCategory.None ? 0 : skills.Get(SkillForCategory(ranged));

            // The archer's only candidate sidearm is One/Two-Handed (Polearm is
            // deliberately excluded, see ArcherSidearm), so that pair is also
            // the correct yardstick for whether ranged beats melee here.
            // Comparing against the hero's true best melee skill instead would
            // let a dominant Polearm skill pull the hero into an archer
            // archetype whose sidearm could never actually reflect it.
            WeaponCategory sidearm = ArcherSidearm(skills);
            int sidearmSkill = skills.Get(SkillForCategory(sidearm));

            if (ranged != WeaponCategory.None && rangedSkill >= sidearmSkill)
            {
                BuildRangedArchetype(target, ranged, sidearm, rangedSkill, sidearmSkill, dominanceMargin, mounted);
            }
            else
            {
                BuildMeleeArchetype(target, BestMelee(skills));
            }

            TrimToSlots(target);
            return target;
        }

        /// <summary>
        /// The archer's sidearm is the better of OneHanded and TwoHanded.
        /// Polearm is deliberately excluded: two quivers and a spear is not a
        /// loadout anyone actually fields.
        /// </summary>
        private static WeaponCategory ArcherSidearm(SkillProfile skills)
        {
            return skills.Get(SkillKind.TwoHanded) > skills.Get(SkillKind.OneHanded)
                ? WeaponCategory.TwoHandedSword
                : WeaponCategory.OneHandedSword;
        }

        private static void BuildRangedArchetype(LoadoutTarget target, WeaponCategory ranged, WeaponCategory sidearm,
                                                 int rangedSkill, int sidearmSkill, int dominanceMargin, bool mounted)
        {
            WeaponCategory ammo = CategoryRules.AmmoFor(ranged);

            target.Weapons.Add(ranged);
            target.Weapons.Add(ammo);

            bool sidearmIsTwoHanded = CategoryRules.IsTwoHanded(sidearm);
            bool dominant = (rangedSkill - sidearmSkill) >= dominanceMargin;

            // A shield is only worth a slot beside a one-handed sidearm, and
            // only when the hero is not a dedicated shooter and is fighting
            // on foot. Otherwise the fourth slot is a second quiver.
            bool takesShield = !sidearmIsTwoHanded && !dominant && !mounted;

            target.Weapons.Add(takesShield ? WeaponCategory.Shield : ammo);
            target.Weapons.Add(sidearm);
        }

        private static void BuildMeleeArchetype(LoadoutTarget target, WeaponCategory best)
        {
            if (best == WeaponCategory.None) best = WeaponCategory.OneHandedSword;

            target.Weapons.Add(best);

            if (CategoryRules.IsTwoHanded(best))
            {
                // No shield: it would never come off the back.
                target.Weapons.Add(WeaponCategory.OneHandedSword);
            }
            else
            {
                target.Weapons.Add(WeaponCategory.Shield);
            }

            // Remaining slots are filled later by the gap-fill pass (Task 6),
            // which walks the skill list with the same viability rules.
        }

        /// <summary>
        /// The better of Bow/Crossbow that this hero could actually use given
        /// their mount state, or None if neither qualifies.
        /// </summary>
        private static WeaponCategory BestViableRanged(SkillProfile skills,
                                                       MountedRangedAvailability availability,
                                                       bool mounted)
        {
            int bow = skills.Get(SkillKind.Bow);
            int crossbow = skills.Get(SkillKind.Crossbow);

            // On foot, mounted viability never applies; mounted, only what
            // MountedRangedAvailability actually confirms can be used.
            bool bowOk = !mounted || availability.BowViable;
            bool crossbowOk = !mounted || availability.CrossbowViable;

            WeaponCategory best = WeaponCategory.None;
            int bestValue = 0;

            if (crossbowOk && crossbow > bestValue) { best = WeaponCategory.Crossbow; bestValue = crossbow; }
            if (bowOk && bow > bestValue) { best = WeaponCategory.Bow; bestValue = bow; }

            return best;
        }

        /// <summary>
        /// Best melee weapon across all three melee skills, including
        /// Polearm. Used only once the hero has already lost the archer
        /// comparison in PlanTarget, where Polearm is not a candidate.
        /// </summary>
        private static WeaponCategory BestMelee(SkillProfile skills)
        {
            int oneHanded = skills.Get(SkillKind.OneHanded);
            int twoHanded = skills.Get(SkillKind.TwoHanded);
            int polearm = skills.Get(SkillKind.Polearm);

            if (twoHanded >= oneHanded && twoHanded >= polearm && twoHanded > 0) return WeaponCategory.TwoHandedSword;
            if (polearm >= oneHanded && polearm > 0) return WeaponCategory.Spear;
            if (oneHanded > 0) return WeaponCategory.OneHandedSword;
            return WeaponCategory.None;
        }

        /// <summary>Maps a planned category back to the skill that governs it.</summary>
        private static SkillKind SkillForCategory(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.TwoHandedSword:
                case WeaponCategory.TwoHandedAxe:
                    return SkillKind.TwoHanded;
                case WeaponCategory.Spear:
                case WeaponCategory.Polearm:
                    return SkillKind.Polearm;
                case WeaponCategory.Bow:
                case WeaponCategory.Arrows:
                    return SkillKind.Bow;
                case WeaponCategory.Crossbow:
                case WeaponCategory.Bolts:
                    return SkillKind.Crossbow;
                case WeaponCategory.Throwing:
                    return SkillKind.Throwing;
                default:
                    return SkillKind.OneHanded;
            }
        }

        /// <summary>Safety net for the slot-count invariant; the archetypes
        /// built above never actually produce more than four entries.</summary>
        private static void TrimToSlots(LoadoutTarget target)
        {
            while (target.Weapons.Count > SlotSnapshot.WeaponSlotCount)
            {
                target.Weapons.RemoveAt(target.Weapons.Count - 1);
            }
        }
    }
}
