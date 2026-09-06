using System.Collections.Generic;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    /// <summary>
    /// Every fixture here is a real hero from a live campaign, with the skills
    /// and the role the game reported for them. The four inversion cases are the
    /// ones that motivated reading the role at all: before this, the planner
    /// wanted to give a spear to an archer and a bow to a cavalry lord.
    /// </summary>
    public static class BattleRoleTests
    {
        private static SlotSnapshot Empty()
        {
            WeaponCategory[] weapons = new WeaponCategory[SlotSnapshot.WeaponSlotCount];
            for (int i = 0; i < weapons.Length; i++) weapons[i] = WeaponCategory.None;
            return new SlotSnapshot(weapons, false, false, false, false, false, false, false);
        }

        private static LoadoutTarget Plan(SkillProfile skills, BattleRole role, bool cultureMounted)
        {
            return LoadoutPlanner.PlanTarget(skills, Empty(), MountedRangedAvailability.All(),
                                             30, cultureMounted, role);
        }

        private static bool StartsRanged(LoadoutTarget target)
        {
            if (target.Weapons.Count == 0) return false;
            WeaponCategory first = target.Weapons[0];
            return first == WeaponCategory.Bow || first == WeaponCategory.Crossbow;
        }

        public static void RunAll()
        {
            // Merag, Battanian, formation=Ranged. Skills: 1h=139 2h=135 pole=154
            // bow=109. Polearm is his best skill by a wide margin, so the
            // skill-only planner made him a spearman -- the game says archer.
            SkillProfile merag = new SkillProfile(139, 135, 154, 109, 0, 0, 19);
            Check.True(StartsRanged(Plan(merag, BattleRole.Ranged, false)),
                     "Ranged role: archer with a stronger polearm still plans ranged");
            Check.False(StartsRanged(Plan(merag, BattleRole.Unset, false)),
                     "Unset role: same hero still falls back to the old skill rule");

            // Chaghan, Khuzait, formation=HorseArcher. pole=131 beats bow=72.
            SkillProfile chaghan = new SkillProfile(107, 96, 131, 72, 66, 56, 152);
            LoadoutTarget horseArcher = Plan(chaghan, BattleRole.HorseArcher, true);
            Check.True(StartsRanged(horseArcher), "HorseArcher role plans a ranged weapon");
            Check.True(horseArcher.WantsMount, "HorseArcher role wants a mount");

            // Zoana, Empire, formation=Cavalry. bow=81 beats her best sidearm
            // (1h=70), so the skill-only planner made her an archer.
            SkillProfile zoana = new SkillProfile(70, 70, 85, 81, 72, 76, 109);
            LoadoutTarget cavalry = Plan(zoana, BattleRole.Cavalry, true);
            Check.False(StartsRanged(cavalry), "Cavalry role plans melee even when bow outranks the sidearm");
            Check.True(cavalry.WantsMount, "Cavalry role wants a mount");
            Check.True(StartsRanged(Plan(zoana, BattleRole.Unset, true)),
                     "Unset role: same hero still becomes an archer under the old rule");

            // Ranaon, Battanian, formation=Infantry. bow=79 vs 1h=71/2h=72.
            SkillProfile ranaon = new SkillProfile(71, 72, 80, 79, 74, 77, 106);
            LoadoutTarget infantry = Plan(ranaon, BattleRole.Infantry, false);
            Check.False(StartsRanged(infantry), "Infantry role plans melee");
            Check.False(infantry.WantsMount, "Infantry role stays on foot");

            // The mount decision is the role's alone once a role is known: a
            // Cavalry lord with poor Riding still rides, and an Infantry lord of
            // a horse-fielding culture still walks.
            SkillProfile poorRider = new SkillProfile(120, 60, 60, 40, 0, 0, 15);
            Check.True(Plan(poorRider, BattleRole.Cavalry, false).WantsMount,
                     "Cavalry role overrides weak riding skill");
            Check.False(Plan(poorRider, BattleRole.Infantry, true).WantsMount,
                     "Infantry role overrides a mounted culture");

            // MountedRangedAvailability describes mounted usability only -- on
            // foot every ranged weapon is viable, which is why a Ranged role is
            // always armable and the starvation case belongs to HorseArcher.
            MountedRangedAvailability none = new MountedRangedAvailability(false, false);
            SkillProfile archer = new SkillProfile(90, 60, 60, 140, 0, 0, 30);

            Check.True(StartsRanged(LoadoutPlanner.PlanTarget(archer, Empty(), none, 30, false,
                                                              BattleRole.Ranged)),
                       "a foot archer is unaffected by mounted-only availability");

            // A horse archer the catalogue cannot arm from the saddle must still
            // end up holding something rather than nothing.
            LoadoutTarget starved = LoadoutPlanner.PlanTarget(archer, Empty(), none, 30, false,
                                                              BattleRole.HorseArcher);
            Check.True(starved.Weapons.Count > 0,
                       "HorseArcher with no mounted-viable ranged weapon still plans melee");
            Check.False(StartsRanged(starved), "...and that fallback is not a ranged weapon");

            // Role must not disturb slot arithmetic.
            List<PlannedSlot> plan = LoadoutPlanner.Plan(merag, Empty(), MountedRangedAvailability.All(),
                                                         30, false, BattleRole.Ranged);
            Check.True(plan.Count <= SlotSnapshot.WeaponSlotCount,
                     "role-driven plan never exceeds the weapon slots");
        }
    }
}
