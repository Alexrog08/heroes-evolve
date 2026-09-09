using System;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;

namespace HeroLoadoutFixer
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
    /// Every figure quoted in the hints was measured rather than asserted. A
    /// man deciding whether to halve the spending share deserves to know it
    /// almost never binds as it stands, and a mod that spent this long
    /// measuring itself should say what it found.
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

        public override string Id { get { return "HeroLoadoutFixer"; } }
        public override string DisplayName { get { return "Hero Loadout Fixer"; } }
        public override string FolderName { get { return "HeroLoadoutFixer"; } }
        public override string FormatType { get { return "json2"; } }

        // ---- What runs ------------------------------------------------------

        [SettingPropertyBool("Repair broken lords", RequireRestart = false,
            HintText = "Fixes lords the game's come-of-age bug left half-equipped: too few weapons "
                     + "to fight with, or still in civilian clothing. Gives them a basic kit chosen "
                     + "for their skills, never the best one -- the rest they buy.")]
        [SettingPropertyGroup("What runs", GroupOrder = 6)]
        public bool EnableRepair { get; set; } = true;

        [SettingPropertyBool("Grow lords' skills", RequireRestart = false,
            HintText = "Lords otherwise stagnate and end up weaker than the troops they lead. "
                     + "Measured: one to three points a year in a given skill, about a hundred and "
                     + "ten over a forty-two-year career, aimed at a peak of a hundred and fifty "
                     + "times the man's own talent. He climbs all his life and never quite arrives.")]
        [SettingPropertyGroup("What runs", GroupOrder = 6)]
        public bool EnableSkillGrowth { get; set; } = true;

        [SettingPropertyBool("Lords buy their own gear", RequireRestart = false,
            HintText = "A lord entering a town may buy one thing. It only ever improves a slot he "
                     + "already fills and never changes what kind of fighter he is: repair decides "
                     + "what you are, buying decides how good you are.")]
        [SettingPropertyGroup("What runs", GroupOrder = 6)]
        public bool EnablePurchases { get; set; } = true;

        [SettingPropertyBool("Write hlf.log", RequireRestart = false,
            HintText = "Logs what the mod does, under the Bannerlord logs folder. Off costs nothing "
                     + "and writes nothing. The census below needs this on.")]
        [SettingPropertyGroup("What runs", GroupOrder = 6)]
        public bool EnableLogging { get; set; } = true;

        // ---- How good a lord's gear may get ---------------------------------

        [SettingPropertyInteger("Skill per tier of gear", 1, 400, "0", RequireRestart = false,
            HintText = "Combat skill points that buy one tier. LOWER MEANS BETTER EQUIPPED LORDS, "
                     + "and this is the setting to reach for first. 28 puts the average lord one "
                     + "tier above what the game already gave him, which leaves the top end to be "
                     + "decided by his clan's money rather than by this number.")]
        [SettingPropertyGroup("Gear ceiling", GroupOrder = 5)]
        public int SkillPerTier { get; set; } = 28;

        [SettingPropertyFloatingInteger("Weight of personal skill", 0f, 10f, "0.00",
            RequireRestart = false,
            HintText = "How far a lord's own fighting skill decides what he is allowed to wear.")]
        [SettingPropertyGroup("Gear ceiling", GroupOrder = 5)]
        public float SkillWeight { get; set; } = 1.0f;

        [SettingPropertyFloatingInteger("Weight of clan standing", 0f, 10f, "0.00",
            RequireRestart = false,
            HintText = "Zero from measurement rather than taste: across two campaigns, clan tier "
                     + "neither predicted what a lord wears nor separated the population, since by "
                     + "midgame every clan is tier 4 or better. Raise it if you want great houses "
                     + "to outfit their lords well regardless of merit.")]
        [SettingPropertyGroup("Gear ceiling", GroupOrder = 5)]
        public float ClanWeight { get; set; } = 0.0f;

        [SettingPropertyInteger("Lowest ceiling", 1, 6, "0", RequireRestart = false,
            HintText = "Nobody is capped below this, however unskilled.")]
        [SettingPropertyGroup("Gear ceiling", GroupOrder = 5)]
        public int MinimumTier { get; set; } = 1;

        [SettingPropertyInteger("Archer threshold", 0, 300, "0", RequireRestart = false,
            HintText = "How far a ranged skill must lead a lord's melee skills before the repair "
                     + "commits him to a bow or a crossbow. Lower makes more archers.")]
        [SettingPropertyGroup("Gear ceiling", GroupOrder = 5)]
        public int DominanceMargin { get; set; } = 30;

        // ---- Money ----------------------------------------------------------

        [SettingPropertyFloatingInteger("Chance of shopping per town visit", 0f, 1f, "0.00",
            RequireRestart = false,
            HintText = "He buys at most one thing per trip and takes at most one trip a day, so "
                     + "this is the pace of the whole engine. At 0.25 a lord converges on the gear "
                     + "he deserves over years, paying for it, which is the point.")]
        [SettingPropertyGroup("Money", GroupOrder = 4)]
        public float ShopChancePerVisit { get; set; } = 0.25f;

        [SettingPropertyFloatingInteger("Share of the purse per purchase", 0f, 1f, "0.00",
            RequireRestart = false,
            HintText = "What makes wealth matter: at a tenth, a tier-6 piece needs a house holding "
                     + "some 430,000. Measured on a mature campaign it almost never binds -- the "
                     + "median lord holds a million and spends six thousand -- so lower it if you "
                     + "want poverty to be felt.")]
        [SettingPropertyGroup("Money", GroupOrder = 4)]
        public float SpendingShare { get; set; } = 0.10f;

        [SettingPropertyFloatingInteger("Gold held back for troops", 0f, 10f, "0.00",
            RequireRestart = false,
            HintText = "Scales the reserve so buying gear can never stop a clan paying its men. "
                     + "1.00 keeps exactly the game's own threshold per war party. Zero removes the "
                     + "safety net.")]
        [SettingPropertyGroup("Money", GroupOrder = 4)]
        public float ReserveMultiplier { get; set; } = 1.0f;

        // ---- Losing gear ------------------------------------------------------

        [SettingPropertyBool("Lords rob their prisoners", RequireRestart = false,
            HintText = "A captor may take a captured lord's arms and armour, by his own character. "
                     + "Gear taken is moved and never destroyed, so it can be sold, worn, or won "
                     + "back in battle. You are never rolled for -- you rob by asking, in "
                     + "conversation, and pay for it.")]
        [SettingPropertyGroup("Capture", GroupOrder = 3)]
        public bool EnableCaptureLoss { get; set; } = true;

        [SettingPropertyFloatingInteger("Robbery chance multiplier", 0f, 5f, "0.00",
            RequireRestart = false,
            HintText = "Scales every captor's chance. At 1.00, measured across 495 lords: the "
                     + "median robs one prisoner in eight, the most honourable about one in fifty, "
                     + "the worst one in two. His word binds him hardest, then his loyalty to his "
                     + "own and his pity equally. Bandits always. 0.00 stops robbery without "
                     + "switching the system off, so gear already taken still circulates.")]
        [SettingPropertyGroup("Capture", GroupOrder = 3)]
        public float PlunderChance { get; set; } = 1.0f;

        // ---- Items -------------------------------------------------------------

        [SettingPropertyText("Items lords may never buy", RequireRestart = false,
            HintText = "Item ids separated by commas, empty by default. This mod shops in a "
                     + "catalogue it does not own, so any other mod can add an outlier and five "
                     + "hundred lords will find it faster than you will. Incendiary weapons are "
                     + "already refused outright and need no listing here.")]
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
        [SettingPropertyButton("Write a census to hlf.log",
            Content = "Run",
            RequireRestart = false,
            HintText = "Surveys every lord in the campaign -- tiers, gear, skills, traits, and what "
                     + "the engine would change -- and writes the report to hlf.log in the "
                     + "Bannerlord logs folder. Reads only; nothing in the campaign is modified. "
                     + "About half a second on a mature save. Needs logging switched on.")]
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
