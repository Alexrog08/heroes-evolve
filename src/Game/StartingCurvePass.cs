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
    ///      to drop a single perk, so the skills that lost one are cleared with
    ///      the game's own PerkHelper.ClearPerksForSkill and the perks he still
    ///      earns there are given back; the game's daily selection fills in the
    ///      rest as he grows. Giving a perk back re-runs its opening. Almost
    ///      everything that does is undone by the clearing first, but
    ///      AgingCampaignBehavior grants an extra life each time Cheat Death or
    ///      Health Advise opens and never takes one away, so a skill that would
    ///      have to give either back is left whole, unearned perks and all:
    ///      a lord keeping a Medicine perk he no longer quite earns costs far
    ///      less than him, or his whole clan, living an extra life.
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
            public int PerksRestored;
            public int SkillsLeftWhole;
            public int Unarmed;
            public int OwnClan;
            public int Failed;
            public readonly List<int> LevelBefore = new List<int>();
            public readonly List<int> LevelAfter = new List<int>();
            public readonly List<int> BestBefore = new List<int>();
            public readonly List<int> BestAfter = new List<int>();
        }

        /// <summary>
        /// Who the curve may touch. The growth filter, plus the player's own
        /// clan when he has asked to keep it.
        ///
        /// Growth can share a filter with almost everything because it only
        /// ever raises -- HeroFilter.IsEligibleToGrow says so in as many
        /// words, and that is why it ignores ManageOwnClan. The curve is the
        /// one pass in the mod that takes something away: it settles a hero on
        /// the line his age and talent put him on, which for a well-written
        /// sheet means cutting it.
        ///
        /// So the switch that means "my clan is mine" has to hold here, as it
        /// already does for the repair and the market. It is not an exception
        /// for the player -- it is the same question of who decides that those
        /// two ask -- and the case is real rather than theoretical: the
        /// brother or sister a player starts beside is an adult, armed and in
        /// his clan from the first minute, so without this the curve trims him
        /// before the campaign has drawn its first frame.
        /// </summary>
        private static bool Eligible(Hero hero)
        {
            if (hero == null) return false;
            if (!HeroFilter.IsEligibleToGrow(hero)) return false;
            if (!Settings.ManageOwnClan && hero.Clan == Clan.PlayerClan) return false;
            return true;
        }

        public static void Apply()
        {
            Tally tally = new Tally();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    // Counted before the filter, so the census can say why
                    // a clan the player keeps is missing from the figures
                    // rather than leaving him to wonder.
                    if (!Settings.ManageOwnClan && hero.Clan == Clan.PlayerClan
                        && HeroFilter.IsEligibleToGrow(hero))
                    {
                        tally.OwnClan++;
                        continue;
                    }

                    if (!Eligible(hero)) continue;
                    if (hero.HeroDeveloper == null || hero.BattleEquipment == null) continue;

                    // Without a real weapon the curve cannot tell what he fights
                    // with, and would cap every weapon skill he has at half his
                    // target -- his real one included. He keeps the sheet he was
                    // generated with; the repair arms him and growth takes over.
                    if (SkillGrowthService.RankedWeaponSkills(hero).Count == 0)
                    {
                        tally.Unarmed++;
                        continue;
                    }

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
                        + " perksRestored=" + tally.PerksRestored
                        + " skillsLeftWhole=" + tally.SkillsLeftWhole
                        + " skippedUnarmed=" + tally.Unarmed
                        + " skippedOwnClan=" + tally.OwnClan
                        + " failed=" + tally.Failed);
            ModLog.Info("STARTCURVE level before " + Diagnostics.Percentiles(tally.LevelBefore));
            ModLog.Info("STARTCURVE level after  " + Diagnostics.Percentiles(tally.LevelAfter));
            ModLog.Info("STARTCURVE bestWeapon before " + Diagnostics.Percentiles(tally.BestBefore));
            ModLog.Info("STARTCURVE bestWeapon after  " + Diagnostics.Percentiles(tally.BestAfter));
        }

        /// <summary>
        /// The same curve, for one hero who arrived after the campaign began.
        ///
        /// Apply above runs once, at world creation, and nothing has put a
        /// latecomer on the curve since: a hero created in year ten keeps
        /// whatever he was made with for the rest of the campaign, because
        /// growth only ever raises and never lowers.
        ///
        /// Who that reaches is narrower than it sounds, and the game's own
        /// arithmetic is why. A child of two lords does not inherit their
        /// figures: DefaultHeroCreationModel.GetInheritedSkillsForHero keeps
        /// his best 28% of skills and then rescales the set so it totals 112 a
        /// skill, whatever the parents grew to. Six skills averaging 112 is
        /// already inside the band -- TaleWorlds writes his own 18-to-24 lords
        /// at a median of 110 and this mod's curve leaves them at 123 -- so
        /// children need nothing.
        ///
        /// It is the parentless ones that arrive wrong. The same method falls
        /// back to GetDefaultCharacterSkills for a hero with no father and no
        /// mother, and a template carries the figures of a grown man. Every
        /// wanderer a tavern spawns and every lord generated to fill a clan
        /// comes of age holding them.
        ///
        /// Once, at creation, and never again -- which is what makes it safe.
        /// Running the curve repeatedly would do one of two harmful things: it
        /// settles a developed skill exactly ON its target, so a daily pass
        /// would teleport every hero who is merely behind straight to where
        /// growth was walking him; and lowering on a schedule would strip a
        /// lord who changed weapons, whose old primary becomes an unused skill
        /// with an unused skill's cap. Neither happens to a man it touches once
        /// on the day he is made.
        /// </summary>
        public static bool ApplyToLatecomer(Hero hero)
        {
            if (!Eligible(hero)) return false;

            Tally tally = new Tally();
            ApplyTo(hero, tally);

            ModLog.Info("STARTCURVE latecomer=" + hero.Name
                        + " age=" + (int)hero.Age
                        + " raised=" + tally.Raised
                        + " lowered=" + tally.Lowered);
            return true;
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
            int civilCap = StartingCurve.UnusedCivilCap(age, HeroTalent.For(hero, Talent.Civil),
                                                        SkillGrowth.CivilPeakNorm);
            int navalCap = StartingCurve.UnusedCivilCap(age, HeroTalent.For(hero, Talent.Naval),
                                                        SkillGrowth.NavalPeakNorm);

            foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
            {
                if (skill == null) continue;

                int target;
                bool isDeveloped = developed.TryGetValue(skill, out target);

                int cap = SkillGrowthService.IsWeaponSkill(skill) ? combatCap
                        : SkillGrowthService.IsNavalSkill(skill) ? navalCap
                        : civilCap;

                // A skill this mod does not develop is still one TaleWorlds may
                // have written, and the cap is about the generator's figures
                // rather than his. So the cap never falls below his own written
                // profile at his age: the bow he does not carry keeps its share
                // of what he was written with, and only what the generator
                // invented on top of that comes off.
                int written = AuthoredTalent.WrittenAt(HeroTalent.WrittenIn(hero, skill), age);
                if (written > cap) cap = written;

                int current = hero.GetSkillValue(skill);
                int value = StartingCurve.Settle(current, target, cap, isDeveloped);
                if (value == current) continue;

                developer.SetInitialSkillLevel(skill, value);
                if (value > current) tally.Raised++;
                else tally.Lowered++;
            }

            DropUnearnedPerks(hero, tally);

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

        private static void DropUnearnedPerks(Hero hero, Tally tally)
        {
            HashSet<SkillObject> losing = new HashSet<SkillObject>();
            HashSet<SkillObject> keepsALife = new HashSet<SkillObject>();

            foreach (PerkObject perk in PerkObject.All)
            {
                if (perk == null || perk.Skill == null || !hero.GetPerkValue(perk)) continue;

                if (hero.GetSkillValue(perk.Skill) < perk.RequiredSkillValue) losing.Add(perk.Skill);
                else if (GrantsALifeOnOpening(perk)) keepsALife.Add(perk.Skill);
            }

            foreach (SkillObject skill in keepsALife)
            {
                if (losing.Remove(skill)) tally.SkillsLeftWhole++;
            }
            if (losing.Count == 0) return;

            List<PerkObject> kept = new List<PerkObject>();
            foreach (PerkObject perk in PerkObject.All)
            {
                if (perk == null || perk.Skill == null || !losing.Contains(perk.Skill)) continue;
                if (!hero.GetPerkValue(perk)) continue;

                if (hero.GetSkillValue(perk.Skill) < perk.RequiredSkillValue) tally.PerksDropped++;
                else kept.Add(perk);
            }

            foreach (SkillObject skill in losing)
            {
                Helpers.PerkHelper.ClearPerksForSkill(hero, skill);
            }
            for (int i = 0; i < kept.Count; i++)
            {
                hero.HeroDeveloper.AddPerk(kept[i]);
            }
            tally.PerksRestored += kept.Count;
        }

        /// <summary>
        /// The perks whose opening AgingCampaignBehavior.OnPerkOpened answers with
        /// an extra life, which no reset ever takes back.
        /// </summary>
        private static bool GrantsALifeOnOpening(PerkObject perk)
        {
            return perk == DefaultPerks.Medicine.CheatDeath || perk == DefaultPerks.Medicine.HealthAdvise;
        }
    }
}
