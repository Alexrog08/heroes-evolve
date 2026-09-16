namespace HeroesEvolve.Core
{
    /// <summary>
    /// How sound one piece of gear is, as its modifier says.
    ///
    /// The game's ItemQuality, numbered exactly as the game numbers it -- read
    /// from TaleWorlds.Core's own metadata: Poor 0, Inferior 1, Common 2, Fine 3,
    /// Masterwork 4, Legendary 5 -- so a cast carries a modifier's quality
    /// across without a table to fall out of step.
    ///
    /// A piece with no modifier is Common, and so is a modifier whose XML names
    /// no quality at all: ItemModifier.ReadItemQuality falls back to Common. That
    /// is why the horse temperaments -- Strong, Lean, Stubborn, none of which
    /// declares a quality -- never read as damage.
    /// </summary>
    public static class ItemGrade
    {
        public const int Poor = 0;
        public const int Inferior = 1;
        public const int Common = 2;
        public const int Fine = 3;
        public const int Masterwork = 4;
        public const int Legendary = 5;

        /// <summary>How many grades there are, for tallies indexed by grade.</summary>
        public const int Count = 6;

        private static readonly string[] Names = { "poor", "inferior", "common", "fine", "masterwork", "legendary" };

        /// <summary>
        /// Rusty, bent, worn, cracked: a piece made worse than the item it is.
        ///
        /// Never bought, and never taken off a prisoner as an upgrade. The market
        /// rules measure an item by the tier of the item, not of the piece, so a
        /// rusty sword and a sound one tied on every term but price -- and the
        /// cheaper won. A lord in a town that stocked both walked out with the
        /// rusty one: fifteen points less damage for a third of the money.
        /// </summary>
        public static bool IsDamaged(int grade)
        {
            return grade < Common;
        }

        /// <summary>A grade as an index into a tally, whatever a mod's modifier reports.</summary>
        public static int Index(int grade)
        {
            if (grade < Poor) return Poor;
            if (grade > Legendary) return Legendary;
            return grade;
        }

        /// <summary>A tally by grade the way the census prints it, every grade named.</summary>
        public static string Describe(int[] counts)
        {
            if (counts == null || counts.Length == 0) return "none";

            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int grade = 0; grade < Count; grade++)
            {
                if (grade > 0) text.Append(' ');
                text.Append(Names[grade]).Append('=').Append(grade < counts.Length ? counts[grade] : 0);
            }
            return text.ToString();
        }
    }
}
