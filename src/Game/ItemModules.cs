using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// Which module each item came from, so a player can keep a whole gear pack
    /// out of his campaign with one name instead of three hundred item ids.
    /// See ModuleRules for why the module is the right unit.
    ///
    /// Read from the modules' own files rather than asked of the game, because
    /// the game does not keep it: ItemObject remembers what it is, not where it
    /// was declared. So this opens every XML under every active module's
    /// ModuleData, reads the first few hundred bytes, and only parses the ones
    /// whose root element is Items or CraftedItems. Measured on a stock
    /// installation that is 3,606 files sniffed to find 15 worth reading, about
    /// 1.4 MB of the 347 MB those folders hold, in well under a second.
    ///
    /// The modules' own SubModule.xml manifests would be the obvious place to
    /// look instead, and they cannot be trusted for this: War Sails ships 205
    /// items in ModuleData/items.xml and declares no Items node at all. The
    /// root element never lies.
    ///
    /// Nothing is scanned until something is excluded. With an empty list --
    /// which is the default and what nearly every campaign runs -- this class
    /// costs one array-length check per item and never touches the disk. The
    /// census pays for the scan itself, because listing what is installed is
    /// how a player learns the names he can type.
    /// </summary>
    public static class ItemModules
    {
        /// <summary>
        /// An item element and its id. Non-greedy up to the id so an attribute
        /// declared before it does not swallow the match, and \b so that
        /// multiplayer_item_id and its like are not mistaken for the id itself.
        /// The character class crosses newlines on its own: these files put
        /// every attribute on a line of its own.
        /// </summary>
        private static readonly Regex ItemId =
            new Regex("<(?:Item|CraftedItem)\\b[^>]*?\\bid=\"([^\"]+)\"", RegexOptions.Compiled);

        private static Dictionary<string, string> _moduleOf;
        private static Dictionary<string, int> _declaredBy;
        private static List<string> _installed;

        /// <summary>
        /// Forgets the scan. Called where ItemCatalog forgets its own, since a
        /// player who has just changed the exclusion list is the one person
        /// certain to want the answer recomputed.
        /// </summary>
        public static void ResetSession()
        {
            _moduleOf = null;
            _declaredBy = null;
            _installed = null;
        }

        /// <summary>
        /// Whether this item belongs to a module the player has excluded.
        ///
        /// The first line is the one that matters for cost: asked of every item
        /// on every shelf, and answered without work whenever nothing is
        /// excluded.
        /// </summary>
        public static bool Refuses(ItemObject item)
        {
            if (item == null) return false;

            // Asked once and held, because ExcludedModuleIds hands out a fresh
            // copy on every call and this runs per item per shelf. Two calls
            // here were two arrays per item.
            string[] excluded = Settings.ExcludedModuleIds();
            if (!ModuleRules.Any(excluded)) return false;

            return ModuleRules.Refuses(ModuleOf(item.StringId), excluded);
        }

        /// <summary>The module that declared this item id, or null if none did.</summary>
        public static string ModuleOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            Scan();

            string module;
            return _moduleOf.TryGetValue(itemId, out module) ? module : null;
        }

        /// <summary>
        /// Every active module and how many items it declares, for the census.
        ///
        /// This is the discovery half of the feature. A player cannot exclude a
        /// module whose id he does not know, and the id is the folder name
        /// rather than the title on the Workshop page, so printing the two side
        /// by side is what makes the setting usable at all. A module declaring
        /// nothing is printed too, and says so: either it adds no gear, or it
        /// adds gear from code, which this cannot exclude.
        /// </summary>
        public static string Describe()
        {
            Scan();

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < _installed.Count; i++)
            {
                string id = _installed[i];

                int declared;
                _declaredBy.TryGetValue(id, out declared);

                if (text.Length > 0) text.Append(' ');
                text.Append(id).Append('=').Append(declared);
            }

            return text.Length > 0 ? text.ToString() : "none";
        }

        private static void Scan()
        {
            if (_moduleOf != null) return;

            _moduleOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _declaredBy = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _installed = new List<string>();

            try
            {
                foreach (ModuleInfo info in ModuleHelper.GetActiveModules())
                {
                    if (info == null) continue;

                    string id = info.Id;
                    if (string.IsNullOrEmpty(id)) continue;

                    _installed.Add(id);
                    ReadModule(id, info.FolderPath);
                }
            }
            catch (Exception e)
            {
                // A scan that cannot finish must leave the campaign playable:
                // an empty map refuses nothing, which is the same as the
                // default. Said out loud, because a player who excluded a
                // module and saw no change deserves to know why.
                ModLog.Info("MODULES scan failed, nothing will be excluded: " + e.Message);
            }
        }

        private static void ReadModule(string moduleId, string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;

            string data = Path.Combine(folder, "ModuleData");
            if (!Directory.Exists(data)) return;

            string[] files;
            try
            {
                files = Directory.GetFiles(data, "*.xml", SearchOption.AllDirectories);
            }
            catch
            {
                return;
            }

            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    if (!DeclaresItems(files[i])) continue;

                    foreach (Match m in ItemId.Matches(File.ReadAllText(files[i])))
                    {
                        string id = m.Groups[1].Value;
                        if (string.IsNullOrEmpty(id)) continue;

                        // First module to declare an id keeps it. Two modules
                        // declaring the same item means one is overriding the
                        // other, and the override is not what put the item in
                        // the world.
                        if (_moduleOf.ContainsKey(id)) continue;

                        _moduleOf[id] = moduleId;

                        int seen;
                        _declaredBy.TryGetValue(moduleId, out seen);
                        _declaredBy[moduleId] = seen + 1;
                    }
                }
                catch
                {
                    // One unreadable file must not cost the rest of the module.
                }
            }
        }

        /// <summary>
        /// Whether a file is worth reading in full: its root element is Items
        /// or CraftedItems. Four hundred bytes is room for the XML declaration,
        /// a byte-order mark and a comment before it.
        /// </summary>
        private static bool DeclaresItems(string path)
        {
            byte[] head = new byte[400];
            int read;

            using (FileStream stream = File.OpenRead(path))
            {
                read = stream.Read(head, 0, head.Length);
            }

            if (read <= 0) return false;

            string text = Encoding.UTF8.GetString(head, 0, read);
            return text.IndexOf("<Items", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("<CraftedItems", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
