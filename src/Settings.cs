using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace HeroesEvolve
{
    /// <summary>
    /// Every number a player is allowed to change, in one place.
    ///
    /// Read from settings.xml beside the module's own dll at load, and every
    /// field keeps its default if the file is missing, unreadable or says
    /// something the field cannot hold. A malformed settings file must never be
    /// the reason a campaign fails to start, so nothing here throws and nothing
    /// here is required.
    ///
    /// Not MCM. MCM is the usual way to do this in Bannerlord and it is nicer,
    /// but it is a hard dependency every user then has to install, and this mod
    /// otherwise needs nothing but the game. If MCM is added later it becomes a
    /// second face on this same class rather than a rewrite: everything reads
    /// its values from here and from nowhere else.
    /// </summary>
    public static class Settings
    {
        // --- Scope: which systems run at all -------------------------------

        /// <summary>Repair lords the come-of-age bug left half-equipped.</summary>
        public static bool EnableRepair = true;

        /// <summary>Grow lords' skills so they do not fall behind their troops.</summary>
        public static bool EnableSkillGrowth = true;

        /// <summary>At a new campaign, start every lord's skills where his age and talent put him.</summary>
        public static bool StartLordsOnCurve = true;

        /// <summary>Let lords buy better gear when they visit a town.</summary>
        public static bool EnablePurchases = true;

        /// <summary>
        /// Whose colours a lord favours when he buys: his clan's, his own, or no
        /// one's. His own by default, which is what the market always did.
        /// </summary>
        public static Core.CultureChoice ShoppingCulture = Core.CultureChoice.Hero;

        /// <summary>
        /// Refuse armour that is not the favoured culture's outright, rather
        /// than merely preferring it.
        ///
        /// Off by default because a wall cannot be reasoned with, not because
        /// the catalogue cannot carry it: counted over what a lord may now buy,
        /// every culture has armour for every slot -- the thinnest is a single
        /// pair of boots for five of the seven, against three for Empire and
        /// Vlandia -- so nothing is left frozen. Expect a lord's boots to be
        /// decided for him, and his helmet chosen from eleven rather than the
        /// three hundred on the shelf.
        ///
        /// Pieces belonging to no culture pass the wall: they carry nobody's
        /// colours to clash with, and they are what dresses a Nord's horse,
        /// since his people sell no harness at all.
        /// </summary>
        public static bool OwnCultureArmorOnly = false;

        /// <summary>
        /// The same wall for weapons, where the catalogue is kinder: 350 crafted
        /// blades carry a culture between them, 32 to 55 each, and 63 belong to
        /// nobody. The thin cases are the Nord bow -- his people sell none, so he
        /// takes one of the three neutral ones -- and the crossbow, which only
        /// Empire and Vlandia make, so a Battanian who carries one will never
        /// upgrade it.
        /// </summary>
        public static bool OwnCultureWeaponsOnly = false;

        /// <summary>Write hev.log. Off costs nothing and writes nothing.</summary>
        public static bool EnableLogging = true;

        // --- How good a lord's gear may get --------------------------------

        /// <summary>
        /// Weight of clan standing in the tier ceiling. Zero by measurement:
        /// two censuses found clan tier neither predicts what a lord wears nor
        /// separates the population past midgame. See TierCeiling.
        /// </summary>
        public static float ClanWeight = 0.0f;

        /// <summary>Weight of personal combat skill in the tier ceiling.</summary>
        public static float SkillWeight = 1.0f;

        /// <summary>
        /// Combat skill points that buy one tier. Lower means better-equipped
        /// lords. 28 puts the median lord one tier above what he already wears,
        /// which is what hands the top end to his purse instead of to the
        /// ceiling.
        /// </summary>
        public static int SkillPerTier = 28;

        /// <summary>Floor under the ceiling, so nobody is capped at nothing.</summary>
        public static int MinimumTier = 1;

        /// <summary>How far a ranged skill must lead for the planner to commit to it.</summary>
        public static int DominanceMargin = 30;

        // --- Money ----------------------------------------------------------

        /// <summary>
        /// Share of his wallet a hero may put into ONE TRIP to the market,
        /// divided between the slots that town can improve for him.
        ///
        /// This is what makes gold matter. A campaign measured 876,000 as the
        /// median wallet against a 2,924 median purchase: without a share,
        /// money is not a constraint and never becomes one, and the tier
        /// ceiling is the only thing between a lord and the best item on the
        /// shelf. With one, a price is measured against the wealth of the house
        /// paying it -- a tier-6 piece runs about 43,300, so at three tenths it
        /// takes a house holding some 145,000 to reach one, and a young clan is
        /// priced out of the good stuff and grows into it.
        ///
        /// The figure began at a tenth, which is Lords Gear's own territory --
        /// its AIGoldSpendingPercentage and ClanGoldSpendingPercentage multiply
        /// the same wallet. It is the one number in this mod taken from
        /// somebody else's play experience rather than from measurement, which
        /// is why the census reports what it buys at the share actually in
        /// force.
        /// </summary>
        public static float SpendingShare = 0.30f;

        /// <summary>
        /// Scales the gold held back to keep troops paid. 1.0 keeps exactly the
        /// game's own threshold per war party.
        /// </summary>
        public static float ReserveMultiplier = 1.0f;

        /// <summary>Chance a lord goes shopping when he enters a town.</summary>
        public static float ShopChancePerVisit = 0.25f;

        // --- Losing gear ----------------------------------------------------

        /// <summary>
        /// Lets a captor strip his prisoner.
        ///
        /// On by default. It was off while the system was unproven, on the
        /// grounds that it changes the feel of every defeat and that should be
        /// a choice -- but a campaign has now run it through seventy-one
        /// robberies, fourteen recoveries and four sales without an error, and
        /// a feature nobody switches on is a feature nobody has. The switch is
        /// still there for anyone who wants the old shape.
        /// </summary>
        public static bool EnableCaptureLoss = true;

        /// <summary>
        /// Scales how often a captor robs. Zero is the same as off; one runs
        /// the rules as written. See PlunderRules -- every weight in there is
        /// invented, so this is the dial that matters until a campaign has
        /// measured the rate.
        /// </summary>
        public static float RobberyRate = 1.0f;

        /// <summary>
        /// What the rules are actually handed: the player's rate times the
        /// calibration that makes 1.00 mean a normal campaign. See
        /// PlunderRules.NormalRate.
        /// </summary>
        public static float RobberyMultiplier()
        {
            return RobberyRate * Core.PlunderRules.NormalRate;
        }

        /// <summary>
        /// The commission a caravan's leader keeps out of what it earns, and
        /// the only money he has to dress himself with.
        ///
        /// A fifth by default. Measured against what 1,560 purchases actually
        /// cost, a caravan earning a thousand a day pays its master about 16,800
        /// a year at 0.20: a full tier-3 kit in a little over a year, tier 4 in
        /// about three, tier 5 in about six, if his skills allow those tiers and
        /// his caravan is not robbed first. It was 0.50 -- half of every
        /// caravan's income -- when that half was a commission plus a matching
        /// multiplier; once it became a plain daily commission, half was simply
        /// too much to take every day. Nothing is taken while he has nothing
        /// left to improve (see CaravanPurse.CommissionDue).
        ///
        /// At 1.00 he keeps everything and the caravan stops paying its owner
        /// at all -- a real choice, and the reason the slider runs that far.
        /// Zero does not hand him back the clan purse. It stops the commission
        /// and leaves him spending down whatever he has until he has nothing,
        /// because the feature is that his patron's treasury is not his to
        /// spend, and that has to hold at every setting rather than most.
        /// </summary>
        public static float CaravanGearShare = 0.20f;

        /// <summary>
        /// Whether this mod dresses the heroes of the player's own clan.
        ///
        /// On by default, because a party led by your brother is a party the AI
        /// takes into battle and the same argument for equipping any other lord
        /// applies to him.
        ///
        /// Off is not a special rule for the player, and this mod does not have
        /// those. It is the same boundary already drawn around heroes in the
        /// main party, which are refused because their inventory is the
        /// player's to manage and a mod tidying it would be taking something
        /// away rather than adding it. Some players outfit their family and
        /// their caravan masters by hand and want that respected past the edge
        /// of their own party; this moves the line, it does not bend a rule.
        ///
        /// Deliberately does not touch skill growth. That takes nothing from
        /// anyone -- it makes a man better at what he already does -- and a
        /// player who wants to choose his brother's armour rarely wants his
        /// brother to stop learning.
        /// </summary>
        public static bool ManageOwnClan = true;

        /// <summary>
        /// Item ids this campaign refuses to buy or grant, however good they
        /// are. Empty by default.
        ///
        /// This exists because the mod shops in a catalogue it does not own. A
        /// purchase engine that hunts for the best thing on the shelf is only
        /// as balanced as the shelf, and any mod can add an outlier -- so the
        /// player needs a way to say "not that one" without waiting on a
        /// release. ItemCatalog.IsIncendiary covers the class of outlier the
        /// game's own data identifies; this covers the rest.
        ///
        /// Matched case-insensitively against ItemObject.StringId, which is the
        /// id written in the mod's own XML.
        /// </summary>
        private static string[] _excludedItems = new string[0];

        /// <summary>
        /// Replaces the exclusion list from one line of text, for the options
        /// screen. Parsed by the same splitter settings.xml uses.
        /// </summary>
        public static void SetExcludedItems(string text)
        {
            _excludedItems = Split(text);
        }

        /// <summary>
        /// The exclusion list as entered, for whoever can check it against the
        /// catalogue. Settings cannot do that itself -- it is deliberately free
        /// of TaleWorlds types -- so it hands the ids out and ItemCatalog says
        /// which of them name a real item.
        ///
        /// A copy, because a caller holding the live array would see it change
        /// under him the next time the player pressed Done.
        /// </summary>
        public static string[] ExcludedIds()
        {
            string[] copy = new string[_excludedItems.Length];
            _excludedItems.CopyTo(copy, 0);
            return copy;
        }

        /// <summary>
        /// Modules whose gear a lord may not buy or be granted, by module id --
        /// the folder name under Modules, which the census prints beside each
        /// module's title.
        ///
        /// The coarse instrument beside ExcludedItems' fine one. A gear pack
        /// ships hundreds of pieces and a player who wants it out of his lords'
        /// hands should not have to name them one at a time; see ModuleRules
        /// for the report that asked for this and for why the unit is the
        /// module rather than a list of mod names kept in the source.
        ///
        /// Empty by default, and empty costs nothing: with nothing named here
        /// ItemModules never reads a file.
        /// </summary>
        private static string[] _excludedModules = new string[0];

        /// <summary>
        /// Replaces the excluded-module list from one line of text, for the
        /// options screen. Same splitter as everything else.
        /// </summary>
        public static void SetExcludedModules(string text)
        {
            _excludedModules = Split(text);
        }

        /// <summary>
        /// The excluded modules as entered. A copy, for the same reason
        /// ExcludedIds hands one out.
        /// </summary>
        public static string[] ExcludedModuleIds()
        {
            string[] copy = new string[_excludedModules.Length];
            _excludedModules.CopyTo(copy, 0);
            return copy;
        }

        /// <summary>Whether this module's gear is excluded.</summary>
        public static bool IsModuleExcluded(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId)) return false;

            for (int i = 0; i < _excludedModules.Length; i++)
            {
                if (string.Equals(_excludedModules[i], moduleId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Excludes or restores one module, which is what a tick box does.
        ///
        /// A list rather than a set because the same list is written by hand in
        /// settings.xml, where order and spelling are the player's. Adding
        /// rebuilds it; so does removing. Both run when somebody clicks, not in
        /// any loop, so the copying costs nothing worth avoiding.
        /// </summary>
        public static void SetModuleExcluded(string moduleId, bool excluded)
        {
            if (string.IsNullOrEmpty(moduleId)) return;
            if (IsModuleExcluded(moduleId) == excluded) return;

            List<string> next = new List<string>();
            for (int i = 0; i < _excludedModules.Length; i++)
            {
                if (!string.Equals(_excludedModules[i], moduleId, StringComparison.OrdinalIgnoreCase))
                {
                    next.Add(_excludedModules[i]);
                }
            }

            if (excluded) next.Add(moduleId);

            _excludedModules = next.ToArray();
        }

        /// <summary>
        /// The exclusion list as one line, which is what a text box shows and
        /// what settings.xml holds. Joined the way Split expects to read it
        /// back, so a value that makes a round trip through the screen comes
        /// out as it went in.
        /// </summary>
        public static string ExcludedItemsText()
        {
            return string.Join(", ", _excludedItems);
        }

        /// <summary>Whether the player has struck this item from his campaign.</summary>
        public static bool IsExcluded(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            for (int i = 0; i < _excludedItems.Length; i++)
            {
                if (string.Equals(_excludedItems[i], itemId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Loads settings.xml if it is there. Called once, from OnSubModuleLoad.
        /// </summary>
        public static void Load()
        {
            string path = FilePath();
            if (path == null || !File.Exists(path))
            {
                ModLog.Info("SETTINGS no settings.xml; using defaults");
                return;
            }

            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(path);

                XmlNode root = document.DocumentElement;
                if (root == null) return;

                EnableRepair = Flag(root, "EnableRepair", EnableRepair);
                EnableSkillGrowth = Flag(root, "EnableSkillGrowth", EnableSkillGrowth);
                StartLordsOnCurve = Flag(root, "StartLordsOnCurve", StartLordsOnCurve);
                EnablePurchases = Flag(root, "EnablePurchases", EnablePurchases);
                EnableLogging = Flag(root, "EnableLogging", EnableLogging);
                ManageOwnClan = Flag(root, "ManageOwnClan", ManageOwnClan);
                EnableCaptureLoss = Flag(root, "EnableCaptureLoss", EnableCaptureLoss);

                ClanWeight = Number(root, "ClanWeight", ClanWeight, 0f, 10f);
                SkillWeight = Number(root, "SkillWeight", SkillWeight, 0f, 10f);
                SkillPerTier = Whole(root, "SkillPerTier", SkillPerTier, 1, 400);
                MinimumTier = Whole(root, "MinimumTier", MinimumTier, 1, 6);
                DominanceMargin = Whole(root, "DominanceMargin", DominanceMargin, 0, 300);

                SpendingShare = Number(root, "SpendingShare", SpendingShare, 0f, 1f);
                ReserveMultiplier = Number(root, "ReserveMultiplier", ReserveMultiplier, 0f, 10f);
                ShopChancePerVisit = Number(root, "ShopChancePerVisit", ShopChancePerVisit, 0f, 1f);
                ShoppingCulture = Core.CultureChoices.Parse(Text(root, "ShoppingCulture"), ShoppingCulture);
                OwnCultureArmorOnly = Flag(root, "OwnCultureArmorOnly", OwnCultureArmorOnly);
                OwnCultureWeaponsOnly = Flag(root, "OwnCultureWeaponsOnly", OwnCultureWeaponsOnly);
                RobberyRate = Number(root, "RobberyRate", RobberyRate, 0f, 2f);
                CaravanGearShare = Number(root, "CaravanGearShare", CaravanGearShare, 0f, 1f);

                _excludedItems = List(root, "ExcludedItems");
                _excludedModules = List(root, "ExcludedModules");

                ModLog.Enabled = EnableLogging;
                ModLog.Info("SETTINGS loaded from " + path);
                ModLog.Info("SETTINGS " + Describe());
            }
            catch (Exception error)
            {
                // Defaults are already in the fields, so a broken file simply
                // means the mod behaves as shipped.
                ModLog.Error("SETTINGS could not read " + path + ": "
                             + error.GetType().Name + " " + error.Message + "; using defaults");
            }
        }

        /// <summary>Everything, on one line, for the log.</summary>
        public static string Describe()
        {
            return "repair=" + EnableRepair
                   + " ownClan=" + ManageOwnClan
                   + " growth=" + EnableSkillGrowth
                   + " startCurve=" + StartLordsOnCurve
                   + " purchases=" + EnablePurchases
                   + " clanWeight=" + ClanWeight
                   + " skillWeight=" + SkillWeight
                   + " skillPerTier=" + SkillPerTier
                   + " minimumTier=" + MinimumTier
                   + " dominanceMargin=" + DominanceMargin
                   + " spendingShare=" + SpendingShare
                   + " reserveMultiplier=" + ReserveMultiplier
                   + " shopChance=" + ShopChancePerVisit
                   + " shoppingCulture=" + Core.CultureChoices.NameOf(ShoppingCulture)
                   + " ownCultureArmorOnly=" + OwnCultureArmorOnly
                   + " ownCultureWeaponsOnly=" + OwnCultureWeaponsOnly
                   + " captureLoss=" + EnableCaptureLoss
                   + " robberyRate=" + RobberyRate
                   + " caravanGearShare=" + CaravanGearShare
                   + " excludedItems=" + _excludedItems.Length
                   + " excludedModules=" + _excludedModules.Length;
        }

        /// <summary>
        /// settings.xml beside the compiled dll, which is where a player looking
        /// for it will look. Null if the location cannot be worked out.
        /// </summary>
        private static string FilePath()
        {
            try
            {
                string dll = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(dll)) return null;

                string folder = Path.GetDirectoryName(dll);
                if (string.IsNullOrEmpty(folder)) return null;

                // bin/Win64_Shipping_Client -> the module root, so the file sits
                // next to SubModule.xml rather than buried beside a binary.
                DirectoryInfo bin = new DirectoryInfo(folder);
                if (bin.Parent != null && bin.Parent.Parent != null)
                {
                    return Path.Combine(bin.Parent.Parent.FullName, "settings.xml");
                }
                return Path.Combine(folder, "settings.xml");
            }
            catch
            {
                return null;
            }
        }

        private static string Text(XmlNode root, string name)
        {
            XmlNode node = root.SelectSingleNode(name);
            return node == null ? null : node.InnerText;
        }

        /// <summary>
        /// A list of ids from one element, separated by commas or whitespace so
        /// the player may write it on one line or on many without being told
        /// which. Missing or empty gives an empty array, never null.
        /// </summary>
        private static string[] List(XmlNode root, string name)
        {
            return Split(Text(root, name));
        }

        /// <summary>
        /// One line of ids into an array, separated by commas or whitespace so
        /// the player may write it either way without being told which. Missing
        /// or empty gives an empty array, never null.
        ///
        /// Shared with the options screen through SetExcludedItems, so the file
        /// and MCM cannot disagree about what "a, b" means.
        /// </summary>
        private static string[] Split(string text)
        {
            if (string.IsNullOrEmpty(text)) return new string[0];

            char[] separators = new char[] { ',', ' ', (char)9, (char)13, (char)10 };
            string[] parts = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
            return parts;
        }

        private static bool Flag(XmlNode root, string name, bool fallback)
        {
            string text = Text(root, name);
            if (string.IsNullOrEmpty(text)) return fallback;

            bool value;
            return bool.TryParse(text.Trim(), out value) ? value : fallback;
        }

        /// <summary>
        /// A number, clamped to a range that keeps the mod sane. The clamp is
        /// not decoration: a spending share of 50 or a skillPerTier of 0 would
        /// not crash anything, it would quietly produce nonsense that looks like
        /// a bug in the engine.
        /// </summary>
        private static float Number(XmlNode root, string name, float fallback, float lowest, float highest)
        {
            string text = Text(root, name);
            if (string.IsNullOrEmpty(text)) return fallback;

            float value;
            if (!float.TryParse(text.Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                return fallback;
            }

            if (value < lowest) return lowest;
            if (value > highest) return highest;
            return value;
        }

        private static int Whole(XmlNode root, string name, int fallback, int lowest, int highest)
        {
            string text = Text(root, name);
            if (string.IsNullOrEmpty(text)) return fallback;

            int value;
            if (!int.TryParse(text.Trim(), System.Globalization.NumberStyles.Integer,
                              System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                return fallback;
            }

            if (value < lowest) return lowest;
            if (value > highest) return highest;
            return value;
        }
    }
}
