using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

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
            HintText = "Scales every captor's chance. 1.00 is the measured design: a paragon 0%, "
                     + "an average lord 13%, a deceitful and tightfisted one 61%, bandits always. "
                     + "0.00 stops robbery without switching the system off, so gear already taken "
                     + "still circulates.")]
        [SettingPropertyGroup("Capture")]
        public float PlunderChance { get; set; } = 1.0f;
    }
}
