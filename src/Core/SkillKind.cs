namespace HeroesEvolve.Core
{
    /// <summary>
    /// The seven skills the mod reasons about. The first six are weapon skills
    /// and feed the tier ceiling; Riding is used only for mount decisions.
    /// </summary>
    public enum SkillKind
    {
        OneHanded = 0,
        TwoHanded = 1,
        Polearm = 2,
        Bow = 3,
        Crossbow = 4,
        Throwing = 5,
        Riding = 6
    }
}
