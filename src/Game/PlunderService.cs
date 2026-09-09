using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
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
    /// and he wears the consequence himself: robbing a prisoner costs him
    /// standing with that prisoner, exactly as it should for a thing he chose to
    /// do. The player is the one exception in both directions. He is never made
    /// to rob anybody, because nothing should be deciding that for him, and his
    /// own standing never moves for having been robbed, because being robbed is
    /// not an act of his. Relations in this game are the player's to earn.
    /// </summary>
    public static class PlunderService
    {
        /// <summary>
        /// What robbing a prisoner costs the captor in that prisoner's regard.
        /// </summary>
        public const int RelationCost = -10;

        /// <summary>
        /// What the act does to the robber's character, on the game's own
        /// scale and through the game's own door.
        ///
        /// TraitLevelingHelper.OnHostileAction is what Bannerlord calls when
        /// somebody does something ugly, and it charges Honor and Mercy
        /// together -- which is exactly the pair of marks robbing a disarmed
        /// prisoner leaves. Using it rather than hand-rolling two calls means
        /// the act is logged and weighted the way the game weighs its own.
        ///
        /// Twenty sits just below the game's own figure, which is twenty-five
        /// at full severity: BeHostileAction.ApplyGeneralConsequencesOnPeace
        /// computes -25 times a severity and hands the result to this same
        /// door. No civilian is harmed here, and the offence is chiefly a
        /// breach of the customs of war rather than a cruelty, so a little
        /// under is right. (An earlier version of this comment said thirty,
        /// from memory rather than from the assembly.)
        ///
        /// What that buys, now that the scale has been read: a trait level
        /// costs a thousand experience -- DefaultCharacterDevelopmentModel.
        /// GetTraitLevelForTraitXp steps at 1000 and 4000 either side of zero,
        /// clamped at 6000 -- so fifty robberies move a man's Honor by one,
        /// against forty of the game's own hostile acts. A career of it, not an
        /// afternoon.
        ///
        /// Only the player has traits that move at all: AddTraitXp is hardwired
        /// to Campaign.PlayerTraitDeveloper and Hero.MainHero, and an AI lord's
        /// character is fixed when he is generated. So this fires exactly when
        /// the player's own party robs somebody, which it does only if he has
        /// turned that on himself.
        /// </summary>
        public const int HostileActionXp = -20;

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

            float chance = ChanceFor(bandit, captor, prisoner);
            if (chance <= 0f) return 0;
            if (MBRandom.RandomFloat > chance) return 0;

            // Nobody to hand it to, nobody robs him. See SpoilsFor.
            PartyBase spoils = SpoilsFor(captorParty, captor);
            if (spoils == null) return 0;

            int value;
            int taken = Take(spoils, prisoner, out value);
            if (taken == 0) return 0;

            // Doing the robbing costs the robber standing with the man he
            // robbed. Being robbed costs the victim nothing, because it is not
            // an act of his.
            //
            // The player is excluded as the victim, and that is forced rather
            // than chosen: Bannerlord keeps one relation number per pair of
            // heroes, so charging the captor and moving the player's number are
            // the same write. They cannot be separated, and of the two the
            // player's number is the one that should only ever move for things
            // he did. He is not excluded as the captor here -- he cannot reach
            // this line at all, the guard above returns before it. His own
            // reckoning is in PrisonerDialogue, where he chose it.
            if (captor != null && prisoner != Hero.MainHero)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(captor, prisoner, RelationCost, false);
            }

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
                        + " chance=" + (int)(chance * 100f) + "%"
                        + " pieces=" + taken
                        + " worth=" + value);

            return taken;
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

                float chance = ChanceFor(false, visitor, prisoner);
                if (chance <= 0f) continue;
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
                if (PlunderRules.Draw(visitor.StringId, prisoner.StringId, episode) > chance) continue;

                int value;
                int taken = Take(party.Party, prisoner, out value);
                if (taken == 0) continue;

                robbed += taken;
                worth += value;

                if (prisoner != Hero.MainHero)
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(visitor, prisoner,
                                                                          RelationCost, false);
                }

                ModLog.Info("PLUNDER captor=" + visitor.Name
                            + " captorHonor=" + visitor.GetTraitLevel(DefaultTraits.Honor)
                            + " prisoner=" + prisoner.Name
                            + " prisonerHonor=" + prisoner.GetTraitLevel(DefaultTraits.Honor)
                            + " chance=" + (int)(chance * 100f) + "%"
                            + " pieces=" + taken
                            + " worth=" + value
                            + " at=" + settlement.Name);
            }

            if (robbed == 0) return 0;

            Reward(visitor, worth);
            LootFitting.Equip(visitor, party.Party.ItemRoster,
                              Settings.ClanWeight, Settings.SkillWeight, Settings.MinimumTier);

            return robbed;
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
        /// </summary>
        public static bool CanBeStripped(Hero prisoner)
        {
            if (prisoner == null) return false;
            if (prisoner == Hero.MainHero) return true;
            return HeroFilter.IsEligible(prisoner);
        }

        /// <summary>
        /// The chance this captor robs this prisoner, with the game's own
        /// answers about the man fed into the pure rules.
        /// </summary>
        public static float ChanceFor(bool bandit, Hero captor, Hero prisoner)
        {
            if (bandit) return PlunderRules.Chance(true, 0, 0, 0, 0, 0, 0,
                                                   PlunderRules.Kinship.None, Settings.PlunderChance);
            if (captor == null) return 0f;

            return PlunderRules.Chance(false,
                                       captor.GetTraitLevel(DefaultTraits.Honor),
                                       captor.GetTraitLevel(DefaultTraits.Mercy),
                                       captor.GetTraitLevel(DefaultTraits.Generosity),
                                       captor.GetTraitLevel(DefaultTraits.Calculating),
                                       captor.GetSkillValue(DefaultSkills.Roguery),
                                       captor.GetRelation(prisoner),
                                       KinshipBetween(captor, prisoner),
                                       Settings.PlunderChance);
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
            return worn.Item != null && !worn.IsQuestItem;
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

        private static bool IsBanditParty(PartyBase party)
        {
            MobileParty mobile = party.MobileParty;
            if (mobile != null && mobile.IsBandit) return true;

            Clan clan = party.LeaderHero != null ? party.LeaderHero.Clan : null;
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
