using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// Nudges a lord's combat skills toward what his years and his aptitude say
    /// they should be, in the skills his own equipment says he uses.
    ///
    /// Runs weekly. The peak takes forty years to arrive, so running seven times
    /// as often would multiply the work without changing anything anyone could
    /// notice.
    ///
    /// Nothing is ever reduced and growth stops on arrival, so a lord the game
    /// developed properly is never touched at all.
    /// </summary>
    public static class SkillGrowthService
    {
        /// <summary>
        /// The rank the movement skill is held to -- Riding for a mounted lord,
        /// Athletics for one on foot. Second place rather than first: the
        /// founder of the lab save finished with Athletics 274 against Bow 288,
        /// and the current heir with Riding 236 against One Handed 167, so a
        /// lord's legs or his horse are worth about as much as his weapon.
        /// </summary>
        internal const int MovementRank = 1;

        /// <summary>
        /// Weekly ticks in one campaign year, asked of the game rather than
        /// assumed. CampaignTime derives its calendar from static fields any mod
        /// may change -- FastMode cuts a season to one week, so a year is four
        /// ticks there against twelve in stock -- and a growth rate written
        /// against one calendar runs at the wrong speed on the other.
        /// </summary>
        public static float CyclesPerYear()
        {
            float daysInWeek = CampaignTime.DaysInWeek;
            if (daysInWeek <= 0f) return SkillGrowth.DefaultCyclesPerYear;

            float cycles = CampaignTime.DaysInYear / daysInWeek;
            return cycles > 0f ? cycles : SkillGrowth.DefaultCyclesPerYear;
        }

        /// <summary>
        /// Grows one hero, and says whether he was one this mod grows at all.
        ///
        /// The answer is reported rather than discarded because the weekly log
        /// line used to count every living hero in the game -- wanderers,
        /// notables and templates included -- and call them all grown.
        /// </summary>
        public static bool GrowWeekly(Hero hero)
        {
            if (!HeroFilter.IsEligibleToGrow(hero)) return false;
            if (hero.HeroDeveloper == null || hero.BattleEquipment == null) return false;

            List<SkillTarget> targets = Targets(hero, _weeklyOrder);
            if (targets.Count == 0) return false;

            for (int i = 0; i < targets.Count; i++)
            {
                Grant(hero, targets[i].Skill, targets[i].Target, targets[i].Talent);
            }
            return true;
        }

        /// <summary>One skill this mod develops in a hero: where it is headed, and the talent driving it.</summary>
        internal struct SkillTarget
        {
            public SkillObject Skill;
            public int Target;
            public float Talent;
        }

        /// <summary>
        /// Every skill this mod develops in this hero, with where it is headed.
        /// Empty when his years and talent give him no target at all.
        ///
        /// Shared by the weekly growth and the campaign-start curve, so the two
        /// can never aim the same hero at different places.
        ///
        /// Weapons first, the one he has put the most focus into leading and
        /// slot order wherever focus does not decide -- WeaponRank says why
        /// focus, and why a tie is not grown alike. Never by current value: that
        /// would entrench whatever the generator happened to give him and never
        /// let a repaired hero grow into the loadout he was actually handed,
        /// which is why a tie falls back to the slots and not to his figures.
        ///
        /// Then everything that is not a weapon -- stewardship, medicine,
        /// seamanship and the rest -- by where the game has already spent this
        /// lord's focus. Focus is the selector because it is the game's own
        /// statement of what a lord is for, and the campaign shows the statement
        /// is real: all 507 lords hold between 26 and 40 points and spread them
        /// differently, 81% into Scouting, 28% into Smithing, 6% into Shipmaster.
        /// A skill with no focus gets no target and stays where it is, which is
        /// how 72% of lords remain quite properly unable to forge anything. Civil
        /// and naval aptitude are drawn separately from combat, so a lord may be a
        /// prodigy with a lance and an indifferent quartermaster.
        /// </summary>
        internal static List<SkillTarget> Targets(Hero hero, WeaponRank.Tally weaponOrder)
        {
            List<SkillTarget> targets = new List<SkillTarget>();

            float talent = HeroTalent.For(hero);
            int primaryTarget = SkillGrowth.PrimaryTarget(hero.Age, talent);
            if (primaryTarget <= 0) return targets;

            List<SkillObject> ranked = RankedWeaponSkills(hero, weaponOrder);
            for (int rank = 0; rank < ranked.Count; rank++)
            {
                Add(targets, hero, ranked[rank], SkillGrowth.TargetForRank(primaryTarget, rank), talent);
            }

            bool mounted = hero.BattleEquipment[EquipmentIndex.Horse].Item != null;
            SkillObject movement = mounted ? DefaultSkills.Riding : DefaultSkills.Athletics;
            Add(targets, hero, movement, SkillGrowth.TargetForRank(primaryTarget, MovementRank), talent);

            // Two values for the whole loop, not one per skill: asking inside it
            // once cost a string and a hash per skill per hero per week, some
            // twelve thousand times a tick, to arrive at the same two answers.
            float civil = HeroTalent.For(hero, Talent.Civil);
            float naval = HeroTalent.For(hero, Talent.Naval);
            float age = hero.Age;

            foreach (SkillObject skill in TaleWorlds.CampaignSystem.Extensions.Skills.All)
            {
                if (skill == null) continue;
                if (IsWeaponSkill(skill)) continue;

                int focus = hero.HeroDeveloper.GetFocus(skill);
                if (focus <= 0) continue;

                bool atSea = IsNavalSkill(skill);
                float aptitude = atSea ? naval : civil;
                int norm = atSea ? SkillGrowth.NavalPeakNorm : SkillGrowth.CivilPeakNorm;
                Add(targets, hero, skill, FocusGrowth.TargetFor(age, aptitude, focus, norm), aptitude);
            }

            return targets;
        }

        /// <summary>As above, for a caller keeping no census.</summary>
        internal static List<SkillTarget> Targets(Hero hero)
        {
            return Targets(hero, null);
        }

        /// <summary>
        /// Adds one skill's target, never below where his own written profile
        /// puts that line at his age -- see AuthoredTalent.WrittenAt. The rules
        /// above give a hero the shape of the lords a campaign produces; his
        /// sheet gives him the shape TaleWorlds drew for him, and he takes
        /// whichever is kinder to each line.
        /// </summary>
        private static void Add(List<SkillTarget> targets, Hero hero, SkillObject skill, int target, float talent)
        {
            if (skill == null) return;

            int written = AuthoredTalent.WrittenAt(HeroTalent.WrittenIn(hero, skill), hero.Age);
            if (written > target) target = written;

            if (target <= 0) return;

            SkillTarget entry = new SkillTarget();
            entry.Skill = skill;
            entry.Target = target;
            entry.Talent = talent;
            targets.Add(entry);
        }

        /// <summary>
        /// The eight skills already handled by the weapon and movement passes.
        /// Growing them a second time here would double their rate and ignore
        /// what the hero actually carries.
        /// </summary>
        internal static bool IsWeaponSkill(SkillObject skill)
        {
            return skill == DefaultSkills.OneHanded
                   || skill == DefaultSkills.TwoHanded
                   || skill == DefaultSkills.Polearm
                   || skill == DefaultSkills.Bow
                   || skill == DefaultSkills.Crossbow
                   || skill == DefaultSkills.Throwing
                   || skill == DefaultSkills.Riding
                   || skill == DefaultSkills.Athletics;
        }

        /// <summary>
        /// The War Sails skills, matched by string id so the mod still builds
        /// and runs for anyone without that DLC installed -- referencing
        /// DefaultSkills members that may not exist would not.
        /// </summary>
        internal static bool IsNavalSkill(SkillObject skill)
        {
            string id = skill.StringId;
            if (id == null) return false;

            return id == "Mariner" || id == "Boatswain" || id == "Shipmaster";
        }

        /// <summary>
        /// Skill points this mod has actually delivered since the counter was
        /// last read, and what it asked for.
        ///
        /// The two differ, and finding out by how much is the point. SkillGrowth
        /// clamps the annual gain to MaximumPointsPerYear and hands the result
        /// to Hero.AddSkillXp -- which multiplies it by the generic XP
        /// multiplier and again by the hero's focus factor before any of it
        /// becomes progress. So the documented ceiling of roughly eight points a
        /// year for a talented lord is a ceiling on the request, not on the
        /// delivery, and a live campaign measured weapon skills climbing about
        /// twice that. These two numbers turn that from an argument into a
        /// ratio.
        /// </summary>
        private static int _pointsDelivered;
        private static float _pointsAsked;

        /// <summary>Reads the pair and clears it for the next pass.</summary>
        public static void TakeDelivered(out int delivered, out int asked)
        {
            delivered = _pointsDelivered;
            asked = (int)(_pointsAsked + 0.5f);
            _pointsDelivered = 0;
            _pointsAsked = 0f;
        }

        /// <summary>
        /// What focus did to the order of the weapons of the heroes grown since
        /// the tally was last read. The weekly GROWTH line carries it.
        /// </summary>
        private static WeaponRank.Tally _weeklyOrder = new WeaponRank.Tally();

        /// <summary>Reads the tally and starts a new one for the next pass.</summary>
        public static WeaponRank.Tally TakeWeaponOrder()
        {
            WeaponRank.Tally taken = _weeklyOrder;
            _weeklyOrder = new WeaponRank.Tally();
            return taken;
        }

        private static void Grant(Hero hero, SkillObject skill, int target, float talent)
        {
            if (skill == null || target <= 0) return;

            int current = hero.GetSkillValue(skill);
            float points = SkillGrowth.PointsStep(current, target, talent, CyclesPerYear());
            if (points <= 0f) return;

            int xp = (int)(points * XpPerPointAt(current));
            if (xp <= 0) return;

            _pointsAsked += points;

            // Hero.AddSkillXp routes through HeroDeveloper with the focus factor
            // applied, so this composes with the game's own progression rather
            // than bypassing it: a hero the AI has invested focus in learns
            // faster from the same grant. And because the AI allocates focus to
            // whichever skill most exceeds its learning limit, pushing a skill
            // here makes the game itself follow.
            hero.AddSkillXp(skill, xp);
            _pointsDelivered += hero.GetSkillValue(skill) - current;
        }

        /// <summary>
        /// What one more point of this skill costs at its current level, asked
        /// of the game rather than guessed.
        ///
        /// The first version of this used a flat twelve experience per point and
        /// eight weekly passes over a live campaign moved nothing: the real
        /// curve charges hundreds to thousands per point at the levels lords sit
        /// at, so the grants were two orders of magnitude too small to register.
        /// </summary>
        private static float XpPerPointAt(int level)
        {
            if (level < 0) level = 0;

            CharacterDevelopmentModel model = DevelopmentModel();
            if (model == null) return 1f;

            float here = model.GetXpRequiredForSkillLevel(level);
            float next = model.GetXpRequiredForSkillLevel(level + 1);

            float cost = next - here;
            return cost > 1f ? cost : 1f;
        }

        /// <summary>
        /// The game's development model, held rather than walked to.
        ///
        /// Grant asks for the experience curve once per skill per hero, which
        /// over a weekly pass is tens of thousands of walks down
        /// Campaign.Current.Models.CharacterDevelopmentModel for an object that
        /// does not change. Cached against the campaign it came from, so loading
        /// a different save picks up that save's model rather than the last
        /// one's.
        /// </summary>
        private static Campaign _modelOwner;
        private static CharacterDevelopmentModel _model;

        private static CharacterDevelopmentModel DevelopmentModel()
        {
            Campaign campaign = Campaign.Current;
            if (campaign == null) return null;

            if (!ReferenceEquals(campaign, _modelOwner))
            {
                _modelOwner = campaign;
                _model = campaign.Models != null ? campaign.Models.CharacterDevelopmentModel : null;
            }

            return _model;
        }

        /// <summary>
        /// The distinct skills this hero's weapons call on, in the order they
        /// take their shares of his growth: most focus first, slot order where
        /// focus does not decide. See WeaponRank.
        /// </summary>
        internal static List<SkillObject> RankedWeaponSkills(Hero hero)
        {
            return RankedWeaponSkills(hero, null);
        }

        private static List<SkillObject> RankedWeaponSkills(Hero hero, WeaponRank.Tally tally)
        {
            List<SkillObject> bySlot = WeaponSkillsBySlot(hero);
            List<int> focus = FocusIn(hero, bySlot);
            if (tally != null) tally.Add(focus);

            int[] order = WeaponRank.ByFocus(focus);
            List<SkillObject> ranked = new List<SkillObject>(order.Length);
            for (int i = 0; i < order.Length; i++) ranked.Add(bySlot[order[i]]);
            return ranked;
        }

        /// <summary>
        /// The focus he holds in each of these skills, in the same order. None
        /// for a hero without a developer, which leaves him ranked by slot.
        /// </summary>
        private static List<int> FocusIn(Hero hero, List<SkillObject> skills)
        {
            List<int> focus = new List<int>(skills.Count);
            HeroDeveloper developer = hero.HeroDeveloper;
            for (int i = 0; i < skills.Count; i++)
            {
                focus.Add(developer != null ? developer.GetFocus(skills[i]) : 0);
            }
            return focus;
        }

        /// <summary>
        /// One hero's weapons as growth ranks them, for hev.dry_run: each with
        /// the focus he holds in it and where it is headed, then the slot order
        /// focus replaced. Moving focus on a companion shows up here at once,
        /// where growth itself would take years to show it.
        /// </summary>
        internal static string DescribeWeaponOrder(Hero hero)
        {
            if (hero == null || hero.HeroDeveloper == null || hero.BattleEquipment == null) return "n/a";

            Dictionary<SkillObject, int> targetOf = new Dictionary<SkillObject, int>();
            List<SkillTarget> targets = Targets(hero);
            for (int i = 0; i < targets.Count; i++) targetOf[targets[i].Skill] = targets[i].Target;

            StringBuilder text = new StringBuilder("grows=" + HeroFilter.IsEligibleToGrow(hero) + " weapons=");
            List<SkillObject> ranked = RankedWeaponSkills(hero);
            for (int i = 0; i < ranked.Count; i++)
            {
                int target;
                targetOf.TryGetValue(ranked[i], out target);
                if (i > 0) text.Append(' ');
                text.Append(ranked[i].StringId)
                    .Append("[focus ").Append(hero.HeroDeveloper.GetFocus(ranked[i]))
                    .Append(" -> ").Append(target).Append(']');
            }

            List<SkillObject> bySlot = WeaponSkillsBySlot(hero);
            text.Append(" bySlot=");
            for (int i = 0; i < bySlot.Count; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(bySlot[i].StringId);
            }

            return text.ToString();
        }

        /// <summary>
        /// The distinct skills this hero's weapons call on, in slot order.
        /// Shields and ammunition contribute none and are skipped without
        /// consuming a rank -- a lord carrying bow, arrows, sword and shield
        /// trains two skills, not four.
        /// </summary>
        private static List<SkillObject> WeaponSkillsBySlot(Hero hero)
        {
            List<SkillObject> bySlot = new List<SkillObject>();

            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                ItemObject item = hero.BattleEquipment[SlotMapping.WeaponSlot(i)].Item;
                if (item == null) continue;

                // The placeholder the game hands a hero who comes of age is the
                // bug's marker, not his weapon: training One Handed for it would
                // teach him the sword the repair is about to take away.
                if (HeroAdapter.IsVanillaDummySword(item)) continue;

                WeaponCategory category = ItemClassifier.Classify(item);
                SkillObject skill = SkillFor(category);
                if (skill == null) continue;
                if (bySlot.Contains(skill)) continue;

                bySlot.Add(skill);
            }

            return bySlot;
        }

        /// <summary>
        /// The skill a weapon category trains, or null for the categories that
        /// train nothing: shields, both kinds of ammunition, and anything the
        /// classifier could not place.
        /// </summary>
        private static SkillObject SkillFor(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.OneHandedSword:
                case WeaponCategory.OneHandedAxe:
                case WeaponCategory.OneHandedMace:
                    return DefaultSkills.OneHanded;

                case WeaponCategory.TwoHandedSword:
                case WeaponCategory.TwoHandedAxe:
                case WeaponCategory.TwoHandedMace:
                    return DefaultSkills.TwoHanded;

                case WeaponCategory.Spear:
                case WeaponCategory.Polearm:
                    return DefaultSkills.Polearm;

                case WeaponCategory.Bow: return DefaultSkills.Bow;
                case WeaponCategory.Crossbow: return DefaultSkills.Crossbow;
                case WeaponCategory.Throwing: return DefaultSkills.Throwing;

                default: return null;
            }
        }
    }
}
