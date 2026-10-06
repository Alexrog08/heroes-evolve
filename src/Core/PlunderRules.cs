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
    /// Standing cuts both ways and not alike. Goodwill scales what his
    /// character would do, down to nothing. Bad blood does not scale it: it
    /// wears away what holds him back, so that a decent man robs the house he
    /// has a feud with as a brute robs anyone -- and a robbery that only the
    /// feud explains is priced differently from one that is his own doing. See
    /// GrudgeDeadZone and Motive.
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
        /// Goodwill protects outright: at a hundred a man is not robbed at all,
        /// whoever holds him.
        /// </summary>
        public const int FriendshipShield = 100;

        /// <summary>
        /// Bad blood wears a man's restraint away, and at its worst leaves him
        /// none.
        ///
        /// This is bad blood in general, whatever made it: the friend of a
        /// man who was robbed, a kingdom that has watched it done to its own
        /// six times over, a house whose villages were burnt. It is not how a
        /// robbed house answers the house that robbed it. That one has sworn
        /// vengeance and does not wait on a curve (VengeanceChance).
        ///
        /// Enmity used to multiply: one and a half times the chance at minus a
        /// hundred, which took a lord of average character from 6.2% to 7.2%
        /// at minus thirty -- a difference nobody could have told from the
        /// saddle. A multiplier was also the wrong shape. It left an honourable
        /// man an honourable man however he had been provoked.
        ///
        /// So the grudge works on what holds him back instead. Whatever share
        /// of his restraint it has worn away, he robs as though that share were
        /// not there: own + (1 - own) x grudge, where own is what his character
        /// does unprovoked. A stranger is robbed exactly as before.
        ///
        /// The dead zone is where dislike is not yet a grudge. Relation drifts
        /// a few points for a dozen reasons that have nothing to do with being
        /// robbed -- a raided village costs up to six
        /// (CharacterRelationCampaignBehavior.OnRaidCompleted), two men's
        /// characters up to four (DefaultDiplomacyModel.GetPersonalityEffects)
        /// -- and none of that should strip a prisoner. It also keeps the
        /// smallest price of a robbery, five with each house of the victim's
        /// kingdom, from meaning anything until a man has made a habit of it.
        ///
        /// Linear from there to certainty at minus a hundred. A lord neutral in
        /// all four traits, in a normal campaign:
        ///
        ///      0    6.2%   a stranger
        ///    -15    8.7%   the friend of a man you robbed
        ///    -30   16.0%   his kingdom, once you have robbed six of its lords
        ///    -60   30.6%
        ///   -100   50.0%   as sure as a bandit
        ///
        /// Simulated over thirty years of seventy-two houses, it adds six to
        /// nine robberies a year to some twenty of character, and holds there.
        /// What it adds in a real campaign is the one thing a simulation
        /// cannot settle, because it depends on how much bad blood that
        /// campaign makes for itself -- raids, defections, a decade of war --
        /// and with three times the game's own share of it the same slope gave
        /// twenty-seven a year by the thirtieth. It does not feed on itself
        /// (Motive says why), but it does turn whatever enmity the map already
        /// holds into robberies. So the census prints the conversion for the
        /// campaign it is run in (FEUD perCapture), and that line is what this
        /// constant answers to.
        /// </summary>
        public const int GrudgeDeadZone = 10;
        public const int GrudgeFull = 100;

        /// <summary>
        /// What each level of a prisoner's dishonour is worth to an honourable
        /// captor, as a grudge he holds without ever having met the man.
        ///
        /// The game charges an execution to four circles: the dead man's clan,
        /// his friends, his kingdom, and every honourable noble in Calradia. A
        /// robbery is charged to the first three at half the price
        /// (RobberyCost). The fourth is here instead, and not as relation.
        ///
        /// As relation it was simulated and it sank the map. One robbery
        /// touched some eighteen houses, the same eighteen every time, and
        /// since nearly every house robs somebody sooner or later -- 156
        /// different captors in the log of one campaign -- thirteen years put
        /// one pair of houses in five at minus thirty or worse, a kingdom's own
        /// vassals and its king among them. Relation between AI lords is not
        /// decoration: clans leave kingdoms on it, armies cost influence by it,
        /// marriages and alliances are refused over it.
        ///
        /// Reputation needs no ledger, because the game already keeps one.
        /// Honor is what a man is known for -- the encyclopedia's "reputed to
        /// be" reads it -- and the player's falls with what he does: half an
        /// execution's worth for each prisoner he strips. Two robberies make
        /// him a man honourable lords know for a thief, and they take his arms
        /// when they hold him as a matter of justice rather than of appetite.
        /// An AI lord's Honor is fixed the day he is made, so for him it is
        /// what it has always been in this file: not a record of what he did,
        /// but a description of the sort of man who would.
        ///
        /// Honourable captors only. A man with no honour of his own robs by
        /// appetite and needs no excuse, and a neutral one is not offended on
        /// principle.
        ///
        /// Three tenths a level. An honourable lord, 3.4% against a stranger in
        /// a normal campaign, strips a man of Honor minus one 17% of the time
        /// and a man of minus two 31%.
        /// </summary>
        public const float JusticePerLevel = 0.3f;

        /// <summary>
        /// How surely a house takes back from the house that robbed it: three
        /// captures in four.
        ///
        /// Not a matter of character, which is why it is a number of its own
        /// and not a term in Disposition. A lord who would not rob one stranger
        /// in sixty still takes his own back from the house that took it, and
        /// a brute is no surer of it than he is. It is also not scaled by
        /// NormalRate. That constant says how often character robs in a
        /// normal campaign; a debt is collected or it is not. The campaign's
        /// own dial still scales it, so setting robbery to nothing stops this
        /// with the rest.
        ///
        /// High, because it can afford to be. A curve steep enough to make a
        /// robbed house a real danger to its robber also made every pair of
        /// houses with bad blood between them rob one another, for reasons
        /// that had nothing to do with robbery, and doubled the robberies
        /// between lords. An oath is owed by one house to one other for one act, so
        /// this answers at most once for each robbery of character and can be
        /// as sure as it likes. Simulated: two a year in the first year of a
        /// campaign, a dozen by the thirteenth, against twenty robberies of
        /// character -- most oaths wait years for the two houses to meet.
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
        /// The chance this captor strips a prisoner with no name for or against
        /// him, from zero to one. What the census and the store page read.
        /// </summary>
        public static float Chance(bool captorIsBandit, int honor, int mercy, int generosity, int calculating,
                                   int roguery, int relation, Kinship kinship, float multiplier)
        {
            float ownDoing;
            return Chance(captorIsBandit, honor, mercy, generosity, calculating, roguery,
                          relation, kinship, 0, multiplier, out ownDoing);
        }

        /// <summary>
        /// The same, for two houses neither of which has sworn against the
        /// other.
        /// </summary>
        public static float Chance(bool captorIsBandit, int honor, int mercy, int generosity, int calculating,
                                   int roguery, int relation, Kinship kinship, int prisonerHonor,
                                   float multiplier, out float ownDoing)
        {
            return Chance(captorIsBandit, honor, mercy, generosity, calculating, roguery, relation,
                          kinship, prisonerHonor, Claim.None, multiplier, out ownDoing);
        }

        /// <summary>
        /// The chance this captor strips this prisoner, from zero to one, and
        /// the part of it that is his own doing.
        ///
        /// Two numbers, because a robbery has more than one possible cause and
        /// they are priced differently (Motive). ownDoing is what his character
        /// does to a man he has nothing against, friendship still counting in
        /// the prisoner's favour. The return value adds what bad blood wears
        /// away of the rest: his own with the prisoner's house (Grudge), and
        /// for an honourable captor the prisoner's own name (Justice).
        ///
        /// A house that owes takes no courage from the bad blood it made. When
        /// the prisoner's house is the one that has sworn vengeance
        /// (Claim.Owes), the grudge between them is not the captor's to act on
        /// and his own character is all that speaks. Without that the robber
        /// would be likelier to rob his victim's house a second time for
        /// having robbed it a first, and would do it for nothing. What a house
        /// that is owed does is not here at all: see Vengeance.
        ///
        /// The multiplier scales both alike, so a campaign's one dial still
        /// means what it says. At its normal setting a brute, a bandit and the
        /// deepest feud on the map all stop at one prisoner in two.
        ///
        /// Traits arrive on the game's own -2..+2 scale and are clamped rather
        /// than trusted: a trait some other mod has widened must not quietly
        /// turn a tendency into a certainty.
        /// </summary>
        public static float Chance(bool captorIsBandit, int honor, int mercy, int generosity, int calculating,
                                   int roguery, int relation, Kinship kinship, int prisonerHonor,
                                   Claim claim, float multiplier, out float ownDoing)
        {
            ownDoing = 0f;

            if (captorIsBandit)
            {
                ownDoing = Clamp(BanditChance * multiplier);
                return ownDoing;
            }

            // A paragon stays one. No feud and no thief's name makes a robber
            // of the man Disposition puts at nought: zero still means zero.
            float disposition = Disposition(honor, mercy, generosity, calculating, roguery);
            if (disposition <= 0f) return 0f;

            float blood = Blood(relation, kinship);
            if (blood <= 0f) return 0f;

            float shield = Shield(relation);
            float own = disposition * shield;
            if (own > 1f) own = 1f;

            // His friend's bad name is still his friend's: the same shield
            // that stays his hand stays his sense of justice.
            float worn = (claim == Claim.Owes ? 0f : Grudge(relation))
                         + Justice(honor, prisonerHonor) * shield;
            if (worn > 1f) worn = 1f;

            ownDoing = Clamp(own * blood * multiplier);
            return Clamp((own + (1f - own) * worn) * blood * multiplier);
        }

        /// <summary>
        /// How much of a captor's restraint his bad blood with the prisoner's
        /// house has worn away, from none to all of it. See GrudgeDeadZone.
        /// </summary>
        public static float Grudge(int relation)
        {
            int depth = -relation;
            if (depth <= GrudgeDeadZone) return 0f;
            if (depth >= GrudgeFull) return 1f;

            return (depth - GrudgeDeadZone) / (float)(GrudgeFull - GrudgeDeadZone);
        }

        /// <summary>
        /// The chance a captor whose house is owed takes it back from this
        /// prisoner. Blood still counts, and the rate is the campaign's dial
        /// alone. See VengeanceChance.
        /// </summary>
        public static float Vengeance(int relation, Kinship kinship, float rate)
        {
            return Clamp(VengeanceChance * Blood(relation, kinship) * rate);
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
        /// What kinship does to the whole of it, grudge included.
        ///
        /// Blood is a veto rather than a weight, and it is checked against the
        /// relation because a father who has come to hate his son is no longer
        /// protected by being his father.
        /// </summary>
        private static float Blood(int relation, Kinship kinship)
        {
            if (kinship == Kinship.Immediate && relation > FeudRelation) return 0f;

            // A feud strips the veto but not the reticence: robbing your own
            // brother is still a rarer thing than robbing a stranger.
            return kinship == Kinship.None ? 1f : ClanFactor;
        }

        /// <summary>
        /// How much of his own doing goodwill lets through: all of it for a
        /// man he does not care for, none at FriendshipShield.
        /// </summary>
        private static float Shield(int relation)
        {
            if (relation <= 0) return 1f;

            float shield = 1f - relation / (float)FriendshipShield;
            return shield < 0f ? 0f : shield;
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
        /// Which of two houses has sworn vengeance on the other, seen from the
        /// captor's side.
        ///
        /// A standing between two houses is one number and both of them wear
        /// it, so it cannot say who wronged whom -- and everything that makes
        /// an answer an answer depends on exactly that. Giving a house its
        /// standing back because it has just been robbed only makes sense if
        /// it robbed the other first. So the direction is kept where the game
        /// keeps such things: a robbed lord swears vengeance on the man who
        /// robbed him, and the oath is written in the game's own log, as the
        /// game writes the oaths of its own backstory (VengeanceOaths).
        ///
        /// The oath says who; the standing says whether anything is still
        /// outstanding. A house is owed while the last oath between the two is
        /// its own and the standing between them is below nought. Taking
        /// vengeance pays the standing back a robbery's worth, and so does
        /// anything else that mends it -- a prisoner let go, a marriage -- and
        /// when it is back at nought the oath is history. The last oath and
        /// not a count of them, because what has been answered is not written
        /// down: if the two houses have robbed each other in turn, the one
        /// wronged most recently is the one with something left to answer.
        /// </summary>
        public enum Claim
        {
            /// <summary>Nothing outstanding between the two houses.</summary>
            None,

            /// <summary>The captor's house is owed by the prisoner's.</summary>
            Owed,

            /// <summary>The captor's house owes the prisoner's.</summary>
            Owes
        }

        /// <summary>
        /// Reads the last oath between two houses as a claim. lastSworn is the
        /// house that swore it and lastAgainst the house it was sworn on; both
        /// null when there has never been one.
        /// </summary>
        public static Claim ClaimFor(string lastSworn, string lastAgainst,
                                     string captorHouse, string prisonerHouse, int standing)
        {
            if (standing >= 0) return Claim.None;
            if (string.IsNullOrEmpty(captorHouse) || string.IsNullOrEmpty(prisonerHouse)) return Claim.None;
            if (captorHouse == prisonerHouse) return Claim.None;

            if (lastSworn == captorHouse && lastAgainst == prisonerHouse) return Claim.Owed;
            if (lastSworn == prisonerHouse && lastAgainst == captorHouse) return Claim.Owes;
            return Claim.None;
        }

        /// <summary>
        /// Why a robbery happened, which is what decides its price.
        ///
        /// Character: he would have robbed a stranger on the same draw. His own
        /// doing, charged in full (RobberyCost), and the prisoner swears
        /// vengeance for it.
        ///
        /// Vengeance: his house was owed by the prisoner's and has taken it
        /// back. An answer and not an offence. It costs him nothing, nobody
        /// swears anything, and the standing between the two houses is paid
        /// back by what a robbery costs (RobberyCost.Settled).
        ///
        /// Grudge: he was owed nothing, but bad blood or the prisoner's name
        /// wore away what would have stopped him. Free as well, and it settles
        /// nothing, because there was no debt of his to settle.
        ///
        /// Only the first is charged, and that is the whole defence against
        /// relation feeding on itself. It is arithmetic rather than tuning.
        /// Were every robbery charged, bad blood would raise the chance of
        /// robbery and robbery would deepen the bad blood, between every two
        /// houses at once. Charging his own doing alone means the relation a
        /// captor is expected to lose at a capture is his own chance times the
        /// price -- and his own chance does not contain the relation. However
        /// deep a feud runs it adds nothing to itself; the vengeance it leads
        /// to can only pay it down.
        ///
        /// Simulated over thirty years of seventy-two houses: one pair of
        /// houses in six at minus thirty or worse, against nearly one in two
        /// when all four of the game's circles were charged as relation with
        /// no such rule.
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

            /// <summary>Bad blood or the prisoner's name accounts for it, and no debt.</summary>
            Grudge,

            /// <summary>His house was owed, and took it back.</summary>
            Vengeance
        }

        /// <summary>Reads one draw against the two bars Chance returns.</summary>
        public static Motive Judge(float draw, float ownDoing, float chance)
        {
            if (chance <= 0f || draw > chance) return Motive.None;
            return draw <= ownDoing ? Motive.Character : Motive.Grudge;
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
