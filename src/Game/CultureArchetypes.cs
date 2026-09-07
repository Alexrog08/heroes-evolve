using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// The fighting shapes a culture actually fields, read from its own troops.
    ///
    /// A repaired lord picks his archetype from the skill the failed generation
    /// left him rather than from the label his culture would normally give him:
    /// the broken heroes carry exactly one skill and one matching weapon --
    /// Nus only Throwing, Zandina only Polearm, Rahan only Polearm -- and that
    /// single surviving number is the only thing the game ever recorded about
    /// who he was. A Battanian left holding javelins becomes a skirmisher, not
    /// the archer his culture expects.
    ///
    /// But only within what his people actually field. Battania has
    /// battanian_mounted_skirmisher and battanian_horseman, so a mounted
    /// javelineer or a Battanian lancer are both real; it has no horse archers,
    /// so that is not on offer. Vlandia's eighteen troop types contain not one
    /// bow, so a Vlandian who shoots is a crossbowman by construction. None of
    /// that is written down here -- it is read from the troop tree, which means
    /// modded cultures work without being taught.
    /// </summary>
    public static class CultureArchetypes
    {
        /// <summary>One shape a culture fields: mounted or not, and what it fights with.</summary>
        public struct Shape
        {
            public bool Mounted;
            public WeaponCategory Primary;

            public Shape(bool mounted, WeaponCategory primary)
            {
                Mounted = mounted;
                Primary = primary;
            }
        }

        /// <summary>Troops below this tier are recruits and say nothing about identity.</summary>
        private const int MinimumTier = 3;

        private static Dictionary<string, List<Shape>> _shapes;

        /// <summary>Forces a recompute; call when a campaign is loaded.</summary>
        public static void Reset()
        {
            _shapes = null;
        }

        /// <summary>
        /// The archetype this hero should be given: the one his surviving skill
        /// points at, if his culture fields it, and otherwise
        /// <paramref name="fallback"/> -- the role the game assigned him.
        ///
        /// Returns the fallback unchanged for a hero with no skills at all.
        /// There is nothing to read in him, so his culture's expectation is the
        /// only honest answer.
        /// </summary>
        public static BattleRole RoleFor(CultureObject culture, SkillProfile skills, BattleRole fallback)
        {
            if (culture == null || skills == null) return fallback;

            WeaponCategory best = DominantCategory(skills);
            if (best == WeaponCategory.None) return fallback;

            List<Shape> shapes = For(culture);
            if (shapes.Count == 0) return fallback;

            bool foot = false, mounted = false;
            for (int i = 0; i < shapes.Count; i++)
            {
                if (!CategoryRules.SameFamily(shapes[i].Primary, best)) continue;
                if (shapes[i].Mounted) mounted = true; else foot = true;
            }

            if (!foot && !mounted) return fallback;

            // Where the culture fields this weapon both ways, the hero's label
            // breaks the tie: a Battanian javelineer the game called Cavalry
            // becomes a mounted skirmisher rather than a foot one.
            bool wantsMount = mounted && (!foot || BattleRoleRules.IsMounted(fallback));

            bool ranged = best == WeaponCategory.Bow || best == WeaponCategory.Crossbow;
            if (ranged) return wantsMount ? BattleRole.HorseArcher : BattleRole.Ranged;
            return wantsMount ? BattleRole.Cavalry : BattleRole.Infantry;
        }

        /// <summary>
        /// The hero's single strongest weapon skill, or None when he has none.
        /// Throwing counts: a lord left holding javelins is a skirmisher, and
        /// several of the broken lords are exactly that.
        /// </summary>
        private static WeaponCategory DominantCategory(SkillProfile skills)
        {
            SkillKind[] order = skills.CombatSkillsDescending();
            if (order.Length == 0) return WeaponCategory.None;
            if (skills.Get(order[0]) <= 0) return WeaponCategory.None;

            switch (order[0])
            {
                case SkillKind.OneHanded: return WeaponCategory.OneHandedSword;
                case SkillKind.TwoHanded: return WeaponCategory.TwoHandedSword;
                case SkillKind.Polearm: return WeaponCategory.Spear;
                case SkillKind.Bow: return WeaponCategory.Bow;
                case SkillKind.Crossbow: return WeaponCategory.Crossbow;
                case SkillKind.Throwing: return WeaponCategory.Throwing;
                default: return WeaponCategory.None;
            }
        }

        public static List<Shape> For(CultureObject culture)
        {
            Build();

            List<Shape> shapes;
            if (culture != null && culture.StringId != null && _shapes.TryGetValue(culture.StringId, out shapes))
            {
                return shapes;
            }
            return new List<Shape>();
        }

        private static void Build()
        {
            if (_shapes != null) return;
            _shapes = new Dictionary<string, List<Shape>>();

            HashSet<string> cultures = new HashSet<string>();
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                try
                {
                    if (hero == null || hero.Culture == null || hero.Culture.StringId == null) continue;
                    if (!cultures.Add(hero.Culture.StringId)) continue;
                    _shapes[hero.Culture.StringId] = ShapesOf(hero.Culture);
                }
                catch
                {
                    // One unreadable culture must not cost the rest.
                }
            }
        }

        private static List<Shape> ShapesOf(CultureObject culture)
        {
            List<Shape> shapes = new List<Shape>();

            List<TroopSurvey.Signature> troops = TroopSurvey.Survey(culture);
            for (int i = 0; i < troops.Count; i++)
            {
                TroopSurvey.Signature troop = troops[i];
                if (troop.Tier < MinimumTier) continue;

                WeaponCategory primary = PrimaryOf(troop);
                if (primary == WeaponCategory.None) continue;

                bool known = false;
                for (int j = 0; j < shapes.Count; j++)
                {
                    if (shapes[j].Mounted == troop.Mounted && shapes[j].Primary == primary) known = true;
                }
                if (!known) shapes.Add(new Shape(troop.Mounted, primary));
            }

            return shapes;
        }

        /// <summary>
        /// What a troop is built around: its ranged weapon if it has one, else
        /// its heaviest melee. A fian is a bow even though he carries a sword;
        /// a sharpshooter is a crossbow even though he carries sword and shield.
        /// </summary>
        private static WeaponCategory PrimaryOf(TroopSurvey.Signature troop)
        {
            WeaponCategory melee = WeaponCategory.None;

            for (int i = 0; i < troop.Weapons.Count; i++)
            {
                WeaponCategory category = troop.Weapons[i];

                if (category == WeaponCategory.Bow || category == WeaponCategory.Crossbow) return category;
                if (melee == WeaponCategory.None && IsMelee(category)) melee = category;
            }

            // Throwing only counts as the identity when nothing else does: a
            // skirmisher's javelins define him, an oathsworn's do not.
            if (melee != WeaponCategory.None) return melee;

            for (int i = 0; i < troop.Weapons.Count; i++)
            {
                if (troop.Weapons[i] == WeaponCategory.Throwing) return WeaponCategory.Throwing;
            }

            return WeaponCategory.None;
        }

        private static bool IsMelee(WeaponCategory category)
        {
            return category == WeaponCategory.OneHandedSword
                   || category == WeaponCategory.OneHandedAxe
                   || category == WeaponCategory.OneHandedMace
                   || category == WeaponCategory.TwoHandedMace
                   || category == WeaponCategory.TwoHandedSword
                   || category == WeaponCategory.TwoHandedAxe
                   || category == WeaponCategory.Spear
                   || category == WeaponCategory.Polearm;
        }

        /// <summary>Writes each culture's fielded shapes to the log.</summary>
        public static void Report()
        {
            Build();

            foreach (KeyValuePair<string, List<Shape>> pair in _shapes)
            {
                StringBuilder text = new StringBuilder("SHAPES ");
                text.Append(pair.Key).Append(" n=").Append(pair.Value.Count).Append(" |");

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    text.Append(' ').Append(pair.Value[i].Mounted ? "mounted:" : "foot:")
                        .Append(pair.Value[i].Primary);
                }

                ModLog.Info(text.ToString());
            }
        }
    }
}
