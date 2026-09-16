namespace HeroesEvolve.Core
{
    /// <summary>
    /// The talent a character TaleWorlds wrote was meant to have, read from the
    /// skills his template gives him.
    ///
    /// A template is not a curve. TaleWorlds wrote a king's skills once and in
    /// the base game they never move, so a figure there is not where he stands
    /// at his age but how formidable the character was meant to be: intent.
    /// Read that way it maps onto the one quantity this mod already uses for how
    /// far a hero can go, and the curve does the rest. He grows toward what that
    /// talent and his own focus allow, rather than being pinned to the number
    /// he was written with.
    ///
    /// The scale is the curve's own. A template best of 150, the peak norm, is
    /// an ordinary talent; Caladog's 300 is a prodigy's. A domain the template
    /// leaves empty carries no intent at all, and the hash decides it instead.
    /// </summary>
    public static class AuthoredTalent
    {
        /// <summary>
        /// Which talent a template skill speaks for: Talent.Combat for the six
        /// weapons, Talent.Naval for the War Sails skills, and Talent.Civil for
        /// everything else, including skills other mods add, which growth already
        /// treats as civil. Riding and Athletics return null: they follow the
        /// weapon a hero carries and say nothing on their own.
        /// </summary>
        public static string DomainOf(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return null;

            switch (skillId)
            {
                case "OneHanded":
                case "TwoHanded":
                case "Polearm":
                case "Bow":
                case "Crossbow":
                case "Throwing":
                    return Talent.Combat;

                case "Riding":
                case "Athletics":
                    return null;

                case "Mariner":
                case "Boatswain":
                case "Shipmaster":
                    return Talent.Naval;

                default:
                    return Talent.Civil;
            }
        }

        /// <summary>
        /// The talent a template's best skill in one domain implies, on the
        /// curve's own scale and inside the talent bounds. Zero when the template
        /// writes nothing there, so the caller falls back to the hash.
        /// </summary>
        public static float From(int bestAuthoredSkill)
        {
            if (bestAuthoredSkill <= 0) return 0f;

            float talent = bestAuthoredSkill / (float)SkillGrowth.PeakNorm;
            if (talent < Talent.Minimum) return Talent.Minimum;
            if (talent > Talent.Maximum) return Talent.Maximum;
            return talent;
        }
    }
}
