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
        // Sections follow what the mod does, one area each, in the order the
        // store page tells it: skills, the kit a stripped lord is given, what a
        // lord may wear and buy, caravans, robbery, then the switches that are
        // about you and about the log. Each area's on switch comes first in its
        // section and its dials after it, so nothing about buying sits among
        // the skills or the other way round.
        //
        // GroupOrder counts DOWN the screen: 8 is the top section, 1 the bottom.
        // Backwards from the obvious reading, and not a matter of taste --
        // MCM's CollectionExtensions.SortDefault orders groups by
        // OrderByDescending(isDefaultGroup).ThenByDescending(Order), so the
        // largest number is drawn first. Renumber only in this direction.
        //
        // Within a section properties sort the other way, OrderBy(Order) and then
        // by display name, which is why every property sets Order: without it
        // they fall alphabetical and the switch lands among its own dials.
        //
        // Property names are what MCM saves values under. Moving a property to
        // another section or renaming its label keeps the player's value;
        // renaming the property itself would silently reset it.

        public override string Id { get { return "HeroesEvolve"; } }
        public override string DisplayName { get { return "Heroes Evolve"; } }
        public override string FolderName { get { return "HeroesEvolve"; } }
        public override string FormatType { get { return "json2"; } }

        // ---- Skills -----------------------------------------------------------

        [SettingPropertyBool("Grow lords' skills", RequireRestart = false, Order = 0,
            HintText = "Without this a lord stagnates and ends up weaker than his own troops. One to three points a year, toward a ceiling set by his talent.")]
        [SettingPropertyGroup("Skills", GroupOrder = 8)]
        public bool EnableSkillGrowth { get; set; } = true;

        [SettingPropertyBool("Start lords on their curve", RequireRestart = false, Order = 1,
            HintText = "New campaigns only, and only with skill growth on. Every lord starts where his age and talent put him, then grows. Saves already under way are left alone.")]
        [SettingPropertyGroup("Skills", GroupOrder = 8)]
        public bool StartLordsOnCurve { get; set; } = true;

        // ---- Starting kits --------------------------------------------------

        [SettingPropertyBool("Give a starting kit to lords who have none", RequireRestart = false, Order = 0,
            HintText = "Dresses a lord who has nothing: born in civilian clothes, or stripped by a captor. A plain kit for his culture and skills. The rest he buys.")]
        [SettingPropertyGroup("Starting kits", GroupOrder = 7)]
        public bool EnableRepair { get; set; } = true;

        [SettingPropertyInteger("Archer threshold", 0, 300, "0", RequireRestart = false, Order = 1,
            HintText = "How far a ranged skill must lead his melee skills before his kit is a bow or a crossbow. Lower makes more archers.")]
        [SettingPropertyGroup("Starting kits", GroupOrder = 7)]
        public int DominanceMargin { get; set; } = 30;

        // ---- Shopping ---------------------------------------------------------

        [SettingPropertyBool("Lords buy their own gear", RequireRestart = false, Order = 0,
            HintText = "In a town, a lord buys a better version of what he already carries. Never changes what kind of fighter he is, never fills an empty slot.")]
        [SettingPropertyGroup("Shopping", GroupOrder = 6)]
        public bool EnablePurchases { get; set; } = true;

        [SettingPropertyFloatingInteger("Chance of shopping per town visit", 0f, 1f, "0.00",
            RequireRestart = false, Order = 1,
            HintText = "How often a lord bothers with the market. One trip a day at most. At 1.00 he stops at every town he enters.")]
        [SettingPropertyGroup("Shopping", GroupOrder = 6)]
        public float ShopChancePerVisit { get; set; } = 0.25f;

        [SettingPropertyFloatingInteger("Share of the purse per shopping trip", 0f, 1f, "0.00",
            RequireRestart = false, Order = 2,
            HintText = "What a lord may spend in one trip, split between the slots that town can improve. Lower means poorer lords and slower recovery after a robbery.")]
        [SettingPropertyGroup("Shopping", GroupOrder = 6)]
        public float SpendingShare { get; set; } = 0.30f;

        [SettingPropertyFloatingInteger("Gold held back for troops", 0f, 10f, "0.00",
            RequireRestart = false, Order = 3,
            HintText = "Reserve kept back so buying gear can never stop a clan paying its men. Zero removes the safety net.")]
        [SettingPropertyGroup("Shopping", GroupOrder = 6)]
        public float ReserveMultiplier { get; set; } = 1.0f;

        // Listed in CultureChoice's own order, so the selected index is the value.
        [SettingPropertyDropdown("Culture favoured when buying", RequireRestart = false, Order = 4,
            HintText = "Whose colours a lord prefers at market: his clan's, his own, or none. Worth one tier. Repair still dresses him as his own people.")]
        [SettingPropertyGroup("Shopping", GroupOrder = 6)]
        public Dropdown<string> ShoppingCulture { get; set; } =
            new Dropdown<string>(new string[] { "His clan's culture", "His own culture", "No preference" }, 1);

        // ---- Gear limits: what a lord may wear, from a kit or a market --------------

        [SettingPropertyInteger("Skill per tier of gear", 1, 400, "0", RequireRestart = false, Order = 0,
            HintText = "Combat skill that buys one tier. LOWER MEANS BETTER EQUIPPED LORDS, and this is the first setting to reach for.")]
        [SettingPropertyGroup("Gear limits", GroupOrder = 5)]
        public int SkillPerTier { get; set; } = 28;

        [SettingPropertyFloatingInteger("Weight of personal skill", 0f, 10f, "0.00",
            RequireRestart = false, Order = 1,
            HintText = "How far a lord's own fighting skill decides what he is allowed to wear.")]
        [SettingPropertyGroup("Gear limits", GroupOrder = 5)]
        public float SkillWeight { get; set; } = 1.0f;

        [SettingPropertyFloatingInteger("Weight of clan standing", 0f, 10f, "0.00",
            RequireRestart = false, Order = 2,
            HintText = "Zero by default: two campaigns found clan tier does not predict what a lord wears. Raise it to have great houses dress their lords well regardless of merit.")]
        [SettingPropertyGroup("Gear limits", GroupOrder = 5)]
        public float ClanWeight { get; set; } = 0.0f;

        [SettingPropertyInteger("Lowest ceiling", 1, 6, "0", RequireRestart = false, Order = 3,
            HintText = "Nobody is capped below this, however unskilled.")]
        [SettingPropertyGroup("Gear limits", GroupOrder = 5)]
        public int MinimumTier { get; set; } = 1;

        [SettingPropertyText("Items lords never get", RequireRestart = false, Order = 4,
            HintText = "Item ids separated by commas: never bought, never given in a kit. For outliers another mod adds. Incendiaries are already refused.")]
        [SettingPropertyGroup("Gear limits", GroupOrder = 5)]
        public string ExcludedItems { get; set; } = "";

        // ---- Caravans ------------------------------------------------------------

        [SettingPropertyFloatingInteger("Caravan leader's commission", 0f, 1f, "0.00",
            RequireRestart = false, Order = 0,
            HintText = "What a caravan's leader keeps of its profit, and the only money he buys gear with. Higher arms him better and pays you less. At 1.00 the caravan pays you nothing.")]
        [SettingPropertyGroup("Caravans", GroupOrder = 4)]
        public float CaravanGearShare { get; set; } = 0.50f;

        // ---- Robbery ---------------------------------------------------------------

        [SettingPropertyBool("Lords rob their prisoners", RequireRestart = false, Order = 0,
            HintText = "A captor may strip a captured lord, according to his character. Gear is moved and never destroyed, so it can be won back. You rob by asking, in conversation.")]
        [SettingPropertyGroup("Robbery", GroupOrder = 3)]
        public bool EnableCaptureLoss { get; set; } = true;

        [SettingPropertyFloatingInteger("Robbery chance multiplier", 0f, 5f, "0.00",
            RequireRestart = false, Order = 1,
            HintText = "Scales every captor's chance; bandits always rob. At 1.00 robbery becomes the biggest influence on what lords wear. 0.00 stops it without switching the system off.")]
        [SettingPropertyGroup("Robbery", GroupOrder = 3)]
        public float PlunderChance { get; set; } = 0.5f;

        // ---- Your own clan ----------------------------------------------------------

        [SettingPropertyBool("Include my own clan", RequireRestart = false, Order = 0,
            HintText = "Your family and the companions leading your parties and caravans. Turn off if you outfit them by hand. Heroes inside your own party are never touched either way.")]
        [SettingPropertyGroup("Your own clan", GroupOrder = 2)]
        public bool ManageOwnClan { get; set; } = true;

        // ---- Diagnostics --------------------------------------------------------

        [SettingPropertyBool("Write hev.log", RequireRestart = false, Order = 0,
            HintText = "Logs what the mod does, in the Bannerlord logs folder. The census below needs it on.")]
        [SettingPropertyGroup("Diagnostics", GroupOrder = 1)]
        public bool EnableLogging { get; set; } = true;

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
            Order = 1,
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
