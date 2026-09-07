using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
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
        /// Twenty sits below the thirty it charges for burning a village. No
        /// civilian is harmed here, and the offence is chiefly a breach of the
        /// customs of war rather than a cruelty.
        ///
        /// Only the player has traits that move at all: AddTraitXp is hardwired
        /// to Campaign.PlayerTraitDeveloper and Hero.MainHero, and an AI lord's
        /// character is fixed when he is generated. So this fires exactly when
        /// the player's own party robs somebody, which it does only if he has
        /// turned that on himself.
        /// </summary>
        public const int HostileActionXp = -20;

        /// <summary>
        /// Roguery earned per piece taken. A full strip of nine pieces is worth
        /// a couple of hundred, which is a real gain to a novice thief and
        /// nothing at all to a practised one -- and it feeds back: roguery
        /// raises the odds he does it again.
        /// </summary>
        public const int RogueryXpPerPiece = 25;

        /// <summary>
        /// Strips the prisoner if this captor would. Returns how many pieces
        /// changed hands, zero being much the commonest answer.
        /// </summary>
        public static int TryPlunder(PartyBase captorParty, Hero prisoner)
        {
            if (!Settings.EnableCaptureLoss) return 0;
            if (captorParty == null || prisoner == null || prisoner.BattleEquipment == null) return 0;

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

            int taken = Take(captorParty, prisoner);
            if (taken == 0) return 0;

            // Being robbed is not an act of the victim's, so it never moves
            // his standing. Doing the robbing is, so it always moves the
            // robber's -- including the player's, on the one path where he
            // reaches this at all.
            if (captor != null && prisoner != Hero.MainHero)
            {
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(captor, prisoner, RelationCost, false);
            }

            if (captor != null) Reward(captor, taken);

            ModLog.Info("PLUNDER captor=" + (captor != null ? captor.Name.ToString() : "bandits")
                        + " prisoner=" + prisoner.Name
                        + " chance=" + (int)(chance * 100f) + "%"
                        + " pieces=" + taken);

            return taken;
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
        /// captor has no use for a cuirass of the wrong culture and sells it,
        /// and it turns up on a shelf somewhere for its owner to buy back, or
        /// for whoever walks into that town first. Caladog would never sell his
        /// gilded plate. He can still lose it, and then it is anybody's.
        ///
        /// That is the point of the whole feature: unique gear circulating
        /// because it was taken, rather than sitting in one man's slot forever.
        /// </summary>
        public static int Take(PartyBase captorParty, Hero prisoner)
        {
            ItemRoster loot = captorParty.ItemRoster;
            int taken = 0;

            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                taken += TakeSlot(loot, prisoner, SlotMapping.WeaponSlot(i));
            }
            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                taken += TakeSlot(loot, prisoner, slot);
            }
            taken += TakeSlot(loot, prisoner, EquipmentIndex.Horse);
            taken += TakeSlot(loot, prisoner, EquipmentIndex.HorseHarness);

            return taken;
        }

        private static int TakeSlot(ItemRoster loot, Hero prisoner, EquipmentIndex slot)
        {
            EquipmentElement worn = prisoner.BattleEquipment[slot];
            if (worn.Item == null) return 0;

            prisoner.BattleEquipment[slot] = EquipmentElement.Invalid;
            if (loot != null) loot.AddToCounts(worn, 1);
            return 1;
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
                if (prisoner.BattleEquipment[SlotMapping.WeaponSlot(i)].Item != null) return true;
            }
            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                if (prisoner.BattleEquipment[slot].Item != null) return true;
            }
            return prisoner.BattleEquipment[EquipmentIndex.Horse].Item != null
                   || prisoner.BattleEquipment[EquipmentIndex.HorseHarness].Item != null;
        }

        /// <summary>
        /// Practice at theft. A lord who robs gets better at it, and being
        /// better at it makes him likelier to rob again -- which closes a loop
        /// the rules already opened through roguery.
        ///
        /// No trait cost here: the game keeps a trait ledger for the player
        /// alone, and the player never reaches this path. His own reckoning is
        /// in PrisonerDialogue, where he chose it.
        /// </summary>
        private static void Reward(Hero captor, int pieces)
        {
            captor.AddSkillXp(DefaultSkills.Roguery, RogueryXpPerPiece * pieces);
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
