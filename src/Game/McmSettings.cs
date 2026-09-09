using System;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// The in-game face of the capture settings, for players who have MCM.
    ///
    /// A second face on Settings, not a second source of truth. Everything in
    /// this mod reads Settings and only Settings; McmBridge copies what MCM
    /// holds into those fields at campaign load and again whenever the player
    /// moves a slider. That is the arrangement the Settings doc comment
    /// promised when it turned MCM down, and keeping it means the toggle
    /// changes behaviour the same instant the tick next reads it -- no restart,
    /// no reload.
    ///
    /// Only the capture settings live here. The other eleven are still in
    /// settings.xml, because they are things you set once while tuning a
    /// campaign, and this is the one you reach for mid-game.
    ///
    /// MCM is not required. Nothing outside McmBridge names a type from it, and
    /// that one contact point is wrapped, so on a machine without MCM this
    /// class is simply never loaded and settings.xml carries the whole
    /// configuration exactly as before.
    /// </summary>
    public class McmSettings : AttributeGlobalSettings<McmSettings>
    {
        public override string Id { get { return "HeroLoadoutFixer"; } }
        public override string DisplayName { get { return "Hero Loadout Fixer"; } }
        public override string FolderName { get { return "HeroLoadoutFixer"; } }
        public override string FormatType { get { return "json2"; } }

        /// <summary>
        /// Defaults match settings.xml deliberately. A player who installs MCM
        /// halfway through a campaign should find the switches where he left
        /// them, not reset to something else.
        /// </summary>
        [SettingPropertyBool("Lords rob their prisoners",
            RequireRestart = false,
            HintText = "A captor may take a captured lord's arms and armour, by his own character: "
                     + "honest men rarely, deceitful men often, bandits always. Gear taken is moved, "
                     + "never destroyed, so it can be sold, worn, or won back in battle. "
                     + "You are never rolled for -- you rob by asking, in conversation.")]
        [SettingPropertyGroup("Capture")]
        public bool EnableCaptureLoss { get; set; } = false;

        [SettingPropertyFloatingInteger("Robbery chance multiplier", 0f, 5f, "0.00",
            RequireRestart = false,
            HintText = "Scales every captor's chance. At 1.00, measured across 495 lords: the "
                     + "median robs one prisoner in eight, the most honourable about one in fifty, "
                     + "the worst one in two. His word binds him hardest, then his loyalty to his "
                     + "own and his pity equally. Bandits always. 0.00 stops robbery without switching "
                     + "the system off, so gear already taken still circulates.")]
        [SettingPropertyGroup("Capture")]
        public float PlunderChance { get; set; } = 1.0f;

        /// <summary>
        /// The census, without the developer console.
        ///
        /// Worth having for a reason that took a live campaign to notice. The
        /// console only opens with cheat_mode = 1 in engine_config.txt, and
        /// cheat mode is not a quiet flag: PartyScreenHelper.OpenScreenAsNormal
        /// diverts to OpenScreenAsCheat and fills the party screen from
        /// GetRosterWithAllGameTroops, while the inventory screen stocks its
        /// far side with the whole item catalogue. Turning the console on to
        /// read a diagnostic therefore turns the game into a sandbox, which is
        /// a steep price for a log file. This button costs nothing and leaves
        /// cheat mode off.
        /// </summary>
        [SettingPropertyButton("Write a census to hlf.log",
            Content = "Run",
            RequireRestart = false,
            HintText = "Surveys every lord in the campaign -- tiers, gear, skills, what the engine "
                     + "would change -- and writes the report to hlf.log in the Bannerlord logs "
                     + "folder. Reads only; nothing in the campaign is modified. Takes about half a "
                     + "second on a mature save.")]
        [SettingPropertyGroup("Diagnostics")]
        public Action RunCensus { get; set; } = Census;

        /// <summary>
        /// Guarded the same way every other entry point in this mod is: a
        /// diagnostic must never be the thing that takes a campaign down.
        /// </summary>
        private static void Census()
        {
            try
            {
                if (Campaign.Current == null)
                {
                    ModLog.Info("CENSUS refused: no campaign is running");
                    return;
                }

                Diagnostics.RunCensus(HeroLoadoutBehavior.ClanWeight, HeroLoadoutBehavior.SkillWeight,
                                      HeroLoadoutBehavior.MinimumTier, HeroLoadoutBehavior.DominanceMargin);
            }
            catch (Exception ex)
            {
                ModLog.Error("census failed: " + ex.GetType().Name + " " + ex.Message);
            }
        }
    }
}
