namespace HeroesEvolve.Core
{
    /// <summary>
    /// What the robberies of this session cost and what they settled, for the
    /// census.
    ///
    /// The line it prints is the watch on one specific fear: that charging a
    /// robbery to three circles of houses would sink relation across the map.
    /// relationSpent is every point taken from a standing between two houses,
    /// relationSettled every point given back by a house taking vengeance,
    /// and oaths against vengeance says how many robberies have been answered
    /// so far. offences against grudges and justice says how much of the
    /// robbing is character and how much is bad blood nobody was owed for.
    ///
    /// Counted since the campaign was loaded and written to no save. A reload
    /// starts the count again, which costs a running total and nothing else.
    /// </summary>
    public static class RobberyTally
    {
        /// <summary>Robberies that were the captor's own doing, and charged.</summary>
        public static int Offences;

        /// <summary>Oaths of vengeance sworn for them: one each, less the robberies inside one house.</summary>
        public static int Oaths;

        /// <summary>Robberies by a house that was owed.</summary>
        public static int Vengeance;

        /// <summary>Robberies only bad blood explains, with no debt behind it.</summary>
        public static int Grudges;

        /// <summary>Robberies only the prisoner's name explains: an honourable captor, a known thief.</summary>
        public static int Justice;

        /// <summary>Robberies by leaderless bands, who pay nothing and are sworn against by nobody.</summary>
        public static int Bandits;

        /// <summary>Relation taken, summed over every house an offence touched.</summary>
        public static int RelationSpent;

        /// <summary>Standings moved by those offences: one per house and offence.</summary>
        public static int HousesTouched;

        /// <summary>Relation given back by vengeance taken.</summary>
        public static int RelationSettled;

        public static void Reset()
        {
            Offences = 0;
            Oaths = 0;
            Vengeance = 0;
            Grudges = 0;
            Justice = 0;
            Bandits = 0;
            RelationSpent = 0;
            HousesTouched = 0;
            RelationSettled = 0;
        }

        /// <summary>One robbery of character, with what it cost in all and across how many houses.</summary>
        public static void Offence(int spent, int houses)
        {
            Offences++;
            RelationSpent += spent < 0 ? -spent : spent;
            HousesTouched += houses < 0 ? 0 : houses;
        }

        /// <summary>One oath of vengeance sworn.</summary>
        public static void Oath()
        {
            Oaths++;
        }

        /// <summary>One robbery in vengeance, with what it gave back.</summary>
        public static void Avenged(int settled)
        {
            Vengeance++;
            if (settled > 0) RelationSettled += settled;
        }

        /// <summary>One robbery nobody was owed for: bad blood, or the prisoner's name.</summary>
        public static void Unprovoked(bool justice)
        {
            if (justice) Justice++;
            else Grudges++;
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
                   + " grudges=" + Grudges
                   + " justice=" + Justice
                   + " bandits=" + Bandits
                   + " relationSpent=" + RelationSpent
                   + " housesTouched=" + HousesTouched
                   + " relationSettled=" + RelationSettled;
        }
    }
}
