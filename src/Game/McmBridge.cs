using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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

            Settings.ShopChancePerVisit = settings.ShopChancePerVisit;
            Settings.ShoppingCulture = Core.CultureChoices.FromIndex(
                settings.ShoppingCulture != null ? settings.ShoppingCulture.SelectedIndex : -1,
                Settings.ShoppingCulture);
            Settings.SpendingShare = settings.SpendingShare;
            Settings.ReserveMultiplier = settings.ReserveMultiplier;

            Settings.EnableCaptureLoss = settings.EnableCaptureLoss;
            Settings.PlunderChance = settings.PlunderChance;
            Settings.CaravanGearShare = settings.CaravanGearShare;

            // Two that are not plain fields, and both would have gone stale.
            //
            // The log's own switch lives on ModLog and was only ever set inside
            // Settings.Load, so toggling it here would have changed a field
            // nobody reads. And the exclusion list is held parsed, so it has to
            // be handed the text rather than the array.
            ModLog.Enabled = Settings.EnableLogging;
            Settings.SetExcludedItems(settings.ExcludedItems);

            // The best tier on sale was scanned against the old list. Forgotten
            // every time rather than only on a change, because swapping one id
            // for another leaves the settings description below the same.
            ItemCatalog.ResetSession();

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
