using System.Collections.Generic;

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
                BuildRangedArchetype(target, ranged, sidearm, rangedSkill, sidearmSkill, dominanceMargin);
            }
            else
            {
                BuildMeleeArchetype(target, BestMelee(skills));
            }

            TrimToSlots(target);
            return target;
        }

        /// <summary>
        /// The whole algorithm: plan the target, reconcile it against what the
        /// hero already carries, then fill the empty slots with what remains.
        /// Nothing already equipped is ever removed.
        /// </summary>
        public static List<PlannedSlot> Plan(SkillProfile skills, SlotSnapshot current,
                                             MountedRangedAvailability availability,
                                             int dominanceMargin, bool cultureIsMounted)
        {
            List<PlannedSlot> plan = new List<PlannedSlot>();

            int[] freeSlots = current.EmptySlotIndices();
            if (freeSlots.Length == 0) return plan;

            LoadoutTarget target = PlanTarget(skills, current, availability, dominanceMargin, cultureIsMounted);
            List<WeaponCategory> wanted = Reconcile(target, current);

            int nextFree = 0;
            bool mounted = current.HasMount || target.WantsMount;

            // Step three: place what survived reconciliation. A ranged
            // weapon's ammunition is deliberately NOT auto-added here:
            // PlanTarget already emitted it as its own explicit entry, so it
            // shows up later in `wanted` (or was already satisfied by
            // Reconcile) and gets placed by its own loop iteration. The room
            // check below still uses the two-slot cost, so a ranged weapon
            // is skipped rather than placed with nothing left for the
            // ammunition that follows it.
            foreach (WeaponCategory category in wanted)
            {
                if (nextFree >= freeSlots.Length) break;
                if (!IsPlaceable(category, availability, mounted)) continue;

                int cost = CategoryRules.IsRanged(category) ? 2 : 1;
                if (freeSlots.Length - nextFree < cost) continue;

                plan.Add(new PlannedSlot(freeSlots[nextFree++], category));
            }

            // Still room: walk the skill list for anything not yet represented.
            if (nextFree < freeSlots.Length)
            {
                nextFree = FillFromSkills(plan, skills, current, availability, mounted, freeSlots, nextFree);
            }

            // Still room: courtesy filling.
            if (nextFree < freeSlots.Length)
            {
                FillCourtesy(plan, current, freeSlots, nextFree);
            }

            return plan;
        }

        /// <summary>
        /// Step two: drop target entries the hero already satisfies, and entries
        /// the current equipment contradicts.
        /// </summary>
        private static List<WeaponCategory> Reconcile(LoadoutTarget target, SlotSnapshot current)
        {
            List<WeaponCategory> remaining = new List<WeaponCategory>();

            // Count what the hero already has so duplicated target entries (two
            // quivers) are only satisfied once each.
            Dictionary<WeaponCategory, int> have = new Dictionary<WeaponCategory, int>();
            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                WeaponCategory c = current.WeaponAt(i);
                if (c == WeaponCategory.None) continue;
                have[c] = have.ContainsKey(c) ? have[c] + 1 : 1;
            }

            foreach (WeaponCategory category in target.Weapons)
            {
                if (have.ContainsKey(category) && have[category] > 0)
                {
                    have[category] = have[category] - 1;
                    continue;
                }

                // A shield is pointless next to a two-handed weapon.
                if (category == WeaponCategory.Shield && current.HasTwoHandedEquipped) continue;

                remaining.Add(category);
            }

            return remaining;
        }

        private static bool IsPlaceable(WeaponCategory category,
                                        MountedRangedAvailability availability, bool mounted)
        {
            if (category == WeaponCategory.None) return false;
            if (mounted && !availability.IsViable(category)) return false;
            return true;
        }

        private static int FillFromSkills(List<PlannedSlot> plan, SkillProfile skills, SlotSnapshot current,
                                          MountedRangedAvailability availability, bool mounted,
                                          int[] freeSlots, int nextFree)
        {
            SkillKind[] order = skills.CombatSkillsDescending();

            foreach (SkillKind skill in order)
            {
                if (nextFree >= freeSlots.Length) break;
                if (skills.Get(skill) <= 0) continue;

                WeaponCategory category = CategoryForSkill(skill);
                if (current.Contains(category)) continue;
                if (AlreadyPlanned(plan, category)) continue;
                if (!IsPlaceable(category, availability, mounted)) continue;

                int cost = CategoryRules.IsRanged(category) ? 2 : 1;
                if (freeSlots.Length - nextFree < cost) continue;

                plan.Add(new PlannedSlot(freeSlots[nextFree++], category));
                if (cost == 2)
                {
                    plan.Add(new PlannedSlot(freeSlots[nextFree++], CategoryRules.AmmoFor(category)));
                }
            }

            return nextFree;
        }

        /// <summary>
        /// Last resort: a shield, then spare ammunition, then nothing. Leaving a
        /// slot empty is a valid outcome and beats equipping something incoherent.
        /// </summary>
        private static void FillCourtesy(List<PlannedSlot> plan, SlotSnapshot current,
                                         int[] freeSlots, int nextFree)
        {
            bool shieldPossible = !current.Contains(WeaponCategory.Shield)
                                  && !AlreadyPlanned(plan, WeaponCategory.Shield)
                                  && !current.HasTwoHandedEquipped
                                  && !PlanIntroducesTwoHanded(plan);

            if (shieldPossible && nextFree < freeSlots.Length)
            {
                plan.Add(new PlannedSlot(freeSlots[nextFree++], WeaponCategory.Shield));
            }

            WeaponCategory ammo = SpareAmmoKind(plan, current);
            while (ammo != WeaponCategory.None && nextFree < freeSlots.Length && CountPlanned(plan, ammo) < 2)
            {
                plan.Add(new PlannedSlot(freeSlots[nextFree++], ammo));
            }
        }

        private static bool PlanIntroducesTwoHanded(List<PlannedSlot> plan)
        {
            foreach (PlannedSlot p in plan)
            {
                if (CategoryRules.IsTwoHanded(p.Category)) return true;
            }
            return false;
        }

        /// <summary>The ammunition kind matching whatever ranged weapon is in play, or None.</summary>
        private static WeaponCategory SpareAmmoKind(List<PlannedSlot> plan, SlotSnapshot current)
        {
            if (current.Contains(WeaponCategory.Bow) || AlreadyPlanned(plan, WeaponCategory.Bow))
                return WeaponCategory.Arrows;
            if (current.Contains(WeaponCategory.Crossbow) || AlreadyPlanned(plan, WeaponCategory.Crossbow))
                return WeaponCategory.Bolts;
            return WeaponCategory.None;
        }

        private static bool AlreadyPlanned(List<PlannedSlot> plan, WeaponCategory category)
        {
            foreach (PlannedSlot p in plan)
            {
                if (p.Category == category) return true;
            }
            return false;
        }

        private static int CountPlanned(List<PlannedSlot> plan, WeaponCategory category)
        {
            int n = 0;
            foreach (PlannedSlot p in plan)
            {
                if (p.Category == category) n++;
            }
            return n;
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
                                                 int rangedSkill, int sidearmSkill, int dominanceMargin)
        {
            WeaponCategory ammo = CategoryRules.AmmoFor(ranged);

            target.Weapons.Add(ranged);
            target.Weapons.Add(ammo);

            bool sidearmIsTwoHanded = CategoryRules.IsTwoHanded(sidearm);
            bool dominant = (rangedSkill - sidearmSkill) >= dominanceMargin;

            // A shield is only worth a slot beside a one-handed sidearm, and
            // only when the hero is not a dedicated shooter. Whether the
            // hero is mounted has no bearing on this: a horse archer can
            // carry bow, ammunition, sword and shield exactly like a foot
            // archer. The mount only ever gates whether the ranged weapon
            // itself is usable (see MountedRangedAvailability), never the
            // shield decision. Otherwise the fourth slot is a second quiver.
            bool takesShield = !sidearmIsTwoHanded && !dominant;

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

        /// <summary>Maps a skill to the weapon category it would buy.</summary>
        public static WeaponCategory CategoryForSkill(SkillKind skill)
        {
            switch (skill)
            {
                case SkillKind.TwoHanded: return WeaponCategory.TwoHandedSword;
                case SkillKind.Polearm: return WeaponCategory.Spear;
                case SkillKind.Bow: return WeaponCategory.Bow;
                case SkillKind.Crossbow: return WeaponCategory.Crossbow;
                case SkillKind.Throwing: return WeaponCategory.Throwing;
                default: return WeaponCategory.OneHandedSword;
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
