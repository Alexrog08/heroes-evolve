namespace HeroesEvolve.Core
{
    /// <summary>
    /// What the robberies of this session were, for the census.
    ///
    /// offences against vengeance is the line to watch: every robbery of
    /// character leaves one oath, and an oath is answered at most once, so
    /// vengeance can approach offences over the years and never pass them.
    /// justice is the robberies only a prisoner's name explains, and bandits
    /// the ones nobody answers for.
    ///
    /// Counted since the campaign was loaded and written to no save. A reload
    /// starts the count again, which costs a running total and nothing else.
    /// </summary>
    public static class RobberyTally
    {
        /// <summary>Robberies that were the captor's own doing.</summary>
        public static int Offences;

        /// <summary>Oaths of vengeance sworn for them: one each, less the robberies inside one house.</summary>
        public static int Oaths;

        /// <summary>Robberies by a house that was owed, each striking one oath off.</summary>
        public static int Vengeance;

        /// <summary>Robberies only the prisoner's name explains: an honourable captor, a known thief.</summary>
        public static int Justice;

        /// <summary>Robberies by leaderless bands, who are sworn against by nobody.</summary>
        public static int Bandits;

        public static void Reset()
        {
            Offences = 0;
            Oaths = 0;
            Vengeance = 0;
            Justice = 0;
            Bandits = 0;
        }

        public static void Offence()
        {
            Offences++;
        }

        public static void Oath()
        {
            Oaths++;
        }

        public static void Avenged()
        {
            Vengeance++;
        }

        public static void Justified()
        {
            Justice++;
        }

        public static void Bandit()
        {
            Bandits++;
        }

        public static string Describe()
        {
            return "offences=" + Offences
                   + " oaths=" + Oaths
                   + " vengeance=" + Vengeance
                   + " justice=" + Justice
                   + " bandits=" + Bandits;
        }
    }
}
