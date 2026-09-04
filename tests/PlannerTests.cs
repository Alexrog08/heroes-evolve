using System.Collections.Generic;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class PlannerTests
    {
        private static bool Plans(List<PlannedSlot> plan, WeaponCategory c)
        {
            foreach (PlannedSlot p in plan) { if (p.Category == c) return true; }
            return false;
        }

        private static int CountPlanned(List<PlannedSlot> plan, WeaponCategory c)
        {
            int n = 0;
            foreach (PlannedSlot p in plan) { if (p.Category == c) n++; }
            return n;
        }

        private static bool PlansAnyTwoHanded(List<PlannedSlot> plan)
        {
            foreach (PlannedSlot p in plan) { if (CategoryRules.IsTwoHanded(p.Category)) return true; }
            return false;
        }

        public static void RunAll()
        {
            MountedRangedAvailability all = MountedRangedAvailability.All();

            // Spec case 1: shield + spear + one-hander, one free slot, Throwing third.
            SlotSnapshot infantry = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.Spear, WeaponCategory.Shield, WeaponCategory.OneHandedSword, WeaponCategory.None },
                false, false, true, true, true, true, false);
            SkillProfile throwerSkills = new SkillProfile(200, 30, 210, 20, 10, 150, 40);
            List<PlannedSlot> p1 = LoadoutPlanner.Plan(throwerSkills, infantry, all, 30, false);
            Check.Equal(1, p1.Count, "one slot planned");
            Check.True(Plans(p1, WeaponCategory.Throwing), "throwing fills the gap");

            // Spec case 2: same, but Bow is third. Cost two, only one slot free.
            //
            // Skill order here is Polearm(210), OneHanded(200), Bow(160),
            // TwoHanded(150), Throwing(20), Crossbow(10) (confirmed by
            // running CombatSkillsDescending on this profile). Polearm and
            // OneHanded are skipped for free (Spear and OneHandedSword are
            // already equipped on `infantry`), Bow is skipped for not
            // fitting -- that much was always the point of this fixture.
            //
            // Before the shield-vs-two-handed guard, the walk then landed on
            // TwoHanded, the 4th-ranked skill, and this assertion checked
            // for TwoHandedSword. But `infantry` already has a Shield
            // equipped (see case 1 above), and the guard added in
            // FillFromSkills to fix the defect where it could plant a
            // two-handed weapon beside a planned or equipped shield now
            // correctly skips TwoHanded too -- the same domain rule
            // Reconcile (case 12) and FillCourtesy already enforce. The walk
            // falls through one skill further, to Throwing, which fits the
            // one free slot and gets placed. "Falls through to the next
            // skill" is still exactly what happens; it is just Throwing, not
            // TwoHandedSword, once the shield guard is applied everywhere it
            // belongs. Confirmed by running the fixture: p2 is exactly one
            // entry, WeaponCategory.Throwing at slot 3.
            SkillProfile bowThirdSkills = new SkillProfile(200, 150, 210, 160, 10, 20, 40);
            List<PlannedSlot> p2 = LoadoutPlanner.Plan(bowThirdSkills, infantry, all, 30, false);
            Check.False(Plans(p2, WeaponCategory.Bow), "bow does not fit in one slot");
            Check.False(Plans(p2, WeaponCategory.TwoHandedSword), "two-hander is skipped: a shield is already equipped");
            Check.True(Plans(p2, WeaponCategory.Throwing), "falls through past the guarded two-hander to the next skill");

            // Spec case 3: two free slots and Bow third -> bow plus ammo.
            //
            // The brief's own fixture reused bowThirdSkills (best melee skill:
            // Polearm, so PlanTarget always yields {Spear, Shield}) on a
            // snapshot with Spear+Shield already equipped. That combination is
            // unsatisfiable within four slots: Shield has no governing skill,
            // so FillFromSkills can never skip it "for free" the way it skips
            // an already-equipped weapon skill -- it is only ever free when
            // literally equipped. Equipping Spear+Shield+OneHanded (the skill
            // that outranks Bow here) to clear a path leaves one free slot,
            // which is exactly case 2, not two. Confirmed by running the
            // literal brief fixture: it fails ("bow fits in two slots:
            // expected true"), because the fixture asks the four-slot budget
            // for something it cannot produce, not because Plan is wrong.
            //
            // This fixture keeps the same intent -- Bow is the hero's
            // third-best skill, and with two free slots its two-slot cost
            // fits -- using a TwoHanded-dominant profile instead. Facing a
            // two-handed weapon, BuildMeleeArchetype never adds a Shield (see
            // LoadoutPlanner.BuildMeleeArchetype), so both archetype entries
            // (TwoHandedSword, OneHanded) are ordinary skill-driven categories
            // that can be equipped and then skipped for free, leaving the
            // full two free slots open when the walk reaches Bow.
            SkillProfile twoHandedBowThirdSkills = new SkillProfile(150, 200, 10, 100, 5, 5, 0);
            SlotSnapshot twoFree = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.TwoHandedSword, WeaponCategory.OneHandedSword, WeaponCategory.None, WeaponCategory.None },
                false, false, true, true, true, true, false);
            List<PlannedSlot> p3 = LoadoutPlanner.Plan(twoHandedBowThirdSkills, twoFree, all, 30, false);
            Check.True(Plans(p3, WeaponCategory.Bow), "bow fits in two slots");
            Check.True(Plans(p3, WeaponCategory.Arrows), "ammo planned alongside");

            // Spec case 9: nothing empty means nothing planned.
            SlotSnapshot full = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.Spear, WeaponCategory.Shield, WeaponCategory.OneHandedSword, WeaponCategory.Throwing },
                false, false, true, true, true, true, false);
            List<PlannedSlot> p4 = LoadoutPlanner.Plan(throwerSkills, full, all, 30, false);
            Check.Equal(0, p4.Count, "no empty slots, no plan");

            // Spec case 11: the eighteen-year-old with the vanilla dummy set.
            SlotSnapshot dummy = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.OneHandedSword, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                false, false, false, true, true, false, false);
            SkillProfile archerSkills = new SkillProfile(60, 20, 10, 200, 0, 0, 0);
            List<PlannedSlot> p5 = LoadoutPlanner.Plan(archerSkills, dummy, all, 30, false);
            Check.True(Plans(p5, WeaponCategory.Bow), "dummy-set archer gets a bow");
            Check.Equal(2, CountPlanned(p5, WeaponCategory.Arrows), "and two quivers");
            Check.False(Plans(p5, WeaponCategory.OneHandedSword), "the spatha is not duplicated");
            Check.Equal(3, p5.Count, "exactly the three free slots are used");

            // Spec case 12: the target wants a shield but a two-hander is equipped.
            SlotSnapshot twoHander = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.TwoHandedSword, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                false, false, true, true, true, true, false);
            SkillProfile hybridSkills = new SkillProfile(190, 200, 20, 180, 0, 0, 0);
            List<PlannedSlot> p6 = LoadoutPlanner.Plan(hybridSkills, twoHander, all, 30, false);
            Check.False(Plans(p6, WeaponCategory.Shield), "shield dropped beside a two-hander");

            // Spec case 8: a completely naked hero gets a coherent four-slot loadout.
            SlotSnapshot naked = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.None, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                false, false, false, false, false, false, false);
            List<PlannedSlot> p7 = LoadoutPlanner.Plan(archerSkills, naked, all, 30, false);
            Check.Equal(4, p7.Count, "naked hero fills all four slots");

            // Spec case 14: a flagged bow on a mounted hero is not viable.
            MountedRangedAvailability noBow = new MountedRangedAvailability(false, false);
            SlotSnapshot mountedNaked = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.None, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                true, true, false, false, false, false, false);
            List<PlannedSlot> p8 = LoadoutPlanner.Plan(archerSkills, mountedNaked, noBow, 30, false);
            Check.False(Plans(p8, WeaponCategory.Bow), "unusable bow is never planned");

            // No plan ever targets an occupied slot.
            foreach (PlannedSlot slot in p5)
            {
                Check.Equal((int)WeaponCategory.None, (int)dummy.WeaponAt(slot.SlotIndex), "planned slot was empty");
            }

            // --- Extra boundary coverage beyond the brief's fixtures ---
            // None of the eight cases above happen to leave a hero holding
            // exactly one of a two-quiver target, force a ranged weapon to
            // compete for a slot it cannot fully pay for, exhaust the skill
            // walk without exhausting the free slots, or hand Plan an
            // all-zero profile end to end. A broken implementation can
            // satisfy every case above and still mishandle each of these;
            // the fixtures below each isolate one such gap.

            SkillProfile dedicatedArcherSkills = new SkillProfile(100, 20, 10, 200, 0, 0, 0);

            // Reconcile must consume exactly one of the target's two planned
            // quivers when the hero already carries one, not both (a
            // presence-only check, ignoring the count, would wrongly treat a
            // single carried quiver as satisfying both target entries) and
            // not neither (the bow itself must not be re-planned either).
            SlotSnapshot oneQuiverCarried = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.Bow, WeaponCategory.Arrows, WeaponCategory.None, WeaponCategory.None },
                false, false, true, true, true, true, false);
            List<PlannedSlot> pQuiver = LoadoutPlanner.Plan(dedicatedArcherSkills, oneQuiverCarried, all, 30, false);
            Check.Equal(1, CountPlanned(pQuiver, WeaponCategory.Arrows), "only the missing second quiver is planned, not both and not neither");
            Check.False(Plans(pQuiver, WeaponCategory.Bow), "the already-carried bow is not re-planned");
            Check.True(Plans(pQuiver, WeaponCategory.OneHandedSword), "the sidearm still fills the other free slot");
            Check.Equal(2, pQuiver.Count, "both free slots are used, nothing wasted");

            // A ranged weapon must never be placed in the last free slot
            // with no room left for its ammunition -- not by itself (empty
            // quiver forever) and not as an orphan quiver with no bow
            // anywhere either. One slot is free; everything the hero
            // already carries is irrelevant to the target, so the wanted
            // list here is just the archetype's own Bow entry.
            SlotSnapshot noRoomForAmmo = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.Arrows, WeaponCategory.Arrows, WeaponCategory.OneHandedSword, WeaponCategory.None },
                false, false, true, true, true, true, false);
            List<PlannedSlot> pNoRoom = LoadoutPlanner.Plan(dedicatedArcherSkills, noRoomForAmmo, all, 30, false);
            Check.False(Plans(pNoRoom, WeaponCategory.Bow), "a ranged weapon is never placed when its ammunition would not also fit");
            Check.Equal(0, CountPlanned(pNoRoom, WeaponCategory.Arrows), "and no orphan ammunition is placed for it either");
            Check.Equal(1, pNoRoom.Count, "the last slot is still filled coherently instead of left reserved");
            Check.True(Plans(pNoRoom, WeaponCategory.TwoHandedSword), "falling through to the next skill that actually fits");

            // The courtesy filler must run, and do something useful, once
            // the skill walk is exhausted with free slots still open. Only
            // OneHanded is nonzero here, and it is already equipped, so
            // FillFromSkills walks all six skills and offers nothing new.
            // The hero already carries a bow with no ammunition at all;
            // courtesy recognising that and topping up a quiver is the only
            // thing that can still fill the remaining slot.
            SkillProfile oneHandedOnlySkills = new SkillProfile(150, 0, 0, 0, 0, 0, 0);
            SlotSnapshot bowWithNoAmmo = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.Bow, WeaponCategory.OneHandedSword, WeaponCategory.None, WeaponCategory.None },
                false, false, true, true, true, true, false);
            List<PlannedSlot> pCourtesyAmmo = LoadoutPlanner.Plan(oneHandedOnlySkills, bowWithNoAmmo, all, 30, false);
            Check.True(Plans(pCourtesyAmmo, WeaponCategory.Arrows), "courtesy tops up ammo for the carried bow once the skill walk offers nothing more");
            Check.Equal(2, pCourtesyAmmo.Count, "both remaining slots are used: the wanted shield, then the courtesy quiver");
            Check.False(Plans(pCourtesyAmmo, WeaponCategory.TwoHandedSword), "nothing stray gets introduced along the way");

            // A hero whose every one of the six weapon skills is zero must
            // still get PlanTarget's sane default (one-hander plus shield,
            // see ArchetypeTests) carried all the way through reconcile and
            // fill -- and, with two slots left over and nothing else to
            // offer, must leave them empty rather than invent something.
            SkillProfile allZeroSkills = new SkillProfile(0, 0, 0, 0, 0, 0, 0);
            SlotSnapshot allZeroNaked = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.None, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                false, false, false, false, false, false, false);
            List<PlannedSlot> pAllZero = LoadoutPlanner.Plan(allZeroSkills, allZeroNaked, all, 30, false);
            Check.True(Plans(pAllZero, WeaponCategory.OneHandedSword), "an all-zero profile still plans a plain sidearm");
            Check.True(Plans(pAllZero, WeaponCategory.Shield), "and a shield beside it");
            Check.Equal(2, pAllZero.Count, "the other two empty slots are left empty rather than filled with something incoherent");

            // Defect fix pin: a naked dedicated archer must end with a bow,
            // exactly two quivers, and a sidearm -- four distinct slots. The
            // Step-3 placement loop must not auto-add a ranged weapon's
            // ammunition on top of what PlanTarget already emitted
            // explicitly, or the auto-added quiver crowds out the sidearm
            // (bow plus three quivers, no sword).
            List<PlannedSlot> pDedicatedArcher = LoadoutPlanner.Plan(dedicatedArcherSkills, naked, all, 30, false);
            Check.Equal(4, pDedicatedArcher.Count, "all four slots are used");
            Check.True(Plans(pDedicatedArcher, WeaponCategory.Bow), "the bow is planned");
            Check.Equal(2, CountPlanned(pDedicatedArcher, WeaponCategory.Arrows), "exactly two quivers, not three");
            Check.True(Plans(pDedicatedArcher, WeaponCategory.OneHandedSword), "the sidearm is planned, not crowded out by a phantom quiver");

            // Defect fix pin: a shield beside a two-handed weapon is one of
            // the mod's core rules. Reconcile enforces it (case 12 above)
            // and FillCourtesy enforces it (PlanIntroducesTwoHanded), but
            // FillFromSkills had no equivalent guard. An ordinary naked
            // hero with OneHanded=100 and TwoHanded=50 (everything else,
            // including Riding, zero) is a plain melee archetype: PlanTarget
            // yields {OneHandedSword, Shield} (BuildMeleeArchetype), and
            // Step 3 places both at slots 0 and 1. The skill walk then skips
            // OneHanded (already planned) but, without a guard, happily
            // plants TwoHandedSword -- the hero's second-best skill -- right
            // beside the shield already sitting in the plan.
            SkillProfile weakTwoHandedSkills = new SkillProfile(100, 50, 0, 0, 0, 0, 0);
            List<PlannedSlot> pShieldVsTwoHanded = LoadoutPlanner.Plan(weakTwoHandedSkills, naked, all, 30, false);
            Check.False(Plans(pShieldVsTwoHanded, WeaponCategory.Shield) && PlansAnyTwoHanded(pShieldVsTwoHanded),
                "plan never carries both a shield and a two-handed category");
        }
    }
}
