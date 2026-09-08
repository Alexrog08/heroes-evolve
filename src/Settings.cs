using System;
using System.IO;
using System.Reflection;
using System.Xml;

namespace HeroLoadoutFixer
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

        /// <summary>Let lords buy better gear when they visit a town.</summary>
        public static bool EnablePurchases = true;

        /// <summary>Write hlf.log. Off costs nothing and writes nothing.</summary>
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
        /// Share of his wallet a hero may put into one purchase. This is the
        /// gate on item tier: at a tenth, a tier-6 piece needs a house holding
        /// some 430,000.
        /// </summary>
        public static float SpendingShare = 0.10f;

        /// <summary>
        /// Scales the gold held back to keep troops paid. 1.0 keeps exactly the
        /// game's own threshold per war party.
        /// </summary>
        public static float ReserveMultiplier = 1.0f;

        /// <summary>Chance a lord goes shopping when he enters a town.</summary>
        public static float ShopChancePerVisit = 0.25f;

        // --- Losing gear ----------------------------------------------------

        /// <summary>
        /// Lets a captor strip his prisoner. Off by default: it changes the
        /// feel of every defeat, and that should be a choice.
        /// </summary>
        public static bool EnableCaptureLoss = false;

        /// <summary>
        /// Scales how often a captor robs. Zero is the same as off; one runs
        /// the rules as written. See PlunderRules -- every weight in there is
        /// invented, so this is the dial that matters until a campaign has
        /// measured the rate.
        /// </summary>
        public static float PlunderChance = 1.0f;

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
                EnablePurchases = Flag(root, "EnablePurchases", EnablePurchases);
                EnableLogging = Flag(root, "EnableLogging", EnableLogging);
                EnableCaptureLoss = Flag(root, "EnableCaptureLoss", EnableCaptureLoss);

                ClanWeight = Number(root, "ClanWeight", ClanWeight, 0f, 10f);
                SkillWeight = Number(root, "SkillWeight", SkillWeight, 0f, 10f);
                SkillPerTier = Whole(root, "SkillPerTier", SkillPerTier, 1, 400);
                MinimumTier = Whole(root, "MinimumTier", MinimumTier, 1, 6);
                DominanceMargin = Whole(root, "DominanceMargin", DominanceMargin, 0, 300);

                SpendingShare = Number(root, "SpendingShare", SpendingShare, 0f, 1f);
                ReserveMultiplier = Number(root, "ReserveMultiplier", ReserveMultiplier, 0f, 10f);
                ShopChancePerVisit = Number(root, "ShopChancePerVisit", ShopChancePerVisit, 0f, 1f);
                PlunderChance = Number(root, "PlunderChance", PlunderChance, 0f, 5f);

                _excludedItems = List(root, "ExcludedItems");

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
                   + " growth=" + EnableSkillGrowth
                   + " purchases=" + EnablePurchases
                   + " clanWeight=" + ClanWeight
                   + " skillWeight=" + SkillWeight
                   + " skillPerTier=" + SkillPerTier
                   + " minimumTier=" + MinimumTier
                   + " dominanceMargin=" + DominanceMargin
                   + " spendingShare=" + SpendingShare
                   + " reserveMultiplier=" + ReserveMultiplier
                   + " shopChance=" + ShopChancePerVisit
                   + " captureLoss=" + EnableCaptureLoss
                   + " plunderChance=" + PlunderChance
                   + " excludedItems=" + _excludedItems.Length;
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
            string text = Text(root, name);
            if (string.IsNullOrEmpty(text)) return new string[0];

            char[] separators = new char[] { ',', ' ', '\t', '\r', '\n' };
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
