using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
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
            if (!Settings.EnableCaptureLoss) return false;

            Hero hero = Hero.OneToOneConversationHero;
            if (hero == null || hero == Hero.MainHero) return false;
            if (hero.BattleEquipment == null) return false;

            // His, and mine to take: a prisoner in somebody else's dungeon is
            // not something this option should reach.
            if (!hero.IsPrisoner) return false;
            if (hero.PartyBelongedToAsPrisoner != PartyBase.MainParty) return false;

            return PlunderService.HasAnythingToTake(hero);
        }

        private static void Strip()
        {
            Hero hero = Hero.OneToOneConversationHero;
            if (hero == null) return;

            int taken = PlunderService.Take(PartyBase.MainParty, hero);
            if (taken == 0) return;

            // Chosen, and therefore paid for. This is the one path where the
            // player's own standing moves, and it is right that it does: the
            // objection was ever only to consequences for decisions he did not
            // make. His relatives hear about it too.
            ChangeRelationAction.ApplyPlayerRelation(hero, PlayerRelationCost, true, true);
            TraitLevelingHelper.OnHostileAction(PlunderService.HostileActionXp);
            Hero.MainHero.AddSkillXp(DefaultSkills.Roguery, PlunderService.RogueryXpPerPiece * taken);

            ModLog.Info("PLUNDER by player prisoner=" + hero.Name + " pieces=" + taken);
        }
    }
}
