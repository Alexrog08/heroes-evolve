namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// Whether a captor strips his prisoner.
    ///
    /// Two stages, because character has to outweigh the dice. A flat roll makes
    /// the same lord behave differently every time and reads as randomness; this
    /// asks first what kind of man he is, and only then who he is holding.
    ///
    ///   disposition   who he is      -- his traits, stable across the campaign
    ///   circumstance  who he holds   -- standing and blood, a property of the pair
    ///
    /// The trait weighting comes from what the game says each one means, not from
    /// taste. Honor is "respecting your formal commitments and obeying the law",
    /// and taking a prisoner's property is the plainest breach of the customs of
    /// war there is, so it leads. Generosity runs Tightfisted to Munificent and
    /// is about wanting what is not yours. Mercy is Sadistic to Compassionate,
    /// and stripping a helpless man is a cruelty, though a small one. Calculating
    /// is Hotheaded to Cerebral: a man who weighs his long-term interests can see
    /// what robbing a peer will cost him in standing, and stays his hand.
    ///
    /// Valor is left out on purpose. "Risking your life to win glory or wealth"
    /// says nothing about a disarmed man in a cell.
    /// </summary>
    public static class PlunderRules
    {
        /// <summary>
        /// Bandits take everything. There is no honour to appeal to, no standing
        /// to lose and no relation to spend, so none of the rest applies.
        /// </summary>
        public const float BanditChance = 1.0f;

        // --- disposition: who the captor is ---------------------------------

        /// <summary>
        /// Relative pull of each trait, summing to ten so the weighted score
        /// stays on the traits' own -2..+2 scale.
        /// </summary>
        public const int HonorWeight = 4;
        public const int GenerosityWeight = 3;
        public const int MercyWeight = 2;
        public const int CalculatingWeight = 1;

        /// <summary>
        /// How sharply disposition falls away from the worst man to the best.
        ///
        /// The curve exists because the linear reading puts a lord of wholly
        /// average character at one capture in two, and Calradia's custom is
        /// ransom rather than robbery -- a man with no particular vice should be
        /// an unusual thief, not a coin flip. Cubed puts him at one in eight and
        /// leaves both ends intact: all four traits at their worst still reaches
        /// certainty, and all four at their best still reaches never.
        /// </summary>
        public const int DispositionCurve = 3;

        /// <summary>
        /// What the highest roguery in the game adds, as a share of the man's
        /// own disposition. Multiplied rather than added, so skill at theft
        /// makes a thief worse and leaves an honest man honest.
        /// </summary>
        public const float RogueryReach = 0.5f;

        /// <summary>Roguery at which that full bonus applies.</summary>
        public const int RogueryScale = 300;

        // --- circumstance: who he is holding --------------------------------

        /// <summary>
        /// Goodwill protects outright and enmity aggravates by half. The
        /// asymmetry is deliberate: a man you like is a man you do not rob at
        /// all, while a man you hate is not thereby someone you rob twice.
        /// </summary>
        public const int FriendshipShield = 100;
        public const int EnmitySpur = 200;

        /// <summary>A clansman is family enough to be a rare victim.</summary>
        public const float ClanFactor = 0.25f;

        /// <summary>
        /// Below this, blood has already failed and kinship stops protecting.
        /// Deep hostility on the game's -100..+100 scale, not a mere quarrel.
        /// </summary>
        public const int FeudRelation = -50;

        /// <summary>How close two heroes are by blood or marriage.</summary>
        public enum Kinship
        {
            None,

            /// <summary>Of the same house: family at a remove.</summary>
            Clan,

            /// <summary>Parent, child, sibling, spouse.</summary>
            Immediate
        }

        /// <summary>
        /// The chance this captor strips this prisoner, from zero to one.
        ///
        /// Traits arrive on the game's own -2..+2 scale and are clamped rather
        /// than trusted: a trait some other mod has widened must not quietly
        /// turn a tendency into a certainty.
        /// </summary>
        public static float Chance(bool captorIsBandit, int honor, int mercy, int generosity, int calculating,
                                   int roguery, int relation, Kinship kinship, float multiplier)
        {
            if (captorIsBandit) return Clamp(BanditChance * multiplier);

            float disposition = Disposition(honor, mercy, generosity, calculating, roguery);
            if (disposition <= 0f) return 0f;

            return Clamp(disposition * Circumstance(relation, kinship) * multiplier);
        }

        /// <summary>
        /// How willing this man is to rob anybody, before it matters who.
        ///
        /// Zero for a lord who is honourable, munificent, compassionate and
        /// cerebral all at once -- and zero means zero, not "rarely". Some men
        /// simply do not do this, and a model where everyone eventually does
        /// loses the only thing that makes the mechanic read as character.
        /// </summary>
        private static float Disposition(int honor, int mercy, int generosity, int calculating, int roguery)
        {
            honor = ClampTrait(honor);
            mercy = ClampTrait(mercy);
            generosity = ClampTrait(generosity);
            calculating = ClampTrait(calculating);

            float score = (HonorWeight * honor
                           + GenerosityWeight * generosity
                           + MercyWeight * mercy
                           + CalculatingWeight * calculating)
                          / (float)(HonorWeight + GenerosityWeight + MercyWeight + CalculatingWeight);

            // Score runs +2 (a paragon) to -2 (a brute); fold it into 0..1 with
            // the good end at zero, then bend it away from the middle.
            float linear = (2f - score) / 4f;
            float disposition = linear;
            for (int i = 1; i < DispositionCurve; i++) disposition *= linear;

            if (roguery > 0)
            {
                float reach = roguery > RogueryScale ? RogueryReach
                                                     : RogueryReach * roguery / RogueryScale;
                disposition *= 1f + reach;
            }

            return disposition < 0f ? 0f : disposition;
        }

        /// <summary>
        /// How much this particular prisoner invites or forbids it.
        ///
        /// Blood is a veto rather than a weight, and it is checked against the
        /// relation because a father who has come to hate his son is no longer
        /// protected by being his father.
        /// </summary>
        private static float Circumstance(int relation, Kinship kinship)
        {
            if (kinship == Kinship.Immediate && relation > FeudRelation) return 0f;

            float factor;
            if (relation > 0)
            {
                factor = 1f - relation / (float)FriendshipShield;
                if (factor < 0f) factor = 0f;
            }
            else
            {
                factor = 1f + (-relation) / (float)EnmitySpur;
            }

            // A feud strips the veto but not the reticence: robbing your own
            // brother is still a rarer thing than robbing a stranger.
            if (kinship == Kinship.Clan || kinship == Kinship.Immediate) factor *= ClanFactor;

            return factor;
        }

        /// <summary>
        /// What a reprisal costs against what an unprovoked robbery costs.
        ///
        /// Borrowed whole from the game's own answer to the same question.
        /// DefaultExecutionRelationModel asks exactly one thing before pricing
        /// an execution -- does the executed man have negative Honor -- and if
        /// he does, every figure halves: -60 with his clan becomes -30, -30
        /// with his friends becomes -15, -10 with his faction becomes -5. Only
        /// the uninvolved honourable noble, who would otherwise take -10,
        /// stops caring altogether.
        ///
        /// Half rather than nothing, and that is the part worth copying. The
        /// game never says a killing was free because the dead man was a
        /// villain; it says it was cheaper. Stripping a thief is still
        /// stripping a beaten prisoner, and the man who does it should still
        /// pay something for it.
        ///
        /// Negative Honor is the test because it is the game's test, and
        /// because it asks nothing of the past. A ledger of who robbed whom
        /// would be the first save data this mod has ever written, and would
        /// buy less than it looks: Honor is the heaviest term in Disposition,
        /// weighted 4, so the lords who rob prisoners are overwhelmingly the
        /// ones already below zero. A paragon robs at 0%, a deceitful man at
        /// 61%. The trait is not a record of what he did to you; it is a
        /// description of the sort of man who would.
        /// </summary>
        public const int ReprisalDivisor = 2;

        /// <summary>
        /// Whether taking this man's gear answers his own conduct rather than
        /// beginning something.
        /// </summary>
        public static bool IsReprisal(int victimHonor)
        {
            return victimHonor < 0;
        }

        /// <summary>
        /// A cost after the reprisal discount, when one applies. Integer
        /// division truncates toward zero, so a cost never grows and never
        /// changes sign on the way through.
        /// </summary>
        public static int AfterReprisal(int cost, bool reprisal)
        {
            return reprisal ? cost / ReprisalDivisor : cost;
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
