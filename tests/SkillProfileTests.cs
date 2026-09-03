using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class SkillProfileTests
    {
        public static void RunAll()
        {
            // oneHanded, twoHanded, polearm, bow, crossbow, throwing, riding
            SkillProfile p = new SkillProfile(120, 40, 200, 90, 10, 60, 150);

            Check.Equal(120, p.Get(SkillKind.OneHanded), "Get OneHanded");
            Check.Equal(150, p.Get(SkillKind.Riding), "Get Riding");
            Check.Equal(200, p.MaxCombatSkill, "MaxCombatSkill excludes Riding");

            SkillKind[] order = p.CombatSkillsDescending();
            Check.Equal(6, order.Length, "six weapon skills returned");
            Check.Equal((int)SkillKind.Polearm, (int)order[0], "highest is Polearm");
            Check.Equal((int)SkillKind.OneHanded, (int)order[1], "second is OneHanded");
            Check.Equal((int)SkillKind.Bow, (int)order[2], "third is Bow");
            Check.Equal((int)SkillKind.Throwing, (int)order[3], "fourth is Throwing");
            Check.Equal((int)SkillKind.TwoHanded, (int)order[4], "fifth is TwoHanded");
            Check.Equal((int)SkillKind.Crossbow, (int)order[5], "sixth is Crossbow");

            // Riding 150 is behind Polearm 200 only, so it is in the top two.
            Check.True(p.RidingInTopTwo(), "Riding in top two");

            SkillProfile footman = new SkillProfile(200, 180, 170, 20, 10, 160, 30);
            Check.False(footman.RidingInTopTwo(), "low Riding not in top two");

            // Ties break by enum order: OneHanded before TwoHanded.
            SkillProfile tied = new SkillProfile(100, 100, 0, 0, 0, 0, 0);
            SkillKind[] tiedOrder = tied.CombatSkillsDescending();
            Check.Equal((int)SkillKind.OneHanded, (int)tiedOrder[0], "tie breaks to OneHanded");
            Check.Equal((int)SkillKind.TwoHanded, (int)tiedOrder[1], "tie second is TwoHanded");

            // Riding is the highest of all seven here, so this fixture fails if
            // MaxCombatSkill ever starts folding Riding into the maximum.
            SkillProfile ridingHighest = new SkillProfile(120, 40, 100, 90, 10, 60, 250);
            Check.Equal(120, ridingHighest.MaxCombatSkill, "MaxCombatSkill ignores a dominant Riding");

            // Exactly two weapon skills beat Riding, so it ranks third of seven.
            // This is the boundary: a threshold of two instead of one fails here.
            SkillProfile thirdPlace = new SkillProfile(200, 180, 50, 20, 10, 30, 100);
            Check.False(thirdPlace.RidingInTopTwo(), "two better weapon skills puts Riding third");

            // A weapon skill equal to Riding is not strictly better, so Riding keeps its place.
            SkillProfile tiedWithRiding = new SkillProfile(150, 150, 50, 20, 10, 30, 150);
            Check.True(tiedWithRiding.RidingInTopTwo(), "ties do not push Riding out of the top two");
        }
    }
}
