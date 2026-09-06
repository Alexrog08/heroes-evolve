namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// The battlefield role a hero is meant to fill.
    ///
    /// This is not inferred from skills -- it comes from the game's own label
    /// for the hero (BasicCharacterObject.DefaultFormationClass, authored as
    /// default_group in lords.xml). Inferring it was measurably wrong: in a live
    /// campaign the planner wanted to hand a spear and shield to a Battanian
    /// archer and to a Khuzait horse archer, and a bow to two cavalry lords,
    /// because a hero whose best skill is Polearm still loses the archetype test
    /// (ArcherSidearm excludes Polearm, so a moderate Bow beats a strong
    /// Polearm). The label does not have that failure mode.
    ///
    /// Pure: the mapping from the game's FormationClass lives in the game layer.
    /// </summary>
    public enum BattleRole
    {
        /// <summary>No usable label; fall back to deciding from skills.</summary>
        Unset = 0,

        Infantry,
        Ranged,
        Cavalry,
        HorseArcher
    }

    public static class BattleRoleRules
    {
        /// <summary>Roles that belong on a horse.</summary>
        public static bool IsMounted(BattleRole role)
        {
            return role == BattleRole.Cavalry || role == BattleRole.HorseArcher;
        }

        /// <summary>Roles whose primary weapon is a ranged one.</summary>
        public static bool IsRanged(BattleRole role)
        {
            return role == BattleRole.Ranged || role == BattleRole.HorseArcher;
        }
    }
}
