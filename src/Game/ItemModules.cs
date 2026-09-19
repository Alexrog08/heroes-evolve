using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.ObjectSystem;
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
    /// Who pays for the scan, and when. Refuses asks one array-length question
    /// first, so a campaign with nothing excluded never reads a file on account
    /// of an item. Two callers do make it happen: the census, and the options
    /// screen MCM builds at load, which needs the list of installed gear mods
    /// to draw a tick box for each. So a player with MCM pays it once while the
    /// campaign is loading and a player without it pays nothing until he asks.
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

        /// <summary>
        /// One installed module that ships gear: what to call it on screen,
        /// what to call it in settings.xml, and how much it brought.
        /// </summary>
        public struct GearModule
        {
            public string Id;
            public string Name;
            public int Items;
        }

        private static Dictionary<string, string> _titleOf;
        private static HashSet<string> _official;
        private static Dictionary<string, string> _moduleOf;
        private static Dictionary<string, int> _declaredBy;
        private static List<string> _installed;
        private static List<GearModule> _gear;

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
            _titleOf = null;
            _official = null;
            _gear = null;
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

        /// <summary>
        /// The mods a player might actually want to keep out: installed, not
        /// TaleWorlds' own, and shipping at least one piece of gear.
        ///
        /// This is what the options screen draws a tick box from, and every
        /// condition here exists to keep that list short. Somebody running four
        /// hundred mods should see the handful that dress his lords, not four
        /// hundred names.
        ///
        /// Official modules are left out with the game's own
        /// GetOfficialModuleIds rather than a list kept here, because a tick
        /// box marked SandBoxCore would empty the world of gear.
        ///
        /// And gear means gear. Counting item elements was the first version
        /// and it was wrong: banners are items, so are trade goods, so are
        /// sheep -- 46, 23 and 7 of them in the base game's own files -- so a
        /// mod that adds nothing but banners would have appeared in this list
        /// looking exactly like an armoury. What is counted now is what a lord
        /// could actually wear or wield, read off the items the game has
        /// loaded rather than off the XML, so an item that failed to load does
        /// not vote.
        ///
        /// Null when the catalogue is not loaded yet, which is not the same
        /// answer as "no mods" and must not be cached as though it were.
        /// </summary>
        public static List<GearModule> GearModules()
        {
            Scan();

            if (_gear == null) _gear = BuildGearList();
            return _gear;
        }

        /// <summary>
        /// Whether a lord could wear or wield this, which is the only kind of
        /// item excluding a module can affect.
        ///
        /// Listed by what is in rather than what is out. The enum grows between
        /// releases and a mod may add types of its own; a list of exclusions
        /// would quietly start counting whatever appeared next, and this list
        /// simply would not.
        /// </summary>
        private static bool IsGear(ItemObject item)
        {
            if (item == null) return false;

            ItemObject.ItemTypeEnum type = item.ItemType;
            if (ItemCatalog.IsArmorSlot(type)) return true;

            return type == ItemObject.ItemTypeEnum.OneHandedWeapon
                || type == ItemObject.ItemTypeEnum.TwoHandedWeapon
                || type == ItemObject.ItemTypeEnum.Polearm
                || type == ItemObject.ItemTypeEnum.Bow
                || type == ItemObject.ItemTypeEnum.Crossbow
                || type == ItemObject.ItemTypeEnum.Thrown
                || type == ItemObject.ItemTypeEnum.Arrows
                || type == ItemObject.ItemTypeEnum.Bolts
                || type == ItemObject.ItemTypeEnum.Shield
                || type == ItemObject.ItemTypeEnum.Horse
                || type == ItemObject.ItemTypeEnum.HorseHarness;
        }

        /// <summary>
        /// Counts each module's gear off the loaded catalogue, and returns null
        /// while there is no catalogue to count.
        /// </summary>
        private static List<GearModule> BuildGearList()
        {
            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            if (all == null || all.Count == 0) return null;

            Dictionary<string, int> gear = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (!IsGear(item)) continue;

                string module;
                if (item.StringId == null || !_moduleOf.TryGetValue(item.StringId, out module)) continue;
                if (_official.Contains(module)) continue;

                int seen;
                gear.TryGetValue(module, out seen);
                gear[module] = seen + 1;
            }

            // In the order the launcher loads them, which is the order the
            // player arranged and therefore the one he can find a name in.
            List<GearModule> list = new List<GearModule>();
            for (int i = 0; i < _installed.Count; i++)
            {
                string id = _installed[i];

                int items;
                if (!gear.TryGetValue(id, out items) || items <= 0) continue;

                GearModule entry = new GearModule();
                entry.Id = id;
                entry.Name = _titleOf.ContainsKey(id) ? _titleOf[id] : id;
                entry.Items = items;
                list.Add(entry);
            }

            return list;
        }

        private static void Scan()
        {
            if (_moduleOf != null) return;

            _moduleOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _declaredBy = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _installed = new List<string>();
            _titleOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _official = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (string id in ModuleHelper.GetOfficialModuleIds()) _official.Add(id);

                foreach (ModuleInfo info in ModuleHelper.GetActiveModules())
                {
                    if (info == null || string.IsNullOrEmpty(info.Id)) continue;

                    _installed.Add(info.Id);
                    _titleOf[info.Id] = string.IsNullOrEmpty(info.Name) ? info.Id : info.Name;
                    ReadModule(info.Id, info.FolderPath);
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
