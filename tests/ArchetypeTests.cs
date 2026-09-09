using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class ArchetypeTests
    {
        private static SlotSnapshot Naked(bool mounted)
        {
            return new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.None, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                mounted, false, false, false, false, false, false);
        }

        private static int CountOf(LoadoutTarget t, WeaponCategory c)
        {
            int n = 0;
            foreach (WeaponCategory w in t.Weapons) { if (w == c) n++; }
            return n;
        }

        public static void RunAll()
        {
            MountedRangedAvailability all = MountedRangedAvailability.All();
            MountedRangedAvailability noCrossbow = new MountedRangedAvailability(true, false);

            // Dedicated foot archer with a one-handed sidearm: two ammo, no shield.
            SkillProfile dedicated = new SkillProfile(100, 20, 10, 200, 0, 0, 0);
            LoadoutTarget t1 = LoadoutPlanner.PlanTarget(dedicated, Naked(false), all, 30, false);
            Check.True(t1.Weapons.Contains(WeaponCategory.Bow), "dedicated archer takes a bow");
            Check.Equal(2, CountOf(t1, WeaponCategory.Arrows), "dedicated archer takes two quivers");
            Check.False(t1.Weapons.Contains(WeaponCategory.Shield), "dedicated archer takes no shield");
            Check.True(t1.Weapons.Contains(WeaponCategory.OneHandedSword), "sidearm is one-handed");

            // Soldier who also shoots: one ammo plus a shield.
            SkillProfile hybrid = new SkillProfile(180, 20, 10, 190, 0, 0, 0);
            LoadoutTarget t2 = LoadoutPlanner.PlanTarget(hybrid, Naked(false), all, 30, false);
            Check.Equal(1, CountOf(t2, WeaponCategory.Arrows), "hybrid takes one quiver");
            Check.True(t2.Weapons.Contains(WeaponCategory.Shield), "hybrid takes a shield");

            // Two-handed sidearm drops the shield and restores the second quiver.
            SkillProfile twoHandedArcher = new SkillProfile(20, 190, 10, 200, 0, 0, 0);
            LoadoutTarget t3 = LoadoutPlanner.PlanTarget(twoHandedArcher, Naked(false), all, 30, false);
            Check.Equal(2, CountOf(t3, WeaponCategory.Arrows), "two-handed sidearm means two quivers");
            Check.False(t3.Weapons.Contains(WeaponCategory.Shield), "no shield beside a two-hander");
            Check.True(t3.Weapons.Contains(WeaponCategory.TwoHandedSword), "sidearm is two-handed");

            // Polearm is never an archer's sidearm, however high the skill.
            SkillProfile polearmArcher = new SkillProfile(30, 20, 250, 200, 0, 0, 0);
            LoadoutTarget t4 = LoadoutPlanner.PlanTarget(polearmArcher, Naked(false), all, 30, false);
            Check.False(t4.Weapons.Contains(WeaponCategory.Polearm), "polearm never accompanies a bow");
            Check.False(t4.Weapons.Contains(WeaponCategory.Spear), "spear never accompanies a bow either");

            // Mounted crossbowman with only heavy crossbows available degrades to a bow.
            SkillProfile crossbowman = new SkillProfile(80, 20, 10, 120, 220, 0, 200);
            LoadoutTarget t5 = LoadoutPlanner.PlanTarget(crossbowman, Naked(true), noCrossbow, 30, false);
            Check.False(t5.Weapons.Contains(WeaponCategory.Crossbow), "unusable crossbow is dropped");
            Check.True(t5.Weapons.Contains(WeaponCategory.Bow), "degrades to a bow");

            // With a light crossbow available it keeps the crossbow.
            LoadoutTarget t6 = LoadoutPlanner.PlanTarget(crossbowman, Naked(true), all, 30, false);
            Check.True(t6.Weapons.Contains(WeaponCategory.Crossbow), "viable crossbow is kept");

            // A mounted archer below the dominance margin takes a shield,
            // exactly like a foot archer: the mount only ever gates whether
            // the ranged weapon itself is usable, never the shield decision.
            SkillProfile horseArcher = new SkillProfile(180, 20, 10, 190, 0, 0, 220);
            LoadoutTarget t7 = LoadoutPlanner.PlanTarget(horseArcher, Naked(true), all, 30, false);
            Check.Equal(1, CountOf(t7, WeaponCategory.Arrows), "mounted archer below the dominance margin takes one quiver");
            Check.True(t7.Weapons.Contains(WeaponCategory.Shield), "mounted archer below the dominance margin takes a shield");

            // Melee dominant with one-handed: weapon plus shield.
            SkillProfile infantry = new SkillProfile(220, 40, 60, 20, 0, 100, 0);
            LoadoutTarget t8 = LoadoutPlanner.PlanTarget(infantry, Naked(false), all, 30, false);
            Check.True(t8.Weapons.Contains(WeaponCategory.OneHandedSword), "infantry takes a one-hander");
            Check.True(t8.Weapons.Contains(WeaponCategory.Shield), "infantry takes a shield");

            // Two-handed dominant: no shield.
            SkillProfile berserker = new SkillProfile(120, 240, 40, 10, 0, 90, 0);
            LoadoutTarget t9 = LoadoutPlanner.PlanTarget(berserker, Naked(false), all, 30, false);
            Check.True(t9.Weapons.Contains(WeaponCategory.TwoHandedSword), "berserker takes a two-hander");
            Check.False(t9.Weapons.Contains(WeaponCategory.Shield), "berserker takes no shield");

            // Mount is wanted when Riding is in the top two.
            Check.True(t7.WantsMount, "high Riding wants a mount");
            Check.False(t8.WantsMount, "low Riding does not want a mount");

            // Mount is also wanted when the culture fields mounted elites --
            // provided the hero can actually ride. The `infantry` fixture has
            // Riding 0, which is below every war horse in the game, so culture
            // alone no longer puts him on one; this fixture is him with just
            // enough riding to be carried.
            SkillProfile infantryWhoRides = new SkillProfile(220, 40, 60, 20, 0, 100,
                                                             LoadoutPlanner.MinimumRidingForWarMount);
            LoadoutTarget t10 = LoadoutPlanner.PlanTarget(infantryWhoRides, Naked(false), all, 30, true);
            Check.True(t10.WantsMount, "mounted culture wants a mount");

            LoadoutTarget t10b = LoadoutPlanner.PlanTarget(infantry, Naked(false), all, 30, true);
            Check.False(t10b.WantsMount, "...but not for a hero who cannot ride at all");

            // The target never exceeds the four weapon slots.
            Check.True(t1.Weapons.Count <= 4, "target fits in four slots");
            Check.True(t9.Weapons.Count <= 4, "melee target fits in four slots");

            // --- Extra boundary coverage beyond the brief's fixtures ---
            // None of the ten fixtures above land exactly on the dominance
            // margin, tie ranged against melee, use an all-zero profile, tie
            // OneHanded against TwoHanded while the archer archetype is
            // chosen, or combine "mounted" with "nothing rideable to shoot
            // with". A broken implementation can satisfy every fixture above
            // and still be wrong on each of these; the cases below each
            // isolate one such gap.

            // dominanceMargin's ">=" boundary: the gap between ranged and
            // sidearm skill lands exactly on the margin (130 - 100 = 30). A
            // ">" instead of ">=" would treat this hero as not yet dominant
            // and hand back a shield the dominant-foot-archer rule forbids.
            SkillProfile marginBoundary = new SkillProfile(100, 20, 10, 130, 0, 0, 0);
            LoadoutTarget t11 = LoadoutPlanner.PlanTarget(marginBoundary, Naked(false), all, 30, false);
            Check.False(t11.Weapons.Contains(WeaponCategory.Shield), "a gap exactly at the dominance margin already counts as dominant");
            Check.Equal(2, CountOf(t11, WeaponCategory.Arrows), "and so takes a second quiver instead of a shield");

            // Ranged skill and the one-handed sidearm skill tied exactly: the
            // archetype decision uses ">=", so the tie must still resolve to
            // the archer, not fall through to a plain melee target with no
            // bow at all.
            SkillProfile tiedSkills = new SkillProfile(150, 20, 10, 150, 0, 0, 0);
            LoadoutTarget t12 = LoadoutPlanner.PlanTarget(tiedSkills, Naked(false), all, 30, false);
            Check.True(t12.Weapons.Contains(WeaponCategory.Bow), "a tie between ranged and sidearm skill still favors the archer");

            // Every one of the six weapon skills at zero. No brief fixture
            // leaves the hero with nothing to be good at; this is the
            // fallback path that must still hand back a sane default rather
            // than an empty loadout or a stray WeaponCategory.None.
            SkillProfile allZero = new SkillProfile(0, 0, 0, 0, 0, 0, 0);
            LoadoutTarget t13 = LoadoutPlanner.PlanTarget(allZero, Naked(false), all, 30, false);
            Check.True(t13.Weapons.Contains(WeaponCategory.OneHandedSword), "an all-zero profile still defaults to a plain sidearm");
            Check.True(t13.Weapons.Contains(WeaponCategory.Shield), "and a shield beside it");

            // OneHanded and TwoHanded tied exactly while the archer archetype
            // is in play: the sidearm rule requires TwoHanded to be strictly
            // greater, so a tie must still resolve to OneHanded.
            SkillProfile tiedSidearm = new SkillProfile(100, 100, 10, 150, 0, 0, 0);
            LoadoutTarget t14 = LoadoutPlanner.PlanTarget(tiedSidearm, Naked(false), all, 30, false);
            Check.True(t14.Weapons.Contains(WeaponCategory.OneHandedSword), "a tied sidearm skill resolves to one-handed");
            Check.False(t14.Weapons.Contains(WeaponCategory.TwoHandedSword), "and not to two-handed");

            // Mounted with neither a bow nor a crossbow viable: even a hero
            // who is excellent with both must fall back to a pure melee
            // target, carrying no ranged weapon or ammunition at all.
            MountedRangedAvailability noneViable = new MountedRangedAvailability(false, false);
            SkillProfile deadeye = new SkillProfile(80, 20, 10, 250, 240, 0, 0);
            LoadoutTarget t15 = LoadoutPlanner.PlanTarget(deadeye, Naked(true), noneViable, 30, false);
            Check.False(t15.Weapons.Contains(WeaponCategory.Bow), "no viable bow while mounted means no bow");
            Check.False(t15.Weapons.Contains(WeaponCategory.Crossbow), "no viable crossbow while mounted means no crossbow either");
            Check.False(t15.Weapons.Contains(WeaponCategory.Arrows), "and so no arrows");
            Check.False(t15.Weapons.Contains(WeaponCategory.Bolts), "nor bolts");

            // The project owner's correction: a mounted archer below the
            // dominance margin ends up with exactly the loadout an
            // equivalent hero on foot would get, shield included. Mounted
            // only ever gates which ranged weapon is usable
            // (MountedRangedAvailability), never the shield decision.
            SkillProfile belowMarginArcher = new SkillProfile(150, 20, 10, 170, 0, 0, 0);
            LoadoutTarget tFootArcher = LoadoutPlanner.PlanTarget(belowMarginArcher, Naked(false), all, 30, false);
            LoadoutTarget tMountedArcher = LoadoutPlanner.PlanTarget(belowMarginArcher, Naked(true), all, 30, false);
            Check.True(tMountedArcher.Weapons.Contains(WeaponCategory.Shield), "mounted archer below the margin takes a shield, same as on foot");
            Check.Equal(1, CountOf(tMountedArcher, WeaponCategory.Arrows), "and only one quiver, matching the shield trade-off");
            Check.Equal(tFootArcher.Weapons.Count, tMountedArcher.Weapons.Count, "mounted and foot loadouts have the same slot count");
            for (int i = 0; i < tFootArcher.Weapons.Count; i++)
            {
                Check.Equal((int)tFootArcher.Weapons[i], (int)tMountedArcher.Weapons[i],
                    "mounted archer matches the on-foot loadout at slot " + i);
            }

            // BestViableRanged checks Crossbow before Bow (see
            // LoadoutPlanner.BestViableRanged), so an exact tie between the
            // two silently favors the crossbow. That was previously an
            // accident with no fixture pinning it either way; this test
            // puts the current behavior on record as a decision. Bow and
            // Crossbow are tied at 150, both strictly above the OneHanded
            // sidearm (50) and Riding (0), so neither the dominance margin
            // nor mount state is in play here -- only the tie-break.
            SkillProfile tiedRanged = new SkillProfile(50, 20, 10, 150, 150, 0, 0);
            LoadoutTarget t16 = LoadoutPlanner.PlanTarget(tiedRanged, Naked(false), all, 30, false);
            Check.True(t16.Weapons.Contains(WeaponCategory.Crossbow), "an exact bow/crossbow skill tie favors the crossbow (documented, current behavior)");
            Check.False(t16.Weapons.Contains(WeaponCategory.Bow), "and not the bow, so a tie can never plan both");
        }
    }
}
