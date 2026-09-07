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
                                  CanStrip, null, 100, null);

            starter.AddDialogLine("hlf_strip_prisoner_reply",
                                  "hlf_strip_prisoner_reply",
                                  "close_window",
                                  "{=hlf_strip_reply}You would strip a beaten man of his own harness? "
                                  + "Take them, then. My kin will hear of this.",
                                  null, Strip, 100, null);
        }

        /// <summary>
        /// Offered only for a lord you are actually holding, who still has
        /// something to hand over.
        /// </summary>
        private static bool CanStrip()
        {
            try
            {
                return CanStripCore();
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

            // Into whatever is holding him: your saddlebags on the road, the
            // keep's stores if he is in a cell. The gear stays where the man is.
            PartyBase holder = hero.PartyBelongedToAsPrisoner;
            if (holder == null) return;

            int value;
            int taken = PlunderService.Take(holder, hero, out value);
            if (taken == 0) return;

            // Chosen, and therefore paid for. This is the one path where the
            // player's own standing moves, and it is right that it does: the
            // objection was ever only to consequences for decisions he did not
            // make. His relatives hear about it too.
            ChangeRelationAction.ApplyPlayerRelation(hero, PlayerRelationCost, true, true);
            TraitLevelingHelper.OnHostileAction(PlunderService.HostileActionXp);
            Hero.MainHero.AddSkillXp(DefaultSkills.Roguery, PlunderService.RogueryXpFor(value));

            ModLog.Info("PLUNDER by player prisoner=" + hero.Name
                        + " pieces=" + taken + " worth=" + value
                        + " roguery=" + PlunderService.RogueryXpFor(value));
        }
    }
}
