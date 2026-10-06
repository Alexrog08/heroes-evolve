using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// A captor going through his prisoner's kit.
    ///
    /// The gear is moved, never destroyed: it lands in the captor's own
    /// inventory, so beating him gets it back. That is the same rule the
    /// purchase engine keeps about gold, and it is what makes this a reversal
    /// rather than a punishment.
    ///
    /// Whose decision it is matters as much as the dice. Every captor here is a
    /// person -- the lord leading the party, or the lord who owns the castle --
    /// and his house wears the consequence. A robbery that was his own doing
    /// costs it standing with the house he robbed (RobberyReckoning), and the
    /// prisoner swears vengeance for it (VengeanceOaths). A house that has
    /// sworn takes it back when it next holds one of theirs, and that robbery
    /// costs nothing and strikes the oath off (PlunderRules.Motive). The
    /// player is the one exception in both directions. He is never made to
    /// rob anybody, because nothing should be deciding that for him, and his
    /// own standing never falls for having been robbed, because being robbed
    /// is not an act of his. Relations in this game are the player's to earn.
    /// </summary>
    public static class PlunderService
    {
        /// <summary>
        /// Denars of loot per point of Roguery experience.
        ///
        /// Counted per piece at first, which was worthless and is the same
        /// mistake this project has made before. The game's own curve, decoded
        /// from InitializeXpRequiredForSkillLevel, charges 30 + 10L + L(L+1)/2
        /// to buy the point at level L: 605 at Roguery 25, 1,805 at 50, 6,080
        /// at 100. A flat 25 per piece made a full strip 12% of one point.
        ///
        /// Scaling on what was taken is right for more than arithmetic. A thief
        /// learns from the score, not from the number of buckles he undid, and
        /// it self-scales with the campaign: stripping a pauper teaches nothing,
        /// stripping a king in full harness is a career moment. A well-equipped
        /// lord should be worth about one point at Roguery 50 and a king's kit
        /// around three.
        ///
        /// That intent was right and the divisor was not. It was set at 20 on
        /// an estimate that a lord's kit came to some thirty-odd thousand
        /// denars; the first live campaign measured two real robberies at
        /// 522,184 and 295,896, off by a factor of ten. At 20 the smaller of
        /// those two paid 14,794 experience, which carries a captor from
        /// Roguery 50 to 57 in one afternoon, or from 25 to 41. The lesson is
        /// the old one this project keeps relearning: measure the input before
        /// calibrating the constant.
        ///
        /// Two hundred restores what the paragraph above always meant. A
        /// 400,000-denar harness is 2,000 experience, which is 1.1 points at
        /// Roguery 50 against the game's own curve of 30 + 10L + L(L+1)/2.
        /// </summary>
        public const int RogueryDenarsPerXp = 200;

        /// <summary>
        /// Strips the prisoner if this captor would. Returns how many pieces
        /// changed hands, zero being much the commonest answer.
        /// </summary>
        public static int TryPlunder(PartyBase captorParty, Hero prisoner)
        {
            if (!Settings.EnableCaptureLoss) return 0;
            if (captorParty == null || prisoner == null || prisoner.BattleEquipment == null) return 0;

            if (!CanBeStripped(prisoner)) return 0;

            // A keep decides nothing, and it has no character to decide with.
            // When a town or castle takes prisoners directly -- war declared,
            // a clan changing kingdom, a settlement changing hands, all of them
            // PrisonerCaptureCampaignBehavior.HandleSettlementHeroes -- the men
            // go into the cells dressed as they are. Robbing them there meant
            // reading the traits of an owner who was three hundred miles away,
            // which is not a decision anyone made. Whoever holds the keys robs
            // them, or does not, when he next walks in. See PlunderPrisonersOf.
            if (captorParty.IsSettlement) return 0;

            bool bandit = IsBanditParty(captorParty);
            Hero captor = CaptorOf(captorParty);

            // Nobody to decide, and not a bandit either: a leaderless garrison
            // has no opinion about a prisoner's boots.
            if (!bandit && captor == null) return 0;

            // The player is never rolled for. He robs by asking, in
            // conversation -- see PrisonerDialogue.
            if (captor != null && captor == Hero.MainHero) return 0;
            if (captor == prisoner) return 0;

            // Whether his house holds an oath of vengeance against the
            // prisoner's. A band with no name holds none.
            bool owed = captor != null && VengeanceOaths.Owed(captor, prisoner);

            float ownDoing;
            float chance = ChanceFor(bandit, captor, prisoner, out ownDoing);
            float vengeance = owed ? VengeanceFor(captor, prisoner) : 0f;
            if (chance <= 0f && vengeance <= 0f) return 0;

            // One draw: whether he robs at all, and what made him. See
            // PlunderRules.Motive.
            PlunderRules.Motive motive = PlunderRules.Judge(MBRandom.RandomFloat, ownDoing, chance, vengeance);
            if (motive == PlunderRules.Motive.None) return 0;

            // Nobody to hand it to, nobody robs him. See SpoilsFor.
            PartyBase spoils = SpoilsFor(captorParty, captor);
            if (spoils == null) return 0;

            int value;
            int taken = Take(spoils, prisoner, out value);
            if (taken == 0) return 0;

            // A person if there is one, the band itself if there is not.
            // PartyBase.Name carries the mobile party's own name here --
            // settlements took the early return above -- so a looter gang is
            // named as readily as a lord.
            Announce(captorParty.MapFaction,
                     captor != null ? NameOf(captor) : captorParty.Name, prisoner, false,
                     motive == PlunderRules.Motive.Vengeance);

            // Read before the charge moves it. This is the standing the draw
            // was judged on, which is the one a log reader needs to check the
            // chance beside it.
            int relation = captor != null ? captor.GetRelation(prisoner) : 0;

            // What it cost him, or which oath it answered. His own reckoning
            // when the captor is the player is in PrisonerDialogue, where he
            // chose it; he cannot reach this line, the guard above returns
            // before it.
            string reckoning = Reckon(captor, prisoner, motive);

            if (captor != null)
            {
                Reward(captor, value);

                // And then he tries it on. Taking a man's harness and never
                // looking at it would be odd; the market's own rules decide
                // whether any of it suits him, which usually it does not.
                //
                // Safe to hand the whole roster over because SpoilsFor only ever
                // returns a baggage train. It used to be able to return a
                // settlement, and the fitting -- which reads everything it is
                // given as "the pile he just took" -- then dressed the owner out
                // of his own town's shelves, eleven pieces at a time.
                LootFitting.Equip(captor, spoils.ItemRoster,
                                  Settings.ClanWeight, Settings.SkillWeight, Settings.MinimumTier);
            }

            // Honor on both sides, because it is the trait the whole feature
            // turns on and neither the census nor the game's own UI puts it
            // where a log reader can see it. The captor's is the heaviest term
            // in his chance to rob at all; the prisoner's is what decides
            // whether robbing him back would be a reprisal. Without these two
            // numbers a line like "chance=8%" cannot be checked against the
            // model that produced it.
            ModLog.Info("PLUNDER captor=" + (captor != null ? captor.Name.ToString() : "bandits")
                        + " captorHonor=" + (captor != null
                            ? captor.GetTraitLevel(DefaultTraits.Honor).ToString() : "-")
                        + " prisoner=" + prisoner.Name
                        + " prisonerHonor=" + prisoner.GetTraitLevel(DefaultTraits.Honor)
                        + " relation=" + relation
                        + " owed=" + owed
                        + " chance=" + (int)(chance * 100f) + "%"
                        + " own=" + (int)(ownDoing * 100f) + "%"
                        + " vengeance=" + (int)(vengeance * 100f) + "%"
                        + reckoning
                        + " pieces=" + taken
                        + " worth=" + value);

            return taken;
        }

        /// <summary>
        /// Prices a robbery by what caused it, and says what it did for the
        /// log.
        ///
        /// A band with no name is charged to nobody and sworn against by
        /// nobody: there is no house to send the bill to. Anybody with a name
        /// answers for it, an outlaw chief included -- he robs like a bandit
        /// (PlunderRules.BanditChance) and is hunted for it like a lord.
        ///
        /// Vengeance costs nothing and strikes one oath off
        /// (VengeanceOaths.Fulfil). An honourable man stripping a known thief
        /// costs nothing and is sworn against by nobody. A robbery of
        /// character costs his house standing with the house he robbed
        /// (RobberyReckoning.Charge), and the prisoner swears vengeance for it
        /// (VengeanceOaths.Swear).
        /// </summary>
        private static string Reckon(Hero captor, Hero prisoner, PlunderRules.Motive motive)
        {
            if (captor == null)
            {
                RobberyTally.Bandit();
                return " motive=bandit";
            }

            if (motive == PlunderRules.Motive.Vengeance)
            {
                bool struck = VengeanceOaths.Fulfil(captor, prisoner);

                RobberyTally.Avenged();
                return " motive=vengeance oathStruck=" + struck;
            }

            if (motive == PlunderRules.Motive.Justice)
            {
                RobberyTally.Justified();
                return " motive=justice";
            }

            int cost = RobberyReckoning.Charge(captor, prisoner, HadItComing(captor, prisoner));
            bool sworn = VengeanceOaths.Swear(prisoner, captor);

            RobberyTally.Offence();
            return " motive=character standing=" + cost + " oathSworn=" + sworn;
        }

        /// <summary>
        /// Whether robbing this man is cheaper for what he is: a prisoner
        /// without honour, who is not the robber's friend. The same two tests,
        /// in the same order, as the three ways the player can ask
        /// (PrisonerDialogue.MannerFor).
        /// </summary>
        private static bool HadItComing(Hero robber, Hero victim)
        {
            if (!PlunderRules.IsReprisal(victim.GetTraitLevel(DefaultTraits.Honor))) return false;
            return !victim.IsFriend(robber);
        }

        /// <summary>
        /// Whose baggage a stripped prisoner's kit goes into, or null when
        /// there is no such baggage and therefore no robbery.
        ///
        /// The man who decided it receives it, into an inventory somebody owns.
        /// Bannerlord keeps item rosters on parties and not on people, so a
        /// captor with nowhere to put it does not take it: somebody receives
        /// the gear, or the gear is not taken.
        ///
        /// No settlement reaches here. It used to, and Settlement.ItemRoster is
        /// literally Settlement.Party.ItemRoster -- for a town its market stock
        /// -- so a keep full of prisoners emptied their harnesses onto the
        /// shelves for nothing. That whole path now goes through
        /// PlunderPrisonersOf instead, where a man with a party does the
        /// robbing and has somewhere to put it.
        ///
        /// A bandit band has no leader hero, so the captor is null, but its
        /// baggage train is a real inventory and is where the loot belongs.
        /// </summary>
        private static PartyBase SpoilsFor(PartyBase captorParty, Hero captor)
        {
            if (captor != null && captor.PartyBelongedTo != null)
            {
                return captor.PartyBelongedTo.Party;
            }

            if (captorParty != null && captorParty.IsMobile) return captorParty;

            return null;
        }

        /// <summary>
        /// A lord walks into a keep he holds and looks over the men in its
        /// cells.
        ///
        /// This is where a settlement's prisoners are robbed, and it is
        /// deliberately not the moment of capture. A town has no character to
        /// roll against, and its owner is usually nowhere near it; deciding
        /// then meant a man was stripped by a lord who was besieging somewhere
        /// else. Here the lord is standing in his own hall, the prisoners are
        /// downstairs, and the decision is his to make face to face -- the same
        /// standing that lets him free them in vanilla, which is the test the
        /// player's own conversation uses.
        ///
        /// One prisoner at a time and each judged on his own, so a lord may
        /// strip the man he despises and leave the one he respects. The die is
        /// PlunderRules.Draw rather than a roll, so walking in and out again
        /// re-asks the question instead of re-rolling it -- see there for why
        /// that is a rule rather than a saving.
        ///
        /// The whole haul is tried on once at the end. Doing it per prisoner
        /// would have him re-dressing between cells.
        /// </summary>
        public static int PlunderPrisonersOf(Settlement settlement, Hero visitor)
        {
            if (!Settings.EnableCaptureLoss) return 0;
            if (settlement == null || visitor == null) return 0;

            // He robs by asking, in conversation. See PrisonerDialogue.
            if (visitor == Hero.MainHero) return 0;

            // The man with the keys, and nobody else in his household.
            if (settlement.Owner != visitor) return 0;

            MobileParty party = visitor.PartyBelongedTo;
            if (party == null || party.Party == null) return 0;

            PartyBase prison = settlement.Party;
            if (prison == null || prison.PrisonRoster == null) return 0;

            // Collected before anything is taken: robbing a man edits his
            // equipment, and the roster is walked to find him.
            List<Hero> prisoners = new List<Hero>();
            for (int i = 0; i < prison.PrisonRoster.Count; i++)
            {
                CharacterObject character = prison.PrisonRoster.GetCharacterAtIndex(i);
                if (character == null || !character.IsHero) continue;
                if (character.HeroObject != null) prisoners.Add(character.HeroObject);
            }

            int robbed = 0;
            int worth = 0;

            for (int i = 0; i < prisoners.Count; i++)
            {
                Hero prisoner = prisoners[i];
                if (prisoner == visitor) continue;
                if (!CanBeStripped(prisoner)) continue;
                if (!HasAnythingToTake(prisoner)) continue;

                bool owed = VengeanceOaths.Owed(visitor, prisoner);

                float ownDoing;
                float chance = ChanceFor(false, visitor, prisoner, out ownDoing);
                float vengeance = owed ? VengeanceFor(visitor, prisoner) : 0f;
                if (chance <= 0f && vengeance <= 0f) continue;
                // Seeded on this spell in the cells, not on the two men alone:
                // Hero.CaptivityStartTime is written by TakePrisonerAction on
                // every capture, so being freed and taken again is a fresh
                // question rather than the same verdict for ever.
                //
                // Hours because NumTicks is not public and ToHours is, and it
                // is an absolute campaign time rather than one measured against
                // now -- ElapsedHoursUntilNow would change the answer every
                // hour he sat in the cell, which is the one thing this must not
                // do. Two captivities of the same pair are always many days
                // apart, so the coarseness costs nothing.
                long episode = (long)prisoner.CaptivityStartTime.ToHours;

                // The same draw read the same way as on the field. It does not
                // move while he sits in the cell, but the bar does: a prisoner
                // whose house robs the keeper's while he is held has given the
                // keeper an oath to collect, and the keeper's next visit may go
                // differently.
                PlunderRules.Motive motive = PlunderRules.Judge(
                    PlunderRules.Draw(visitor.StringId, prisoner.StringId, episode),
                    ownDoing, chance, vengeance);
                if (motive == PlunderRules.Motive.None) continue;

                int value;
                int taken = Take(party.Party, prisoner, out value);
                if (taken == 0) continue;

                robbed += taken;
                worth += value;

                int relation = visitor.GetRelation(prisoner);
                string reckoning = Reckon(visitor, prisoner, motive);

                ModLog.Info("PLUNDER captor=" + visitor.Name
                            + " captorHonor=" + visitor.GetTraitLevel(DefaultTraits.Honor)
                            + " prisoner=" + prisoner.Name
                            + " prisonerHonor=" + prisoner.GetTraitLevel(DefaultTraits.Honor)
                            + " relation=" + relation
                            + " owed=" + owed
                            + " chance=" + (int)(chance * 100f) + "%"
                            + " own=" + (int)(ownDoing * 100f) + "%"
                            + " vengeance=" + (int)(vengeance * 100f) + "%"
                            + reckoning
                            + " pieces=" + taken
                            + " worth=" + value
                            + " at=" + settlement.Name);

                // Same news, other route. A man robbed in a cell is robbed
                // exactly as a man robbed on the field, so the player hears
                // about it on the same terms -- this one stands alone rather
                // than under a capture notice, because his capture was days
                // ago, but withholding it would mean the message depended on
                // where the robbery happened instead of whom it happened to.
                Announce(visitor.MapFaction, NameOf(visitor), prisoner, false,
                         motive == PlunderRules.Motive.Vengeance);
            }

            if (robbed == 0) return 0;

            Reward(visitor, worth);
            LootFitting.Equip(visitor, party.Party.ItemRoster,
                              Settings.ClanWeight, Settings.SkillWeight, Settings.MinimumTier);

            return robbed;
        }

        /// <summary>
        /// Tells the player when his own banner is on either end of a robbery,
        /// and says nothing otherwise.
        ///
        /// After a capture it goes directly under the game's own "X has been
        /// taken prisoner by Y", which is where the news belongs -- but not
        /// for the reason an earlier version of this comment gave. It claimed
        /// the ordering was structural because listeners run in the order they
        /// registered. They run in the reverse of it: MbEvent prepends, so the
        /// last listener registered is the first to run, and a module that
        /// declares its dependencies honestly always registers last. This
        /// mod's line was printing ABOVE the capture that caused it.
        ///
        /// Both are listeners on the same HeroPrisonerTaken, raised once at
        /// the end of TakePrisonerAction.ApplyInternal, with the vanilla entry
        /// built by DefaultLogsCampaignBehavior.OnPrisonerTaken. Nothing on
        /// this side of the event can get behind that, so the line waits a
        /// frame instead. See PendingNotices.
        ///
        /// Both ends, because the banner is what decides and a banner does not
        /// only lose. One of your lords going through a prisoner's kit is news
        /// about your own people's conduct in exactly the way one of your own
        /// being stripped is news about their treatment, and telling you only
        /// the half where you are the victim would make the rule about
        /// grievance rather than about who is yours.
        ///
        /// Narrower than vanilla, deliberately, and the first version of this
        /// comment claimed the opposite. Read off the assembly:
        /// TakePrisonerLogEntry.IsVisibleNotification returns a hardcoded true
        /// and GetNotificationText never mentions Hero.MainHero, PlayerClan or
        /// any kingdom -- its six wordings differ only in whether a faction is
        /// named. The game announces every capture on the map to everybody.
        ///
        /// Copying that is not a virtue. A capture takes a lord off the board
        /// and is worth knowing about wherever it happens; a stranger losing
        /// his boots is not something the player can act on, and robberies are
        /// frequent enough that announcing all of them would bury the ones that
        /// matter. His own banner is the line worth drawing even though vanilla
        /// does not draw it.
        ///
        /// Counted rather than guessed, off the census: 106 robberies, 29 of
        /// them by bandits, so a lord is the captor 73% of the time. A kingdom
        /// of fifty is an eighth of Calradia's lords, which puts it at about
        /// six messages a year from the robbing end on top of the nine it
        /// already saw from the robbed end. One every three weeks or so.
        ///
        /// It decides who is told, not what happens: every lord in Calradia is
        /// robbed by the same rules whether or not anybody reads about it.
        ///
        /// The message never says what was taken. A player who wants the detail
        /// opens the hero and looks, and a list of eleven items in a floating
        /// notification is not read by anybody.
        ///
        /// One wording, shaped like the line it sits under. There were two at
        /// first -- an active one naming a lord, and a vague one for bandits,
        /// who have no hero to name -- which meant two sentences to read for
        /// one event and a robber who sometimes went unnamed. The passive form
        /// covers both, because a party has a name even when nobody in it
        /// does: "by Monchug" and "by Forest Bandits" are both grammatical,
        /// where "Forest Bandits has stripped" is not. It is also the shape
        /// TaleWorlds uses one line above -- "X has been taken prisoner by Y"
        /// -- so the pair reads as one piece of news rather than two.
        /// </summary>
        /// <summary>
        /// Whether he let himself out, which is the difference between the two
        /// wordings.
        ///
        /// A man who slips away takes what he can carry out of the baggage on
        /// his way past it. A man who is handed back -- ransomed, freed in a
        /// peace, let go, compensated for -- is fitted out by the people
        /// releasing him, because sending a naked lord onto the road is not
        /// something anyone does deliberately. Same gear either way; only the
        /// story of how he came by it changes.
        ///
        /// ReleasedAfterBattle sits with escape rather than with release: his
        /// captors have just been beaten, and nobody in that camp is handing
        /// out boots.
        /// </summary>
        private static bool Escaped(EndCaptivityDetail detail)
        {
            return detail == EndCaptivityDetail.ReleasedAfterEscape
                || detail == EndCaptivityDetail.ReleasedAfterBattle;
        }

        /// <summary>
        /// How this man is named in a notice: with his people behind him when
        /// he has any worth naming.
        ///
        /// Vanilla's own shape, read off TakePrisonerLogEntry.
        /// GetNotificationText -- "{PRISONER_LORD.LINK} of the
        /// {PRISONER_FACTION_LINK} has been taken prisoner by ..." -- down to
        /// the test that decides who gets placed. It reads Clan.IsMinorFaction
        /// and leaves those men bare, which is right: a mercenary company or a
        /// bandit clan is not a people to be "of", and the notice reads worse
        /// for pretending otherwise.
        ///
        /// EncyclopediaLinkWithName rather than Name, again as vanilla does,
        /// so the faction is the same clickable name the rest of the log
        /// carries rather than a flat word beside it.
        /// </summary>
        internal static TextObject NameOf(Hero hero)
        {
            if (hero == null) return null;
            if (hero.Clan == null || hero.Clan.IsMinorFaction || hero.MapFaction == null)
            {
                return hero.Name;
            }

            TextObject placed = new TextObject("{=hev_of_faction}{NAME} of the {FACTION}");
            placed.SetTextVariable("NAME", hero.Name);
            placed.SetTextVariable("FACTION", hero.MapFaction.EncyclopediaLinkWithName);
            return placed;
        }

        internal static void Announce(IFaction captorFaction, TextObject captor,
                                      Hero prisoner, bool force, bool inAnswer = false)
        {
            if (prisoner == null || captor == null || Hero.MainHero == null) return;

            // Everyone under the player's own banner, on either end of it.
            // MapFaction settles what that means without a special case: his
            // clan while he is independent, his kingdom once he has sworn to
            // one or founded it.
            //
            // His clan alone was the first cut and it was too narrow to be a
            // feature. Worked out from the census -- 83 robberies among 405
            // lords in seven months at full strength, so about 41 at the
            // default rate -- a clan of eight heroes sees this message 1.4
            // times a year. A kingdom of fifty sees it nine times: often enough
            // to be worth having, rare enough that it never buries anything.
            // The whole map would be 71, which is the register of vanilla's
            // capture spam and the thing worth not copying.
            // A banner nobody holds matches nobody. Without this, a player
            // whose MapFaction is momentarily null would be told about every
            // factionless hero on the map, because null == null passes the
            // test below twice over.
            IFaction mine = Hero.MainHero.MapFaction;
            if (!force && mine == null) return;
            if (!force && prisoner.MapFaction != mine && captorFaction != mine) return;

            try
            {
                // Two wordings, and the second is the only place the player is
                // told why. Vengeance taken on his house for a robbery it did
                // reads differently from a robbery that simply happened to it,
                // and it is also the line that tells him one oath against his
                // house has been answered. Said of his own people too, when
                // they are the ones collecting.
                TextObject line = inAnswer
                    ? new TextObject(
                        "{=hev_robbed_reprisal}{VICTIM} has been stripped of arms and armour by {CAPTOR} in reprisal.")
                    : new TextObject(
                        "{=hev_robbed}{VICTIM} has been stripped of arms and armour by {CAPTOR}.");

                line.SetTextVariable("VICTIM", NameOf(prisoner));
                line.SetTextVariable("CAPTOR", captor);

                // Held one frame, so it prints under the game's own capture
                // line instead of over it. See PendingNotices -- listeners run
                // newest-registered first, which put this mod ahead of the
                // news it was meant to follow.
                //
                // Red is the colour the game uses for a loss rather than a
                // warning, so the pair reads as one run of messages.
                PendingNotices.Queue(line.ToString(), Colors.Red);
            }
            catch
            {
                // A message that cannot be shown must not cost the robbery.
            }
        }

        /// <summary>
        /// Tells a released hero what he walked out in, if he was robbed on
        /// the way in.
        ///
        /// Only the player is told, and that is not an exception to any rule
        /// -- the robbery itself ran on him exactly as it runs on everybody,
        /// and every other hero's robbery was announced the moment it
        /// happened. This is the same message repeated for the one man whose
        /// screen was gone when it was first sent. Announcing an AI lord's
        /// release as well would be saying a thing twice to somebody who read
        /// it the first time.
        ///
        /// The message feed, and nothing else. Two earlier attempts were
        /// wrong for the same reason: a release deserves one line, not a
        /// window of its own.
        ///
        /// A floating banner was the first, on the belief it would land under
        /// the game's own release line. There is no such line. The
        /// AddQuickInformation inside EndCaptivityAction.ApplyInternal sits
        /// behind two guards -- a facilitator, and a detail of Death -- so it
        /// announces a prisoner dying in the cells and nothing else. Ours
        /// would have arrived alone, as a whole popup for one sentence, and
        /// arrived while the captivity menu was closing, which is the one
        /// moment a transient notification is easiest to miss.
        ///
        /// The captivity menu itself would be the natural host and cannot be
        /// reached: GameMenu exposes GetText and no setter, and the body text
        /// is fixed when AddGameMenu registers it. Nothing short of Harmony
        /// puts a line inside it.
        ///
        /// So it goes where his people's robberies already go. One red line
        /// in a feed he is already reading, still there when he looks, and no
        /// new window at all.
        /// </summary>
        public static bool AnnounceRelease(Hero prisoner, EndCaptivityDetail detail)
        {
            if (prisoner == null) return false;

            // Cleared for everybody, read by one. Only the player is told, but
            // every hero on the map is robbed by the same rules, and an entry
            // that is never consumed is never released either: asking about
            // the player first left one behind for every AI lord robbed in the
            // session. Consuming here keeps the ledger to the heroes currently
            // held and robbed, which is what its own comment promises.
            bool robbed = CaptivityRobberies.Consume(prisoner);

            if (prisoner != Hero.MainHero) return false;

            // A dead man is not told what he is wearing. His entry is gone
            // either way, by the line above.
            if (!robbed || detail == EndCaptivityDetail.Death) return false;

            try
            {
                TextObject line = Escaped(detail)
                    ? new TextObject("{=hev_released_scavenged}You scavenged plain gear from your captors' baggage on the way out, of the same kinds you carried.")
                    : new TextObject("{=hev_released_given}Your captors sent you off with plain gear for the road, of the same kinds you carried.");

                // Grey, not the red the robberies use. Nothing is being
                // lost at this moment -- the loss happened days ago and was
                // reported then. This is the man telling you how he got home.
                InformationManager.DisplayMessage(
                    new InformationMessage(line.ToString(), Colors.Gray));
                return true;
            }
            catch
            {
                // A message that cannot be shown must not cost the release.
                return false;
            }
        }

        /// <summary>
        /// Whether this prisoner is someone we may leave with nothing.
        ///
        /// Only heroes the repair will look after, and the player, who can
        /// dress himself. Anyone else -- a wandering troubadour, a notable, a
        /// minor-faction chief -- falls outside what HeroFilter lets the repair
        /// touch, and the purchase engine only ever improves a slot that is
        /// already filled. Strip one of those and he is naked for the rest of
        /// the campaign, with nothing in the mod able to help him. Taking a
        /// man's gear should cost him a war, not his existence.
        ///
        /// The player is the exception to that exception, for the first hour
        /// only. "He can dress himself" is true of every campaign except while
        /// the tutorial refuses him entry to the towns -- and it sends him to
        /// fight Radagos, which he can lose, to bandits who rob half their
        /// prisoners. That was a coin flip on leaving a new campaign with
        /// nothing and nowhere to fix it. Until the map opens he is the
        /// troubadour: see TutorialLock.
        /// </summary>
        public static bool CanBeStripped(Hero prisoner)
        {
            if (prisoner == null) return false;
            if (prisoner == Hero.MainHero) return !TutorialLock.LocksTheMap();
            return HeroFilter.IsEligible(prisoner);
        }

        /// <summary>
        /// The chance this captor robs this prisoner, with the game's own
        /// answers about the two men fed into the pure rules, and the part of
        /// it that is the captor's own doing: his character against this man,
        /// before the prisoner's name adds anything to it.
        /// </summary>
        public static float ChanceFor(bool bandit, Hero captor, Hero prisoner, out float ownDoing)
        {
            ownDoing = 0f;

            if (bandit) return PlunderRules.Chance(true, 0, 0, 0, 0, 0, 0,
                                                   PlunderRules.Kinship.None, 0,
                                                   Settings.RobberyMultiplier(), out ownDoing);
            if (captor == null || prisoner == null) return 0f;

            return PlunderRules.Chance(false,
                                       captor.GetTraitLevel(DefaultTraits.Honor),
                                       captor.GetTraitLevel(DefaultTraits.Mercy),
                                       captor.GetTraitLevel(DefaultTraits.Generosity),
                                       captor.GetTraitLevel(DefaultTraits.Calculating),
                                       captor.GetSkillValue(DefaultSkills.Roguery),
                                       captor.GetRelation(prisoner),
                                       KinshipBetween(captor, prisoner),
                                       prisoner.GetTraitLevel(DefaultTraits.Honor),
                                       Settings.RobberyMultiplier(), out ownDoing);
        }

        /// <summary>
        /// The chance a captor whose house is owed takes it back from this
        /// prisoner. Whether it is owed is the caller's question
        /// (VengeanceOaths.Owed).
        ///
        /// On the campaign's own dial and not on RobberyMultiplier, which
        /// carries the calibration of how often character robs: a debt is not
        /// a matter of character (PlunderRules.VengeanceChance).
        /// </summary>
        public static float VengeanceFor(Hero captor, Hero prisoner)
        {
            if (captor == null || prisoner == null) return 0f;

            return PlunderRules.Vengeance(captor.GetRelation(prisoner),
                                          KinshipBetween(captor, prisoner),
                                          Settings.RobberyRate);
        }

        /// <summary>
        /// Everything the prisoner is wearing, into the captor's inventory.
        ///
        /// Everything, with no exception for the gilded and the unsellable. A
        /// lord will not part with his heirloom, and the market respects that --
        /// but a man robbing him is not asking. The armour then travels: the
        /// captor usually has no use for a cuirass cut for another people and
        /// sells it,
        /// and it turns up on a shelf somewhere for its owner to buy back, or
        /// for whoever walks into that town first. Caladog would never sell his
        /// gilded plate. He can still lose it, and then it is anybody's.
        ///
        /// That is the point of the whole feature: unique gear circulating
        /// because it was taken, rather than sitting in one man's slot forever.
        /// </summary>
        public static int Take(PartyBase captorParty, Hero prisoner, out int value)
        {
            int taken = 0;
            value = 0;

            // Nowhere to put it means nobody takes it, and this guard is the
            // difference between a failed robbery and a destroyed harness.
            // TakeSlot empties the slot before it hands the piece on, so a
            // missing roster would have left the prisoner bare and the gear
            // nowhere at all -- against the one promise this whole feature
            // rests on, that gear is moved and never destroyed.
            ItemRoster loot = captorParty != null ? captorParty.ItemRoster : null;
            if (loot == null || prisoner == null || prisoner.BattleEquipment == null) return 0;

            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                taken += TakeSlot(loot, prisoner, SlotMapping.WeaponSlot(i), ref value);
            }
            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                taken += TakeSlot(loot, prisoner, slot, ref value);
            }
            taken += TakeSlot(loot, prisoner, EquipmentIndex.Horse, ref value);
            taken += TakeSlot(loot, prisoner, EquipmentIndex.HorseHarness, ref value);

            // Every robbery in the mod comes through here -- field, cells and
            // the player's own conversation alike -- so this is the one place
            // that can note it happened. See CaptivityRobberies.
            if (taken > 0) CaptivityRobberies.Record(prisoner);

            return taken;
        }

        private static int TakeSlot(ItemRoster loot, Hero prisoner, EquipmentIndex slot, ref int value)
        {
            EquipmentElement worn = prisoner.BattleEquipment[slot];
            if (!Takeable(worn)) return 0;

            // ItemValue rather than Item.Value: it accounts for the modifier, so
            // a fine sword is worth more to take than a rusty one of the same
            // make, which is the whole idea.
            value += worn.ItemValue;

            prisoner.BattleEquipment[slot] = EquipmentElement.Invalid;
            loot.AddToCounts(worn, 1);

            // And he is dressed again in the same breath, in the cheapest thing
            // of the same kind his people make. Here rather than in the repair
            // because here is the only place the shape is known for certain:
            // the piece that made it is in hand. See Rags.
            Rags.Replace(prisoner, slot, worn.Item);

            return 1;
        }

        /// <summary>
        /// Whether one worn piece may change hands.
        ///
        /// A quest item is not loot. Taking one could strand the quest that put
        /// it there, and the game excludes them from its own looting for the
        /// same reason. Banners need no check: they sit in ExtraWeaponSlot,
        /// which nothing in this mod reaches.
        ///
        /// Shared with HasAnythingToTake so the conversation cannot offer a
        /// demand that Take will then decline -- a lord carrying nothing but a
        /// quest sword used to hear "take them, then" and hand over nothing.
        /// </summary>
        private static bool Takeable(EquipmentElement worn)
        {
            if (worn.Item == null || worn.IsQuestItem) return false;

            // And nothing already at the bottom of the world. See Rags.IsRag:
            // without this a man robbed once is robbed again for the rags the
            // last robbery handed him.
            return !Rags.IsRag(worn.Item);
        }

        /// <summary>
        /// Whether there is anything on him worth asking for. Keeps the
        /// conversation option from appearing over a man already stripped bare.
        /// </summary>
        public static bool HasAnythingToTake(Hero prisoner)
        {
            if (prisoner == null || prisoner.BattleEquipment == null) return false;

            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                if (Takeable(prisoner.BattleEquipment[SlotMapping.WeaponSlot(i)])) return true;
            }
            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                if (Takeable(prisoner.BattleEquipment[slot])) return true;
            }
            return Takeable(prisoner.BattleEquipment[EquipmentIndex.Horse])
                   || Takeable(prisoner.BattleEquipment[EquipmentIndex.HorseHarness]);
        }

        /// <summary>
        /// Practice at theft, worth what the theft was worth. A lord who robs
        /// gets better at it, and being better at it makes him likelier to rob
        /// again -- which closes a loop the rules already opened through
        /// roguery.
        ///
        /// No trait cost here: the game keeps a trait ledger for the player
        /// alone, and the player never reaches this path. His own reckoning is
        /// in PrisonerDialogue, where he chose it.
        /// </summary>
        private static void Reward(Hero captor, int lootValue)
        {
            captor.AddSkillXp(DefaultSkills.Roguery, RogueryXpFor(lootValue));
        }

        /// <summary>Roguery earned for a haul of this value.</summary>
        public static int RogueryXpFor(int lootValue)
        {
            if (lootValue <= 0) return 0;
            return lootValue / RogueryDenarsPerXp;
        }

        /// <summary>
        /// The person answerable for what this party does: the lord leading it,
        /// or the lord who holds the castle. Null for a garrison nobody owns.
        /// </summary>
        private static Hero CaptorOf(PartyBase party)
        {
            if (party.LeaderHero != null) return party.LeaderHero;
            if (party.Owner != null) return party.Owner;
            if (party.Settlement != null) return party.Settlement.Owner;
            return null;
        }

        /// <summary>
        /// Whether this party robs as a bandit does, with no honour to appeal
        /// to. Public because CaravanWatch asks the same question and asked it
        /// differently: it tested MobileParty.IsBandit alone and reported
        /// "bandits=False" for the party of Nal of the Wolfskins, whose clan is
        /// an outlaw clan and who had just been given the bandit branch by this
        /// very method -- a log line contradicting the log line above it. Two
        /// copies of a predicate drift; one does not.
        /// </summary>
        public static bool IsBanditParty(PartyBase party)
        {
            MobileParty mobile = party.MobileParty;
            if (mobile != null && mobile.IsBandit) return true;

            return RobsAsBandit(party.LeaderHero);
        }

        /// <summary>
        /// Whether a party this man leads robs as a bandit does: his clan is
        /// a bandit clan or an outlaw company. Apart from IsBanditParty for
        /// the census, which asks it of lords who may be leading nothing at
        /// the moment it reads them.
        /// </summary>
        public static bool RobsAsBandit(Hero leader)
        {
            Clan clan = leader != null ? leader.Clan : null;
            return clan != null && (clan.IsBanditFaction || clan.IsOutlaw);
        }

        /// <summary>
        /// How close two heroes are by blood or marriage. Only the degrees the
        /// rules care about: the ones you do not rob, and the ones you rob less.
        /// </summary>
        private static PlunderRules.Kinship KinshipBetween(Hero captor, Hero prisoner)
        {
            if (captor.Spouse == prisoner) return PlunderRules.Kinship.Immediate;
            if (captor.Father == prisoner || captor.Mother == prisoner) return PlunderRules.Kinship.Immediate;
            if (prisoner.Father == captor || prisoner.Mother == captor) return PlunderRules.Kinship.Immediate;

            foreach (Hero sibling in captor.Siblings)
            {
                if (sibling == prisoner) return PlunderRules.Kinship.Immediate;
            }

            // Sharing a house is the workable stand-in for the wider family: the
            // game models cousins and in-laws only loosely, and a clansman is
            // the relation a lord would actually feel.
            if (captor.Clan != null && captor.Clan == prisoner.Clan) return PlunderRules.Kinship.Clan;

            return PlunderRules.Kinship.None;
        }
    }
}
