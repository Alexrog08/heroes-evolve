using HeroesEvolve.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// Taking a captured lord's arms, by asking him for them yourself.
    ///
    /// This is the player's whole share of the mechanic, and it is a
    /// conversation rather than a roll because the difference matters. An AI
    /// lord's dice ARE his decision -- his character makes it, and he is not
    /// there to be consulted. The player is there. Rolling for him would be the
    /// mod deciding, and then charging him Honor for a choice he never made.
    ///
    /// It hangs off hero_main_options, the same hub that carries "I have decided
    /// to free you. You may go." -- the two options belong side by side, being
    /// the merciful and the grasping ends of the same moment.
    /// </summary>
    public static class PrisonerDialogue
    {
        /// <summary>
        /// The state every answer leads to: he has said what he thinks of it,
        /// and the decision is still the player's.
        /// </summary>
        private const string Decide = "hev_strip_decide";

        public static void Register(CampaignGameStarter starter)
        {
            if (starter == null) return;

            starter.AddPlayerLine("hev_strip_prisoner",
                                  "hero_main_options",
                                  "hev_strip_prisoner_reply",
                                  "{=hev_strip}Hand over your arms and armour.",
                                  CanStripPlainly, null, 100, null);

            starter.AddDialogLine("hev_strip_prisoner_reply",
                                  "hev_strip_prisoner_reply",
                                  Decide,
                                  "{=hev_strip_reply}You would take the arms off a beaten man? "
                                  + "This dishonours you. Take them, then. My clan will hear of it, "
                                  + "and so will yours.",
                                  null, null, 100, null);

            // He answers first and the arms change hands second, and between
            // the two the player may still think better of it.
            //
            // The demand used to be the act: the prisoner's answer carried the
            // robbery as its consequence, so by the time he had told you what
            // it would cost, it had cost it. That was tolerable at twelve
            // relation and a scratch on Honor. It is not at half a level of
            // your name (RobberyReckoning) and his house sworn to take it back
            // from yours (VengeanceOaths).
            //
            // The game asks the same question the same way. Its own "Off with
            // your head!" does not kill anybody: the lord protests that the
            // other lords will hate you for it and his family never forget,
            // adds that a ransom would pay better, and only then are you
            // offered "I care not" beside "Fine then. You are my prisoner now"
            // (LordConversationsCampaignBehavior.AddOtherConversations, the
            // talk_lord_defeat_to_lord_capture_and_kill lines). All three
            // answers below already said what the robbery would be remembered
            // as, so none of them needed a new word.
            starter.AddPlayerLine("hev_strip_take",
                                  Decide,
                                  "close_window",
                                  "{=hev_strip_take}I will take them.",
                                  null, Strip, 100, null);

            starter.AddPlayerLine("hev_strip_leave",
                                  Decide,
                                  "close_window",
                                  "{=hev_strip_leave}Keep them, then.",
                                  null, null, 100, null);

            // The same act against a man who trades in it. Said differently
            // because it is a different thing, and it costs half -- see
            // PlunderRules.AfterReprisal.
            //
            // Honour is named in all three answers, from three angles, and
            // that is what makes them one conversation rather than three. The
            // honourable man says the act dishonours you. The demand made of a
            // scoundrel says there is no honour in him to offend. The scoundrel
            // throws the word back and asks you to finish robbing him first.
            // The friend never mentions it, because between friends it was
            // never the point.
            //
            // A man with Honor above zero has been wronged and says so plainly.
            // A man below zero is not ashamed and is not pretending to be -- he
            // will have better gear inside a month, and he tells you whose back
            // he means to take it off. Which is the only reason the discount
            // reads as fair rather than as a loophole: you can hear that he had
            // it coming.
            //
            // The demand names honour, and that is the whole job of the
            // sentence. Honor below zero is the entire test, and a player
            // cannot see a trait level without going to the encyclopedia to
            // look for it -- so a spare line, and the first draft was "You know
            // how this goes", left him reading a differently worded option and
            // paying half the usual cost with no way to connect either fact to
            // the man in front of him.
            //
            // Stated as an observation rather than a sermon, which matters
            // while you are the one stripping a prisoner: it names what he is
            // without claiming anything about what you are.
            //
            // And he does not concede it. An earlier draft had him agree --
            // "none at all" -- which is the one thing a man of his sort would
            // never do; conceding the charge makes him a device for explaining
            // the mechanic rather than a person. He throws it back instead, and
            // the charge lands, because you are robbing him while you say it.
            // Then the swagger, unchanged: better gear inside a month, and a
            // threat aimed at somebody of your blood.
            starter.AddPlayerLine("hev_strip_prisoner_reprisal",
                                  "hero_main_options",
                                  "hev_strip_prisoner_reprisal_reply",
                                  "{=hev_strip_reprisal}There is no honour in you for me to offend. "
                                  + "Hand over your arms and armour.",
                                  CanStripInReprisal, null, 100, null);

            // And the third man, who is neither a stranger nor a scoundrel.
            // Robbing a friend is not an outrage and not a reckoning; it is a
            // betrayal, and the only one of the three where the prisoner does
            // not argue. He has nothing to argue about -- he is not surprised
            // that it can be done to him, only by whom. That lands harder than
            // either of the others, and it costs the ordinary price: a friend
            // is never a reprisal, whatever his reputation elsewhere.
            starter.AddPlayerLine("hev_strip_prisoner_friend",
                                  "hero_main_options",
                                  "hev_strip_prisoner_friend_reply",
                                  "{=hev_strip_friend}Do not take this personally. "
                                  + "Hand over your arms and armour.",
                                  CanStripAFriend, null, 100, null);

            starter.AddDialogLine("hev_strip_prisoner_friend_reply",
                                  "hev_strip_prisoner_friend_reply",
                                  Decide,
                                  "{=hev_strip_friend_reply}I expected this from anyone but you. "
                                  + "Take them, then. I have nothing else to say to you.",
                                  null, null, 100, null);

            starter.AddDialogLine("hev_strip_prisoner_reprisal_reply",
                                  "hev_strip_prisoner_reprisal_reply",
                                  Decide,
                                  "{=hev_strip_reprisal_reply}Speak to me of honour when you have "
                                  + "finished robbing me. Take it, then, take all of it -- I will have "
                                  + "better within the month, and the next man I strip to his shirt "
                                  + "may well share your name.",
                                  null, null, 100, null);

            // And the fourth, which is not a robbery at all. His clan stripped
            // one of yours, your man swore vengeance for it, and this is you
            // taking it: the same thing an AI lord does three captures in four
            // when the oath is his (PlunderRules.VengeanceChance). It costs
            // nothing -- no standing, no mark on your name -- and it strikes
            // the oath off.
            //
            // Nobody mentions honour in this one, and that is the tell. The
            // other three demands are all arguments about it, because in each
            // of them the prisoner has a grievance. Here he has none and knows
            // it. He does not protest; he recognises the answer, and asks only
            // that it be the last of it -- which, for one robbery, it is.
            starter.AddPlayerLine("hev_strip_prisoner_vengeance",
                                  "hero_main_options",
                                  "hev_strip_prisoner_vengeance_reply",
                                  "{=hev_strip_vengeance}Your clan stripped one of mine. "
                                  + "Hand over your arms and armour.",
                                  CanStripInVengeance, null, 100, null);

            starter.AddDialogLine("hev_strip_prisoner_vengeance_reply",
                                  "hev_strip_prisoner_vengeance_reply",
                                  Decide,
                                  "{=hev_strip_vengeance_reply}So this is the answer. Take them, then, "
                                  + "and let that be the end of it between your clan and mine.",
                                  null, null, 100, null);
        }

        /// <summary>
        /// Which of the four ways this can be asked for.
        ///
        /// A debt, a friend, a scoundrel or a stranger, tested in that order.
        /// A debt comes first because it changes what the act is: a house that
        /// is owed is collecting, whoever the prisoner happens to be -- a
        /// friend included, as it is for an AI lord (PlunderRules.Vengeance).
        /// Then friendship outranks reputation -- a man who is Devious and
        /// also at your side is robbed as a friend and charged the full
        /// price; being crooked is not the same as being crooked with you.
        /// </summary>
        private enum Manner
        {
            Plain,
            Reprisal,
            Friend,
            Vengeance,
        }

        /// <summary>
        /// How this particular prisoner is to be asked, read off him now.
        ///
        /// The oath first: if somebody of your house has sworn vengeance on
        /// somebody of his and nobody has collected it, you are owed
        /// (VengeanceOaths.Owed).
        ///
        /// Then friendship. The reprisal discount exists because a man had it
        /// coming, and a friend never has it coming, however poor his name
        /// elsewhere -- PlunderRules already holds that friendship restrains a
        /// robbery, FriendshipShield scaling his own doing down to nothing.
        /// </summary>
        private static Manner MannerFor(Hero hero)
        {
            if (hero == null) return Manner.Plain;

            if (Hero.MainHero != null && VengeanceOaths.Owed(Hero.MainHero, hero))
            {
                return Manner.Vengeance;
            }

            if (Hero.MainHero != null && hero.IsFriend(Hero.MainHero)) return Manner.Friend;

            if (PlunderRules.IsReprisal(hero.GetTraitLevel(DefaultTraits.Honor)))
            {
                return Manner.Reprisal;
            }

            return Manner.Plain;
        }

        private static bool CanStripPlainly() { return CanStrip(Manner.Plain); }
        private static bool CanStripInReprisal() { return CanStrip(Manner.Reprisal); }
        private static bool CanStripAFriend() { return CanStrip(Manner.Friend); }
        private static bool CanStripInVengeance() { return CanStrip(Manner.Vengeance); }

        /// <summary>
        /// Offered only for a lord you are actually holding, who still has
        /// something to hand over, and only on the branch that matches him.
        /// </summary>
        private static bool CanStrip(Manner want)
        {
            try
            {
                if (!CanStripCore()) return false;
                return MannerFor(Hero.OneToOneConversationHero) == want;
            }
            catch (System.Exception ex)
            {
                // This runs on every conversation with every hero in the game.
                // An exception here would take the dialogue down with it, so it
                // fails to "no option offered" like every other entry point in
                // this mod fails to doing nothing.
                ModLog.Error("prisoner dialogue condition failed: "
                             + ex.GetType().Name + " " + ex.Message);
                return false;
            }
        }

        private static bool CanStripCore()
        {
            if (!Settings.EnableCaptureLoss) return false;

            Hero hero = Hero.OneToOneConversationHero;
            if (hero == null || hero == Hero.MainHero) return false;
            if (hero.BattleEquipment == null) return false;

            if (!hero.IsPrisoner) return false;
            if (!HeldByPlayer(hero)) return false;

            // Same guard the automatic path uses: never leave a hero naked that
            // nothing in this mod will re-equip. See PlunderService.
            if (!PlunderService.CanBeStripped(hero)) return false;

            return PlunderService.HasAnythingToTake(hero);
        }

        /// <summary>
        /// Whether this prisoner is the player's to take from: travelling with
        /// him, or locked in a keep his clan holds.
        ///
        /// Both, because they are the same thing to everyone but the code. A
        /// lord in your dungeon is your prisoner in every sense the fiction
        /// cares about, and offering the option for one and not the other would
        /// be an accident of which roster he happens to sit in.
        /// </summary>
        private static bool HeldByPlayer(Hero hero)
        {
            PartyBase holder = hero.PartyBelongedToAsPrisoner;
            if (holder == null) return false;
            if (holder == PartyBase.MainParty) return true;

            Settlement settlement = holder.Settlement;
            return settlement != null && settlement.OwnerClan == Clan.PlayerClan;
        }

        private static void Strip()
        {
            try
            {
                StripCore();
            }
            catch (System.Exception ex)
            {
                ModLog.Error("prisoner dialogue failed: " + ex.GetType().Name + " " + ex.Message);
            }
        }

        private static void StripCore()
        {
            Hero hero = Hero.OneToOneConversationHero;
            if (hero == null) return;

            // He must still be somebody's prisoner for this to mean anything.
            PartyBase holder = hero.PartyBelongedToAsPrisoner;
            if (holder == null) return;

            // Into your own baggage, not into whatever is holding him. You are
            // standing in front of the man, so your party is right there -- and
            // the alternative is worse than it sounds: a prisoner in one of your
            // towns is held by that town's party, whose ItemRoster is the
            // market stock. Taking his gear would have put it on sale in your
            // own shop, for you to buy back. See PlunderService.SpoilsFor.
            PartyBase spoils = PartyBase.MainParty != null ? PartyBase.MainParty : holder;

            int value;
            int taken = PlunderService.Take(spoils, hero, out value);
            if (taken == 0) return;

            // Owed, and collecting. Not a robbery of his own doing but the
            // answer to one done to his house, so there is no standing lost,
            // no mark on his name and no oath sworn against him -- only one
            // oath struck off, exactly as when an AI lord takes the same
            // vengeance. Read before anything moves, because the charge below
            // moves the very relation that decides whether a man is a friend.
            Manner manner = MannerFor(hero);

            if (manner == Manner.Vengeance)
            {
                bool struck = VengeanceOaths.Fulfil(Hero.MainHero, hero);
                RobberyTally.Avenged();

                Hero.MainHero.AddSkillXp(DefaultSkills.Roguery, PlunderService.RogueryXpFor(value));

                ModLog.Info("PLUNDER by player prisoner=" + hero.Name
                            + " prisonerHonor=" + hero.GetTraitLevel(DefaultTraits.Honor)
                            + " pieces=" + taken + " worth=" + value
                            + " motive=vengeance oathStruck=" + struck
                            + " roguery=" + PlunderService.RogueryXpFor(value));
                return;
            }

            // Otherwise chosen, and therefore paid for, and always as his own
            // doing: nobody drew for him, so there is no telling his character
            // from the prisoner's name the way a draw tells an AI lord's
            // (PlunderRules.Motive).
            //
            // The same standing an AI lord's house loses for the same act, with
            // the house he robbed, and beside it the one thing only the player
            // has to lose, his name. It is the house that hears of it, not his
            // relatives: an earlier comment here said the cost "spread to his
            // relatives" on the strength of an argument to
            // ChangeRelationAction.ApplyPlayerRelation that the game accepts
            // and ignores. It reaches them because a standing is kept between
            // the heads of two houses and everybody under them shares it.
            //
            // Half of it when the man had it coming, which is the game's own
            // arithmetic for the same situation and not a courtesy invented
            // here -- an execution costs half against a dishonourable victim.
            bool reprisal = manner == Manner.Reprisal;

            int cost = RobberyReckoning.Charge(Hero.MainHero, hero, reprisal);
            int honour = RobberyReckoning.ChargeName(reprisal);
            RobberyTally.Offence();

            // And the man he robbed swears vengeance on him for it, as he
            // would on anybody. From here his house is owed one robbery.
            bool sworn = VengeanceOaths.Swear(hero, Hero.MainHero);

            Hero.MainHero.AddSkillXp(DefaultSkills.Roguery, PlunderService.RogueryXpFor(value));

            ModLog.Info("PLUNDER by player prisoner=" + hero.Name
                        + " prisonerHonor=" + hero.GetTraitLevel(DefaultTraits.Honor)
                        + " pieces=" + taken + " worth=" + value
                        + " motive=character reprisal=" + reprisal
                        + " standing=" + cost
                        + " oathSworn=" + sworn
                        + " honour=" + honour
                        + " roguery=" + PlunderService.RogueryXpFor(value));
        }

    }
}
