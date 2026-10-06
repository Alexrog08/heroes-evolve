namespace HeroesEvolve.Core
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
    /// Two things sit beside the dice and are not character at all: a debt
    /// and a name. A house that has sworn vengeance takes it back from the
    /// house it swore against whoever its captor is (VengeanceChance), and an
    /// honourable man will strip a known thief he would otherwise have let be
    /// (JusticePerLevel). Neither is his own doing, and what a robbery costs
    /// depends on which it was (Motive).
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

        /// <summary>
        /// What a slider setting of 1.00 hands these rules: the rate a normal
        /// campaign runs at.
        ///
        /// Not applied here. Chance takes the multiplier it is given and means
        /// it, so multiplier 1.0 is the character model at full strength -- a
        /// paragon at zero, a brute at one, bandits at one -- which is the
        /// anchor the tests and the arithmetic are written against. This
        /// constant is the separate question of how much of that model a
        /// campaign should actually get, and Settings applies it.
        ///
        /// Half, from measurement. At full strength a census found 83 robberies
        /// among 405 lords in seven months: a third of the nobility stripped
        /// every year, which made robbery a bigger influence on what a lord
        /// wears than his skill, his clan's wealth and the market put together.
        /// The purchase engine was working; it simply could not build faster
        /// than this tore down.
        ///
        /// It is a constant rather than the slider's default because a default
        /// of 0.50 invites the question "half of what?", and the honest answer
        /// -- half of a setting nobody should use -- is not something a player
        /// should have to read a comment to learn. The dial he sees starts at
        /// 1.00 and means his campaign.
        /// </summary>
        public const float NormalRate = 0.5f;

        // --- disposition: who the captor is ---------------------------------

        /// <summary>
        /// Relative pull of each trait, summing to ten so the weighted score
        /// stays on the traits' own -2..+2 scale.
        /// </summary>
        /// <summary>
        /// What the diamond is worth, measured rather than reasoned.
        ///
        /// These four weights were set against a scale of minus two to plus two
        /// and argued about in those terms -- a paragon who never robs, a brute
        /// who always does. A census of 495 lords found that campaign does not
        /// contain either man. Every trait sits between minus one and plus one,
        /// and one lord in the whole world holds a plus two, in Mercy:
        ///
        ///   Honor       weight 4    -1: 20%   0: 53%   +1: 27%
        ///   Generosity  weight 3    -1: 20%   0: 55%   +1: 26%
        ///   Mercy       weight 2    -1: 24%   0: 54%   +1: 22%
        ///   Calculating weight 1    -1: 22%   0: 46%   +1: 32%
        ///
        /// So the extremes the design reasoned from were never reachable. What
        /// matters is that the middle landed anyway. The chance those weights
        /// produce, across the same 495 lords and against a stranger:
        ///
        ///   min 2%   p25 8%   median 13%   p75 19%   p90 26%   max 55%
        ///
        /// A median of thirteen percent is exactly the figure this model was
        /// aimed at, and the spread runs twenty-seven fold from the most
        /// honourable lord to the worst. The weights are doing their work
        /// inside the range the game hands them; only the description of that
        /// work was wrong, and it is corrected here and in the settings screen
        /// the player reads.
        ///
        /// ClampTrait still guards minus two to plus two. It has never fired
        /// and costs nothing, and a trait system is not something to assume
        /// will stay as narrow as one campaign found it.
        /// </summary>
        public const int HonorWeight = 4;
        public const int GenerosityWeight = 3;

        /// <summary>
        /// Level with Generosity, and it was not always.
        ///
        /// Two, at first, on the reasoning that stripping a prisoner is more a
        /// dishonesty and a greed than a cruelty -- you take his things, you do
        /// not hurt him. That is true of the act and misses the man. A merciful
        /// lord does not leave somebody naked even when honour is not what
        /// guides him: it is the sight of a noble sitting in a ditch with
        /// nothing that stops him, and pity is reason enough on its own.
        ///
        /// So compassion now restrains him exactly as much as loyalty to his
        /// own does, and only his word binds him harder. The numbers say the
        /// same, from a neutral man at 12.5%: Honor swings 13.8 points across
        /// its range, Generosity and Mercy 10.3 each, Calculating 3.8.
        ///
        /// The calibration survives the change untouched, which is why it was
        /// safe to make. Every trait level moves the score through the same
        /// denominator, so a lord who is neutral in all four still sits at
        /// 12.5%, one who is minus one in all four still at 42.2%, and one who
        /// is plus one throughout still at 1.6%. What moved is the balance
        /// between the middle two, which is all that was asked.
        /// </summary>
        public const int MercyWeight = 3;

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
        ///
        /// Enmity was briefly much more than this. In two versions of the
        /// rule that never shipped, bad blood wore a captor's restraint away
        /// altogether -- sixteen percent at minus thirty, certainty at minus a
        /// hundred -- so that a house would answer the house that robbed it.
        /// It did that badly and something else too well: the robbed house
        /// stripped its robber one time in six, and every pair of houses with
        /// bad blood between them for any reason robbed each other more. An
        /// answer has to know who is owed, which a standing cannot say, and
        /// once something does (VengeanceChance) the standing has no more to
        /// add than it had before.
        /// </summary>
        public const int FriendshipShield = 100;
        public const int EnmitySpur = 200;

        /// <summary>
        /// What each level of a prisoner's dishonour wears away of an
        /// honourable captor's restraint.
        ///
        /// Reputation needs no ledger, because the game already keeps one.
        /// Honor is what a man is known for -- the encyclopedia's "reputed to
        /// be" reads it -- and the player's falls with what he does: half an
        /// execution's worth for each prisoner he strips (RobberyCost). Two
        /// robberies make him a man honourable lords know for a thief, and
        /// they take his arms when they hold him as a matter of justice
        /// rather than of appetite. An AI lord's Honor is fixed the day he is
        /// made, so for him it is what it has always been in this file: not a
        /// record of what he did, but a description of the sort of man who
        /// would.
        ///
        /// The game charges an execution to every honourable noble in
        /// Calradia as ten points of relation each. That was tried here at
        /// half the price and simulated, and it sank the map. One robbery
        /// touched some eighteen houses, the same eighteen every time, and
        /// since nearly every house robs somebody sooner or later -- 156
        /// different captors in the log of one campaign -- thirteen years put
        /// one pair of houses in five at minus thirty or worse, a kingdom's
        /// own vassals and its king among them. Relation between AI lords is
        /// not decoration: clans leave kingdoms on it, armies cost influence
        /// by it, marriages and alliances are refused over it. A name costs
        /// the map nothing.
        ///
        /// Honourable captors only. A man with no honour of his own robs by
        /// appetite and needs no excuse, and a neutral one is not offended on
        /// principle. And worn away rather than multiplied, because the men
        /// it has to move are the ones with the least appetite: his own
        /// chance, plus this share of everything that was holding him back.
        ///
        /// Three tenths a level. An honourable lord, 3.4% against a stranger
        /// in a normal campaign, strips a man of Honor minus one 17% of the
        /// time and a man of minus two 31%.
        /// </summary>
        public const float JusticePerLevel = 0.3f;

        /// <summary>
        /// How surely a house takes back from the house that robbed it: three
        /// captures in four.
        ///
        /// A robbed lord swears vengeance on the man who robbed him
        /// (VengeanceOaths), and from then on his house is owed one robbery
        /// by the other. This is the chance it collects when it holds one of
        /// theirs. Collecting strikes the oath off, so one robbery is answered
        /// at most once, and that is what lets the number be high.
        ///
        /// Not a matter of character, which is why it is a number of its own
        /// and not a term in Disposition. A lord who would not rob one
        /// stranger in sixty still takes his own back from the house that
        /// took it, and a brute is no surer of it than he is. It is also not
        /// scaled by NormalRate. That constant says how often character robs
        /// in a normal campaign; a debt is collected or it is not. The
        /// campaign's own dial still scales it, so setting robbery to nothing
        /// stops this with the rest.
        ///
        /// Simulated over thirty years of seventy-two houses, against about
        /// twenty robberies of character a year: three vengeances in the
        /// first year, nine in the thirteenth, and about a dozen at most,
        /// because most oaths wait years for the two houses to meet and the
        /// game forgets one after twenty. The standing between houses ends
        /// within a tenth of a point of where it ends without this rule.
        ///
        /// Three in four rather than always, so that being taken by a house
        /// that has sworn against yours is a bad day and not a foregone one.
        /// </summary>
        public const float VengeanceChance = 0.75f;

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
        /// The chance this captor strips a prisoner with no name against him,
        /// from zero to one. What the census and the store page read.
        /// </summary>
        public static float Chance(bool captorIsBandit, int honor, int mercy, int generosity, int calculating,
                                   int roguery, int relation, Kinship kinship, float multiplier)
        {
            float ownDoing;
            return Chance(captorIsBandit, honor, mercy, generosity, calculating, roguery,
                          relation, kinship, 0, multiplier, out ownDoing);
        }

        /// <summary>
        /// The chance this captor strips this prisoner, from zero to one, and
        /// the part of it that is his own doing.
        ///
        /// Two numbers, because a robbery can have two causes here and they
        /// are priced differently (Motive). ownDoing is his character against
        /// this man -- disposition, standing and blood -- and is exactly the
        /// chance this function returned before it returned two. The return
        /// value adds, for an honourable captor holding a man without honour,
        /// the share of his restraint that the prisoner's name wears away
        /// (JusticePerLevel).
        ///
        /// What a house that is owed does is not here at all: see Vengeance.
        ///
        /// Traits arrive on the game's own -2..+2 scale and are clamped rather
        /// than trusted: a trait some other mod has widened must not quietly
        /// turn a tendency into a certainty.
        /// </summary>
        public static float Chance(bool captorIsBandit, int honor, int mercy, int generosity, int calculating,
                                   int roguery, int relation, Kinship kinship, int prisonerHonor,
                                   float multiplier, out float ownDoing)
        {
            ownDoing = 0f;

            if (captorIsBandit)
            {
                ownDoing = Clamp(BanditChance * multiplier);
                return ownDoing;
            }

            // A paragon stays one. No thief's name makes a robber of the man
            // Disposition puts at nought: zero still means zero.
            float disposition = Disposition(honor, mercy, generosity, calculating, roguery);
            if (disposition <= 0f) return 0f;

            float own = disposition * Circumstance(relation, kinship);
            ownDoing = Clamp(own * multiplier);

            // His friend's bad name is still his friend's, and his son's is
            // still his son's: goodwill and blood stay his sense of justice
            // exactly as they stay his hand. Enmity adds nothing to it -- he
            // is not stripping the man because he hates him.
            float worn = Justice(honor, prisonerHonor) * Circumstance(relation > 0 ? relation : 0, kinship);
            if (worn <= 0f) return ownDoing;
            if (worn > 1f) worn = 1f;

            float held = own > 1f ? 1f : own;
            float chance = Clamp((held + (1f - held) * worn) * multiplier);

            return chance > ownDoing ? chance : ownDoing;
        }

        /// <summary>
        /// The chance a captor whose house is owed takes it back from this
        /// prisoner. Blood counts as it does everywhere, standing does not,
        /// and the rate is the campaign's dial alone. See VengeanceChance.
        /// </summary>
        public static float Vengeance(int relation, Kinship kinship, float rate)
        {
            float blood = 1f;
            if (kinship == Kinship.Immediate && relation > FeudRelation) blood = 0f;
            else if (kinship != Kinship.None) blood = ClanFactor;

            return Clamp(VengeanceChance * blood * rate);
        }

        /// <summary>
        /// What a prisoner's own name wears away of an honourable captor's
        /// restraint. See JusticePerLevel.
        /// </summary>
        public static float Justice(int captorHonor, int prisonerHonor)
        {
            if (ClampTrait(captorHonor) <= 0) return 0f;

            int dishonour = -ClampTrait(prisonerHonor);
            if (dishonour <= 0) return 0f;

            return JusticePerLevel * dishonour;
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
            //
            // The bend is what makes a rare trait worth having, and the numbers
            // are worth writing down because the question came up and nobody
            // had ever worked them out. Cubed, with all four traits level:
            //
            //   +2   0.0%      -- and zero means never
            //   +1   1.6%
            //    0  12.5%
            //   -1  42.2%
            //   -2 100.0%      -- and a hundred means always
            //
            // The step from -1 to -2 is fifty-eight percentage points, the
            // largest on the scale. That matters more than it looks: a census
            // found every trait in a live campaign confined to -1 through +1,
            // with one lord in 495 holding a +2 and none a -2, because
            // AgingCampaignBehavior.OnHeroReachesTeenAge copies a parent's
            // trait far more often than it drifts, and only ever drifts by one.
            // TraitObject.Initialize sets the bounds at -2 to 2, so both
            // extremes are reachable -- they simply take generations.
            //
            // Which is the right shape for something that rare. Moving Honor
            // alone, the rest neutral, runs 2.7% at +2 through 12.5% at zero to
            // 34.3% at -2: one exceptional trait nearly triples a man's
            // appetite or quarters it, without needing him to be exceptional in
            // all four.
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
        /// The die for one captor and one prisoner, cast once and for all.
        ///
        /// A lord walks into his own keep as often as he likes, and a fresh
        /// random roll on each visit would make the chance meaningless: an
        /// eight-percent man visiting fifty times strips everyone, and the
        /// character model that decides who robs would be describing nothing.
        /// Remembering who has already been considered would be the obvious
        /// answer and would also be the first save data this mod has ever
        /// written.
        ///
        /// So the die is not thrown, it is read. The same captor, the same
        /// prisoner and the same spell in the cells always give the same
        /// number, and revisiting simply asks the same question again -- which
        /// is the third time this project has replaced a ledger with a property
        /// of the thing itself, after Talent.For and GrantTier.Choose, and it
        /// uses their hash.
        ///
        /// What does move is the bar the number has to clear. Chance rises with
        /// his Roguery and falls as the two men warm to each other, so a man he
        /// spared at fifty may not be safe from him at a hundred. That reads
        /// exactly right: the decision was never re-rolled, the lord simply
        /// became more of a thief. And it can never run backwards into robbing
        /// a man twice, because after the first time there is nothing left on
        /// him to take.
        ///
        /// The episode is what keeps "for ever" from meaning it. Hashing two
        /// names alone made the answer a fact about the pair rather than about
        /// the captivity: a lord who spared a man once would never rob him
        /// again in forty years of war, which is a hash pretending to be
        /// character. Feeding in the moment his captivity began -- a number the
        /// game already keeps and rewrites on every capture -- makes each spell
        /// in the cells its own question, settled once and then left alone.
        ///
        /// One in ten thousand, which is finer than any chance this model
        /// produces. Missing names give 1.0 -- never robbed -- since the only
        /// captors with certainty are bandits, and bandits hold no keeps.
        /// </summary>
        public static float Draw(string captorId, string prisonerId, long episode)
        {
            if (string.IsNullOrEmpty(captorId) || string.IsNullOrEmpty(prisonerId)) return 1f;

            uint hash = 2166136261u;
            string text = captorId + "/" + prisonerId + "/" + episode;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }

            return (hash % 10000u) / 10000f;
        }

        /// <summary>
        /// Why a robbery happened, which is what decides its price.
        ///
        /// Character: his own doing. It costs his house standing with the
        /// house he robbed, as it always has (RobberyCost), and the prisoner
        /// swears vengeance for it (VengeanceOaths).
        ///
        /// Vengeance: his house held such an oath against the prisoner's and
        /// has taken what it was owed. An answer and not an offence. It costs
        /// nothing, nobody swears anything, and the oath is struck off.
        ///
        /// Justice: an honourable man stripping a known thief he would
        /// otherwise have let be. Not an offence either. Nothing is charged
        /// and nothing is sworn: the game's own rule for a man without honour
        /// is that what is done to him costs less, and what only his name
        /// brought on him he brought on himself.
        ///
        /// Only the first makes a debt, and that is what keeps a feud from
        /// feeding on itself. An answer is never itself answered, so one
        /// robbery of character leads to at most one more robbery, ever.
        ///
        /// The player is never drawn for. He robs by choosing to
        /// (PrisonerDialogue), so a robbery of his is his own doing by
        /// definition -- unless his house is the one that is owed, when it is
        /// vengeance for him exactly as it would be for anybody.
        /// </summary>
        public enum Motive
        {
            /// <summary>No robbery.</summary>
            None,

            /// <summary>His own doing: his character accounts for it without help.</summary>
            Character,

            /// <summary>Only the prisoner's name accounts for it.</summary>
            Justice,

            /// <summary>His house was owed, and took it back.</summary>
            Vengeance
        }

        /// <summary>Reads one draw against the two bars Chance returns.</summary>
        public static Motive Judge(float draw, float ownDoing, float chance)
        {
            if (chance <= 0f || draw > chance) return Motive.None;
            return draw <= ownDoing ? Motive.Character : Motive.Justice;
        }

        /// <summary>
        /// The same, for a captor whose house may be owed. vengeance is
        /// nought when it is not, and then this is the plain reading. When it
        /// is, every robbery is vengeance whatever would otherwise have caused
        /// it: a man collecting a debt is not also committing an offence, even
        /// if he is the sort who would have robbed the prisoner anyway.
        /// </summary>
        public static Motive Judge(float draw, float ownDoing, float chance, float vengeance)
        {
            if (vengeance <= 0f) return Judge(draw, ownDoing, chance);

            float bar = vengeance > chance ? vengeance : chance;
            return draw <= bar ? Motive.Vengeance : Motive.None;
        }

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
