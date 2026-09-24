using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MCM.Abstractions.Base.Global;
using MCM.Abstractions.FluentBuilder;
using MCM.Common;

namespace HeroesEvolve
{
    /// <summary>
    /// The one place in this mod that names a type from MCM.
    ///
    /// Which is the whole trick. MCM is optional, and a missing assembly does
    /// not announce itself politely -- it throws a TypeLoadException or a
    /// FileNotFoundException the moment the runtime prepares a method whose
    /// body mentions one of its types. The .NET runtime resolves that per
    /// method, not per class, so confining every mention to Attach's callees
    /// means a machine without MCM throws exactly once, inside a catch, and
    /// carries on with settings.xml as though this file did not exist.
    ///
    /// NoInlining is load-bearing rather than decorative: an inlined Bind would
    /// drag MCM's types into Attach itself, and the throw would then land
    /// outside the try instead of inside it.
    /// </summary>
    public static class McmBridge
    {
        private static bool _bound;

        /// <summary>
        /// Whether the mod-gear screen has been built. Separate from _bound
        /// because MCM refuses a second registration under the same id, and
        /// Attach runs again on every campaign load.
        /// </summary>
        private static bool _gearModsBound;

        /// <summary>
        /// Copies MCM's stored values into Settings and keeps them there.
        ///
        /// Called after Settings.Load, so MCM wins where both have an opinion.
        /// That is the right way round: settings.xml is the configuration for a
        /// player without MCM, and the in-game screen is what a player with MCM
        /// will actually believe he is looking at.
        /// </summary>
        public static void Attach()
        {
            try
            {
                // Not guarded by _bound, and that was a real bug. RegisterEvents
                // runs Settings.Load on every campaign load, which resets every
                // field to what settings.xml says; returning early here left the
                // second and every later campaign of a session running on the
                // file while the options screen showed something else. The
                // subscription is what must happen once, not the copy.
                Bind();
                ModLog.Info("MCM attached; the capture settings are live in the options screen");
            }
            catch (Exception ex)
            {
                // Not an error. The overwhelmingly likely cause is that the
                // player has not installed MCM, which is allowed.
                ModLog.Info("MCM not available (" + ex.GetType().Name + "); settings.xml stands alone");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Bind()
        {
            McmSettings settings = McmSettings.Instance;
            if (settings == null) throw new InvalidOperationException("MCM holds no settings instance");

            // Always copy; subscribe once. MCM keeps one settings instance for
            // the process, so subscribing again would fire Pull twice for every
            // slider the player moves.
            Pull(settings);

            if (!_bound)
            {
                settings.PropertyChanged += OnChanged;
                _bound = true;
            }

            // Its own catch. Without one, a fluent builder that threw would
            // reach Attach's handler and be logged as "MCM not available",
            // which is the sort of message that sends somebody reinstalling
            // MCM for an hour. The main screen is already bound by here, so
            // losing this one costs the tick boxes and nothing else.
            try
            {
                BindGearMods();
            }
            catch (Exception ex)
            {
                ModLog.Error("MCM mod-gear screen failed, the rest of the options stand: "
                             + ex.GetType().Name + " " + ex.Message);
            }
        }

        /// <summary>
        /// A second options screen: everything about gear a player wants kept
        /// out of his campaign, in one place.
        ///
        /// Two groups. A tick box per gear mod he has installed, and the list
        /// of single item ids that used to sit on the main screen under Gear
        /// limits. They answer the same question at two sizes -- a whole mod,
        /// or one sword another mod got wrong -- and reading them beside each
        /// other is how a player works out which one he needs.
        ///
        /// Because the first version of this feature was a text box asking for
        /// module ids, and ids are folder names a player would have had to go
        /// and find -- by running a console command and reading a log. That is
        /// a developer's answer to a player's problem. A mod is something you
        /// recognise by its name on its download page, so the screen shows that
        /// name and the player ticks it.
        ///
        /// It has to be built rather than declared because nobody knows at
        /// compile time which mods are installed, and MCM's attribute screen is
        /// a fixed class. Its fluent builder exists for exactly this.
        ///
        /// A separate screen rather than a group on the first one, which is not
        /// a preference: the attribute-driven settings object and a built one
        /// are two different containers in MCM and cannot be merged. It is
        /// registered whether or not any gear mod is installed, because the
        /// item box belongs to a player who has none just as much; MCM draws no
        /// group for an empty one.
        ///
        /// Every control reads and writes Settings directly through ProxyRef,
        /// so this screen owns no state of its own and cannot drift from the
        /// lists settings.xml carries for players without MCM.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BindGearMods()
        {
            if (_gearModsBound) return;

            List<ItemModules.GearModule> mods = ItemModules.GearModules();

            // Null is the catalogue saying it is not loaded yet, which is a
            // different answer from an empty list and must not be remembered as
            // one. Attach runs again on the next campaign load, and by then it
            // will be.
            if (mods == null) return;

            _gearModsBound = true;

            ISettingsBuilder builder = BaseSettingsBuilder
                .Create("HeroesEvolve_ExcludedGear", "Heroes Evolve - Excluded gear")
                .SetFormat("xml")
                .SetFolderName("HeroesEvolve")
                .SetSubFolder("ExcludedGear");

            builder.CreateGroup("Mods whose gear lords never get", group =>
            {
                group.SetGroupOrder(0);

                // Empty when he runs no gear mods, and MCM simply draws no
                // group. The page is still worth registering for the box below,
                // which is why this no longer returns early on an empty list.
                for (int i = 0; i < mods.Count; i++)
                {
                    // Copied out of the loop before the closure takes it. The
                    // language makes this safe on its own for foreach; an index
                    // loop it does not, and this is the one place where getting
                    // it wrong would tick every box at once.
                    string id = mods[i].Id;
                    string name = mods[i].Name;
                    int items = mods[i].Items;

                    group.AddBool(id, name,
                        new ProxyRef<bool>(
                            () => Settings.IsModuleExcluded(id),
                            value => ExcludeModule(id, value)),
                        box => box.SetHintText(
                            "Ticked, your lords never buy or receive this mod's "
                            + items + " items. The mod itself is untouched: you and your"
                            + " troops keep it. Folder name: " + id + "."));
                }
            });

            builder.CreateGroup("Single items", group =>
            {
                group.SetGroupOrder(1);
                group.AddText("ExcludedItems", "Items lords never get",
                    new ProxyRef<string>(
                        () => Settings.ExcludedItemsText(),
                        value => ExcludeItems(value)),
                    box => box.SetHintText(
                        "Item ids separated by commas: never bought, never given in a kit."
                        + " For the odd piece another mod got wrong, when excluding the whole"
                        + " mod above would be too much. Incendiaries are already refused."));
            });

            builder.BuildAsGlobal().Register();
            ModLog.Info("MCM excluded-gear screen built; " + mods.Count + " installed gear mods listed");
        }

        /// <summary>
        /// One tick box moved. The catalogue's scan of what is on sale was made
        /// against the old answer, so it goes, exactly as it does when the item
        /// exclusion list changes.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ExcludeModule(string moduleId, bool excluded)
        {
            Settings.SetModuleExcluded(moduleId, excluded);
            ItemCatalog.ResetSession();
        }

        /// <summary>
        /// The item list rewritten. Same reason for forgetting the scan: the
        /// best tier on sale was worked out against the old list.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ExcludeItems(string ids)
        {
            Settings.SetExcludedItems(ids);
            ItemCatalog.ResetSession();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OnChanged(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                McmSettings settings = sender as McmSettings;
                if (settings != null) Pull(settings);
            }
            catch (Exception ex)
            {
                ModLog.Error("MCM change failed: " + ex.GetType().Name + " " + ex.Message);
            }
        }

        /// <summary>
        /// Every field this screen owns, copied whole rather than by name.
        ///
        /// MCM raises PropertyChanged for the property that moved, but reading
        /// all of them costs two assignments and cannot drift out of step with
        /// the screen the way a switch on the property name would.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Pull(McmSettings settings)
        {
            // What it looked like before, so a change can be reported rather
            // than merely made. Without this the options screen was a silent
            // partner: SETTINGS in force is written once per campaign load, so
            // anything moved afterwards left no trace at all and neither the
            // player nor anyone reading his log could tell a setting that had
            // failed to apply from one applied after the line was written.
            string before = Settings.Describe();

            Settings.EnableRepair = settings.EnableRepair;
            Settings.EnableSkillGrowth = settings.EnableSkillGrowth;
            Settings.StartLordsOnCurve = settings.StartLordsOnCurve;
            Settings.EnablePurchases = settings.EnablePurchases;
            Settings.EnableLogging = settings.EnableLogging;
            Settings.ManageOwnClan = settings.ManageOwnClan;

            Settings.SkillPerTier = settings.SkillPerTier;
            Settings.SkillWeight = settings.SkillWeight;
            Settings.ClanWeight = settings.ClanWeight;
            Settings.MinimumTier = settings.MinimumTier;
            Settings.DominanceMargin = settings.DominanceMargin;

            Settings.ShoppingCulture = Core.CultureChoices.FromIndex(
                settings.ShoppingCulture != null ? settings.ShoppingCulture.SelectedIndex : -1,
                Settings.ShoppingCulture);
            Settings.OwnCultureArmorOnly = settings.OwnCultureArmorOnly;
            Settings.OwnCultureWeaponsOnly = settings.OwnCultureWeaponsOnly;
            Settings.SpendingShare = settings.SpendingShare;
            Settings.ReserveMultiplier = settings.ReserveMultiplier;

            Settings.EnableCaptureLoss = settings.EnableCaptureLoss;
            Settings.RobberyRate = settings.RobberyRate;
            Settings.CaravanGearShare = settings.CaravanGearShare;

            // Two that are not plain fields, and both would have gone stale.
            //
            // The log's own switch lives on ModLog and was only ever set inside
            // Settings.Load, so toggling it here would have changed a field
            // nobody reads. And the exclusion list is held parsed, so it has to
            // be handed the text rather than the array.
            ModLog.Enabled = Settings.EnableLogging;

            // The best tier on sale was scanned against the old list. Forgotten
            // every time rather than only on a change, because swapping one id
            // for another leaves the settings description below the same.
            ItemCatalog.ResetSession();
            ItemModules.ResetSession();

            // Only when something actually moved. MCM does not raise a change
            // per property or per slider step: BaseSettingsContainer.SaveSettings
            // writes the file and then raises PropertyChanged exactly once, with
            // the property name "SAVE_TRIGGERED" -- which is also why Pull copies
            // every field rather than switching on the name, since the name never
            // identifies what changed. So this fires once per press of Done, and
            // the comparison keeps a press that changed nothing out of the log.
            string after = Settings.Describe();
            if (after == before) return;

            ModLog.Info("SETTINGS changed: " + after);
            ItemCatalog.ReportUnknownExclusions();
        }
    }
}
