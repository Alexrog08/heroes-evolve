namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// Whether a captor strips his prisoner, and how much of that is character
    /// rather than dice.
    ///
    /// The shape comes from what the game already says about a person: bandits
    /// have no honour to appeal to, a merciful and generous lord will not do it
    /// at all, a deceitful or closefisted one is likelier, roguery says he knows
    /// how, and standing between the two men matters more than any of it. Blood
    /// outranks everything short of a feud.
    ///
    /// **Every number below is invented.** Unlike the tier ceiling or the
    /// spending share, there is no population in the game to measure this
    /// against -- nothing in vanilla strips a prisoner, so there is no rate to
    /// compare with. They are a starting shape, exposed to one multiplier in
    /// settings, and the census reports the rate they actually produce so they
    /// can be calibrated the way everything else here was.
    /// </summary>
    public static class PlunderRules
    {
        /// <summary>Bandits take everything. There is nobody to appeal to.</summary>
        public const float BanditChance = 1.0f;

        /// <summary>Where an unremarkable lord starts before his character moves it.</summary>
        public const float Base = 0.20f;

        /// <summary>Each point of Honor resists; each point of dishonour invites.</summary>
        public const float PerHonor = 0.10f;

        /// <summary>Mercy and Generosity pull the same way, more gently.</summary>
        public const float PerMercy = 0.06f;
        public const float PerGenerosity = 0.08f;

        /// <summary>Roguery is knowing how, not wanting to. 200 points adds a third.</summary>
        public const float PerRogueryPoint = 0.0015f;

        /// <summary>Standing between the two men. 50 points of goodwill removes a fifth.</summary>
        public const float PerRelationPoint = 0.004f;

        /// <summary>A cousin is family, but not the kind you would not rob.</summary>
        public const float CousinFactor = 0.35f;

        /// <summary>
        /// Below this, blood has stopped counting. A father will not strip his
        /// son; a father who hates him will.
        /// </summary>
        public const int FeudRelation = -40;

        /// <summary>How close two heroes are by blood or marriage.</summary>
        public enum Kinship
        {
            None,

            /// <summary>Cousin, uncle, in-law: family, at a distance.</summary>
            Distant,

            /// <summary>Parent, child, sibling, spouse.</summary>
            Immediate
        }

        /// <summary>
        /// The chance this captor strips this prisoner, from zero to one.
        ///
        /// Traits arrive on the game's own scale, roughly -2 to +2, and are
        /// clamped here rather than trusted: a trait a mod has widened must not
        /// silently produce a certainty.
        /// </summary>
        public static float Chance(bool captorIsBandit, int honor, int mercy, int generosity,
                                   int roguery, int relation, Kinship kinship, float multiplier)
        {
            if (captorIsBandit) return Clamp(BanditChance * multiplier);

            honor = ClampTrait(honor);
            mercy = ClampTrait(mercy);
            generosity = ClampTrait(generosity);

            // The one absolute among lords: a man who is both merciful and
            // generous does not rob a prisoner, whatever his mood or his debts.
            if (mercy >= 1 && generosity >= 1) return 0f;

            // Blood, unless blood has already failed. Checked before the
            // arithmetic because it is a veto rather than a weight.
            if (kinship == Kinship.Immediate && relation > FeudRelation) return 0f;

            if (roguery < 0) roguery = 0;

            float chance = Base
                           - honor * PerHonor
                           - mercy * PerMercy
                           - generosity * PerGenerosity
                           + roguery * PerRogueryPoint
                           - relation * PerRelationPoint;

            if (kinship == Kinship.Distant) chance *= CousinFactor;

            return Clamp(chance * multiplier);
        }

        private static int ClampTrait(int value)
        {
            if (value < -2) return -2;
            if (value > 2) return 2;
            return value;
        }

        private static float Clamp(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
