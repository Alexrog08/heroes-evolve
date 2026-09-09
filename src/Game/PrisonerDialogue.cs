using HeroLoadoutFixer.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace HeroLoadoutFixer
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
        /// What it costs you with the man himself. Spread to his relatives, as
        /// robbing a lord is the sort of thing a house remembers.
        /// </summary>
        public const int PlayerRelationCost = -12;

        public static void Register(CampaignGameStarter starter)
        {
            if (starter == null) return;

            starter.AddPlayerLine("hlf_strip_prisoner",
                                  "hero_main_options",
                                  "hlf_strip_prisoner_reply",
                                  "{=hlf_strip}Hand over your arms and armour.",
                                  CanStripPlainly, null, 100, null);

            starter.AddDialogLine("hlf_strip_prisoner_reply",
                                  "hlf_strip_prisoner_reply",
                                  "close_window",
                                  "{=hlf_strip_reply}You would take the arms off a beaten man? "
                                  + "This dishonours you. Take them, then. My clan will hear of it, "
                                  + "and so will yours.",
                                  null, Strip, 100, null);

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
            starter.AddPlayerLine("hlf_strip_prisoner_reprisal",
                                  "hero_main_options",
                                  "hlf_strip_prisoner_reprisal_reply",
                                  "{=hlf_strip_reprisal}There is no honour in you for me to offend. "
                                  + "Hand over your arms and armour.",
                                  CanStripInReprisal, null, 100, null);

            // And the third man, who is neither a stranger nor a scoundrel.
            // Robbing a friend is not an outrage and not a reckoning; it is a
            // betrayal, and the only one of the three where the prisoner does
            // not argue. He has nothing to argue about -- he is not surprised
            // that it can be done to him, only by whom. That lands harder than
            // either of the others, and it costs the ordinary price: a friend
            // is never a reprisal, whatever his reputation elsewhere.
            starter.AddPlayerLine("hlf_strip_prisoner_friend",
                                  "hero_main_options",
                                  "hlf_strip_prisoner_friend_reply",
                                  "{=hlf_strip_friend}Do not take this personally. "
                                  + "Hand over your arms and armour.",
                                  CanStripAFriend, null, 100, null);

            starter.AddDialogLine("hlf_strip_prisoner_friend_reply",
                                  "hlf_strip_prisoner_friend_reply",
                                  "close_window",
                                  "{=hlf_strip_friend_reply}I expected this from anyone but you. "
                                  + "Take them, then. I have nothing else to say to you.",
                                  null, Strip, 100, null);

            starter.AddDialogLine("hlf_strip_prisoner_reprisal_reply",
                                  "hlf_strip_prisoner_reprisal_reply",
                                  "close_window",
                                  "{=hlf_strip_reprisal_reply}Speak to me of honour when you have "
                                  + "finished robbing me. Take it, then, take all of it -- I will have "
                                  + "better within the month, and the next man I strip to his shirt "
                                  + "may well share your name.",
                                  null, Strip, 100, null);
        }

        /// <summary>
        /// Which of the three ways this robbery can be asked for.
        ///
        /// A stranger, a scoundrel, or a friend -- and they are tested in that
        /// order of precedence rather than of decency, because friendship
        /// outranks reputation. A man who is Devious and also at your side is
        /// robbed as a friend and charged the full price; being crooked is not
        /// the same as being crooked with you.
        /// </summary>
        private enum Manner
        {
            Plain,
            Reprisal,
            Friend,
        }

        /// <summary>
        /// How this particular prisoner is to be asked, read off him now.
        ///
        /// Friendship first. The reprisal discount exists because a man had it
        /// coming, and a friend never has it coming, however poor his name
        /// elsewhere -- PlunderRules already holds that friendship restrains a
        /// robbery, FriendshipShield being the strongest term in Circumstance.
        /// </summary>
        private static Manner MannerFor(Hero hero)
        {
            if (hero == null) return Manner.Plain;
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

            // Chosen, and therefore paid for. This is the one path where the
            // player's own standing moves, and it is right that it does: the
            // objection was ever only to consequences for decisions he did not
            // make. His relatives hear about it too.
            //
            // Half of it when the man had it coming, which is the game's own
            // arithmetic for the same situation and not a courtesy invented
            // here -- an execution costs half against a dishonourable victim.
            bool reprisal = MannerFor(hero) == Manner.Reprisal;

            ChangeRelationAction.ApplyPlayerRelation(
                hero, PlunderRules.AfterReprisal(PlayerRelationCost, reprisal), true, true);
            TraitLevelingHelper.OnHostileAction(
                PlunderRules.AfterReprisal(PlunderService.HostileActionXp, reprisal));
            Hero.MainHero.AddSkillXp(DefaultSkills.Roguery, PlunderService.RogueryXpFor(value));

            ModLog.Info("PLUNDER by player prisoner=" + hero.Name
                        + " prisonerHonor=" + hero.GetTraitLevel(DefaultTraits.Honor)
                        + " pieces=" + taken + " worth=" + value
                        + " reprisal=" + reprisal
                        + " roguery=" + PlunderService.RogueryXpFor(value));
        }

    }
}
