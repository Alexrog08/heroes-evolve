using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// Puts every lord where his years and talent put him, once, as a new
    /// campaign begins. StartingCurve says why and where; this is how.
    ///
    /// Everything goes through the game's own routines, and the order matters:
    ///
    ///   1. Skills, through HeroDeveloper.SetInitialSkillLevel -- the call the
    ///      game makes when it builds a hero, and the only public one that can
    ///      lower a skill. ChangeSkillLevel cannot: it turns the change into
    ///      experience, and AddSkillXp ignores anything that is not positive.
    ///   2. Perks he no longer meets the requirement for. There is no public way
    ///      to drop a single perk, so all are cleared and the ones he still
    ///      earns are given back; the game's own daily selection fills in the
    ///      rest as he grows.
    ///   3. Level and points, through InitializeHeroDeveloper, again the game's
    ///      own routine. It recomputes his total experience from the new skills
    ///      -- the one step with no public setter -- climbs his level from zero,
    ///      and sets his unspent points to what that level earns minus the focus
    ///      and attributes he already holds. Nobody loses a point of either: a
    ///      lord who now stands lower simply has nothing left to spend, and one
    ///      who stands higher receives what those levels give, spent by the game
    ///      as if he had climbed there himself.
    ///
    /// New campaigns only. This mod writes nothing into a save, so running it on
    /// every load would put every lord back on the curve each time and undo
    /// whatever he had earned since. Run once before the first day, the result
    /// is simply part of the campaign the game saves.
    /// </summary>
    public static class StartingCurvePass
    {
        private sealed class Tally
        {
            public int Heroes;
            public int Raised;
            public int Lowered;
            public int PerksDropped;
            public int Failed;
            public readonly List<int> LevelBefore = new List<int>();
            public readonly List<int> LevelAfter = new List<int>();
            public readonly List<int> BestBefore = new List<int>();
            public readonly List<int> BestAfter = new List<int>();
        }

        public static void Apply()
        {
            Tally tally = new Tally();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (!HeroFilter.IsEligibleToGrow(hero)) continue;
                    if (hero.HeroDeveloper == null || hero.BattleEquipment == null) continue;

                    int levelBefore = hero.Level;
                    int bestBefore = HeroAdapter.ReadSkills(hero).MaxCombatSkill;

                    ApplyTo(hero, tally);

                    tally.Heroes++;
                    tally.LevelBefore.Add(levelBefore);
                    tally.BestBefore.Add(bestBefore);
                    tally.LevelAfter.Add(hero.Level);
                    tally.BestAfter.Add(HeroAdapter.ReadSkills(hero).MaxCombatSkill);
                }
                catch (System.Exception error)
                {
                    // A hero the game cannot rebuild keeps the sheet it was
                    // generated with; the rest of the map still starts on the curve.
                    tally.Failed++;
                    if (tally.Failed <= 3)
                    {
                        ModLog.Error("STARTCURVE failed for " + (hero != null ? hero.Name : null)
                                     + ": " + error.GetType().Name + " " + error.Message);
                    }
                }
            }

            ModLog.Info("STARTCURVE heroes=" + tally.Heroes
                        + " skillsRaised=" + tally.Raised
                        + " skillsLowered=" + tally.Lowered
                        + " perksDropped=" + tally.PerksDropped
                        + " failed=" + tally.Failed);
            ModLog.Info("STARTCURVE level before " + Diagnostics.Percentiles(tally.LevelBefore));
            ModLog.Info("STARTCURVE level after  " + Diagnostics.Percentiles(tally.LevelAfter));
            ModLog.Info("STARTCURVE bestWeapon before " + Diagnostics.Percentiles(tally.BestBefore));
            ModLog.Info("STARTCURVE bestWeapon after  " + Diagnostics.Percentiles(tally.BestAfter));
        }

        private static void ApplyTo(Hero hero, Tally tally)
        {
            HeroDeveloper developer = hero.HeroDeveloper;

            // Where this mod will be taking him, computed exactly as the weekly
            // growth computes it, so the campaign's first week carries on from here.
            Dictionary<SkillObject, int> developed = new Dictionary<SkillObject, int>();
            List<SkillGrowthService.SkillTarget> targets = SkillGrowthService.Targets(hero);
            for (int i = 0; i < targets.Count; i++) developed[targets[i].Skill] = targets[i].Target;

            float age = hero.Age;
            int combatCap = StartingCurve.UnusedCombatCap(SkillGrowth.PrimaryTarget(age, HeroTalent.For(hero)));
            int civilCap = StartingCurve.UnusedCivilCap(age, HeroTalent.For(hero, Talent.Civil));
            int navalCap = StartingCurve.UnusedCivilCap(age, HeroTalent.For(hero, Talent.Naval));

            foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
            {
                if (skill == null) continue;

                int target;
                bool isDeveloped = developed.TryGetValue(skill, out target);

                int cap = SkillGrowthService.IsWeaponSkill(skill) ? combatCap
                        : SkillGrowthService.IsNavalSkill(skill) ? navalCap
                        : civilCap;

                int current = hero.GetSkillValue(skill);
                int value = StartingCurve.Settle(current, target, cap, isDeveloped);
                if (value == current) continue;

                developer.SetInitialSkillLevel(skill, value);
                if (value > current) tally.Raised++;
                else tally.Lowered++;
            }

            tally.PerksDropped += DropUnearnedPerks(hero);

            // Level to zero first: CheckLevel only ever climbs, and the routine
            // starts from wherever the level already stands.
            hero.Level = 0;
            developer.InitializeHeroDeveloper();

            // A lord who now stands lower already holds more focus and attributes
            // than his level earns, and the routine leaves that as a negative
            // balance. Zero instead: nothing he holds is taken, and a companion's
            // sheet does not show a debt.
            if (developer.UnspentFocusPoints < 0) developer.UnspentFocusPoints = 0;
            if (developer.UnspentAttributePoints < 0) developer.UnspentAttributePoints = 0;
        }

        private static int DropUnearnedPerks(Hero hero)
        {
            List<PerkObject> kept = new List<PerkObject>();
            int dropped = 0;

            foreach (PerkObject perk in PerkObject.All)
            {
                if (perk == null || !hero.GetPerkValue(perk)) continue;

                if (perk.Skill != null && hero.GetSkillValue(perk.Skill) < perk.RequiredSkillValue) dropped++;
                else kept.Add(perk);
            }

            if (dropped == 0) return 0;

            hero.ClearPerks();
            for (int i = 0; i < kept.Count; i++) hero.HeroDeveloper.AddPerk(kept[i]);
            return dropped;
        }
    }
}
