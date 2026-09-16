using System;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using TaleWorlds.CampaignSystem;

namespace HeroesEvolve
{
    /// <summary>
    /// The whole configuration, in the game's own options screen.
    ///
    /// A second face on Settings, not a second source of truth. Everything in
    /// this mod reads Settings and only Settings; McmBridge copies what MCM
    /// holds into those fields at campaign load and again whenever the player
    /// moves a slider. That is the arrangement the Settings doc comment
    /// promised when it first turned MCM down, and keeping it means a switch
    /// changes behaviour the instant the next tick reads it -- no restart, no
    /// reload, and no second place for a value to hide.
    ///
    /// Defaults match settings.xml deliberately. A player who installs MCM
    /// halfway through a campaign should find the switches where he left them.
    ///
    /// Hints are kept short, and that is a constraint of the screen rather
    /// than a style. MCM does not wrap or scroll a long hint, it runs it off
    /// the edge, so a hint that does not fit is not a long explanation -- it
    /// is a truncated one, which is worse than a brief one. These ran to 581
    /// characters before anyone looked at them in the game.
    ///
    /// So each says what the setting does and, where it earns the room, what
    /// moving it costs. The measurements that used to be quoted here -- how
    /// many points a year, what a campaign of 5,968 caravan-days found -- live
    /// in settings.xml instead, which is a file with no margins and is read by
    /// anyone who cares enough to open it.
    ///
    /// MCM is not required. Nothing outside McmBridge names a type from it, and
    /// that one contact point is wrapped, so on a machine without MCM this
    /// class is never loaded and settings.xml carries the whole configuration
    /// exactly as before.
    /// </summary>
    public class McmSettings : AttributeGlobalSettings<McmSettings>
    {
        // GroupOrder counts DOWN the screen: 6 is the top group, 1 the bottom.
        //
        // Backwards from the obvious reading, and not a matter of taste --
        // MCM's CollectionExtensions.SortDefault orders groups by
        // OrderByDescending(isDefaultGroup).ThenByDescending(Order), so the
        // largest number is drawn first. Numbering these 0..5 in the natural
        // direction stands the screen on its head and puts the diagnostic
        // button above the switches. Renumber only in this direction.
        //
        // Within a group the properties sort the other way (OrderBy, then by
        // display name), so declaration order here does not survive; nothing
        // sets a per-property Order, which leaves them alphabetical.

        public override string Id { get { return "HeroesEvolve"; } }
        public override string DisplayName { get { return "Heroes Evolve"; } }
        public override string FolderName { get { return "HeroesEvolve"; } }
        public override string FormatType { get { return "json2"; } }

        // ---- What runs ------------------------------------------------------

        [SettingPropertyBool("Give a starting kit to lords who have none", RequireRestart = false,
            HintText = "Dresses a lord who has nothing: born in civilian clothes, or stripped by a captor. A plain kit for his culture and skills. The rest he buys.")]
        [SettingPropertyGroup("What runs", GroupOrder = 6)]
        public bool EnableRepair { get; set; } = true;

        [SettingPropertyBool("Grow lords' skills", RequireRestart = false,
            HintText = "Without this a lord stagnates and ends up weaker than his own troops. One to three points a year, toward a ceiling set by his talent.")]
        [SettingPropertyGroup("What runs", GroupOrder = 6)]
        public bool EnableSkillGrowth { get; set; } = true;

        [SettingPropertyBool("Start lords on their curve", RequireRestart = false,
            HintText = "New campaigns only, and only with skill growth on. Every lord starts where his age and talent put him, then grows. Saves already under way are left alone.")]
        [SettingPropertyGroup("What runs", GroupOrder = 6)]
        public bool StartLordsOnCurve { get; set; } = true;

        [SettingPropertyBool("Lords buy their own gear", RequireRestart = false,
            HintText = "In a town, a lord buys a better version of what he already carries. Never changes what kind of fighter he is, never fills an empty slot.")]
        [SettingPropertyGroup("What runs", GroupOrder = 6)]
        public bool EnablePurchases { get; set; } = true;

        [SettingPropertyBool("Include my own clan", RequireRestart = false,
            HintText = "Your family and the companions leading your parties and caravans. Turn off if you outfit them by hand. Heroes inside your own party are never touched either way.")]
        [SettingPropertyGroup("What runs", GroupOrder = 6)]
        public bool ManageOwnClan { get; set; } = true;

        [SettingPropertyBool("Write hev.log", RequireRestart = false,
            HintText = "Logs what the mod does, in the Bannerlord logs folder. The census below needs it on.")]
        [SettingPropertyGroup("What runs", GroupOrder = 6)]
        public bool EnableLogging { get; set; } = true;

        // ---- How good a lord's gear may get ---------------------------------

        [SettingPropertyInteger("Skill per tier of gear", 1, 400, "0", RequireRestart = false,
            HintText = "Combat skill that buys one tier. LOWER MEANS BETTER EQUIPPED LORDS, and this is the first setting to reach for.")]
        [SettingPropertyGroup("Gear ceiling", GroupOrder = 5)]
        public int SkillPerTier { get; set; } = 28;

        [SettingPropertyFloatingInteger("Weight of personal skill", 0f, 10f, "0.00",
            RequireRestart = false,
            HintText = "How far a lord's own fighting skill decides what he is allowed to wear.")]
        [SettingPropertyGroup("Gear ceiling", GroupOrder = 5)]
        public float SkillWeight { get; set; } = 1.0f;

        [SettingPropertyFloatingInteger("Weight of clan standing", 0f, 10f, "0.00",
            RequireRestart = false,
            HintText = "Zero by default: two campaigns found clan tier does not predict what a lord wears. Raise it to have great houses dress their lords well regardless of merit.")]
        [SettingPropertyGroup("Gear ceiling", GroupOrder = 5)]
        public float ClanWeight { get; set; } = 0.0f;

        [SettingPropertyInteger("Lowest ceiling", 1, 6, "0", RequireRestart = false,
            HintText = "Nobody is capped below this, however unskilled.")]
        [SettingPropertyGroup("Gear ceiling", GroupOrder = 5)]
        public int MinimumTier { get; set; } = 1;

        [SettingPropertyInteger("Archer threshold", 0, 300, "0", RequireRestart = false,
            HintText = "How far a ranged skill must lead his melee skills before he is given a bow or a crossbow. Lower makes more archers.")]
        [SettingPropertyGroup("Gear ceiling", GroupOrder = 5)]
        public int DominanceMargin { get; set; } = 30;

        // ---- Money ----------------------------------------------------------

        [SettingPropertyFloatingInteger("Chance of shopping per town visit", 0f, 1f, "0.00",
            RequireRestart = false,
            HintText = "How often a lord bothers with the market. One trip a day at most. At 1.00 he stops at every town he enters.")]
        [SettingPropertyGroup("Money", GroupOrder = 4)]
        public float ShopChancePerVisit { get; set; } = 0.25f;

        // Listed in CultureChoice's own order, so the selected index is the value.
        [SettingPropertyDropdown("Culture favoured when buying", RequireRestart = false,
            HintText = "Whose colours a lord prefers at market: his clan's, his own, or none. Worth one tier. Repair still dresses him as his own people.")]
        [SettingPropertyGroup("Money", GroupOrder = 4)]
        public Dropdown<string> ShoppingCulture { get; set; } =
            new Dropdown<string>(new string[] { "His clan's culture", "His own culture", "No preference" }, 1);

        [SettingPropertyFloatingInteger("Share of the purse per shopping trip", 0f, 1f, "0.00",
            RequireRestart = false,
            HintText = "What a lord may spend in one trip, split between the slots that town can improve. Lower means poorer lords and slower recovery after a robbery.")]
        [SettingPropertyGroup("Money", GroupOrder = 4)]
        public float SpendingShare { get; set; } = 0.30f;

        [SettingPropertyFloatingInteger("Caravan leader's commission", 0f, 1f, "0.00",
            RequireRestart = false,
            HintText = "What a caravan's leader keeps of its profit, and the only money he buys gear with. Higher arms him better and pays you less. At 1.00 the caravan pays you nothing.")]
        [SettingPropertyGroup("Money", GroupOrder = 4)]
        public float CaravanGearShare { get; set; } = 0.50f;

        [SettingPropertyFloatingInteger("Gold held back for troops", 0f, 10f, "0.00",
            RequireRestart = false,
            HintText = "Reserve kept back so buying gear can never stop a clan paying its men. Zero removes the safety net.")]
        [SettingPropertyGroup("Money", GroupOrder = 4)]
        public float ReserveMultiplier { get; set; } = 1.0f;

        // ---- Losing gear ------------------------------------------------------

        [SettingPropertyBool("Lords rob their prisoners", RequireRestart = false,
            HintText = "A captor may strip a captured lord, according to his character. Gear is moved and never destroyed, so it can be won back. You rob by asking, in conversation.")]
        [SettingPropertyGroup("Capture", GroupOrder = 3)]
        public bool EnableCaptureLoss { get; set; } = true;

        [SettingPropertyFloatingInteger("Robbery chance multiplier", 0f, 5f, "0.00",
            RequireRestart = false,
            HintText = "Scales every captor's chance; bandits always rob. At 1.00 robbery becomes the biggest influence on what lords wear. 0.00 stops it without switching the system off.")]
        [SettingPropertyGroup("Capture", GroupOrder = 3)]
        public float PlunderChance { get; set; } = 0.5f;

        // ---- Items -------------------------------------------------------------

        [SettingPropertyText("Items lords may never buy", RequireRestart = false,
            HintText = "Item ids separated by commas. For outliers another mod adds -- five hundred lords will find one faster than you will. Incendiaries are already refused.")]
        [SettingPropertyGroup("Items", GroupOrder = 2)]
        public string ExcludedItems { get; set; } = "";

        // ---- Diagnostics --------------------------------------------------------

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
        [SettingPropertyButton("Write a census to hev.log",
            Content = "Run",
            RequireRestart = false,
            HintText = "Surveys every lord -- tiers, gear, skills, traits -- into hev.log. Reads only and changes nothing. About half a second. Needs logging on.")]
        [SettingPropertyGroup("Diagnostics", GroupOrder = 1)]
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
