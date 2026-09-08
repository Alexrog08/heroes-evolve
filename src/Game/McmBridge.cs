using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HeroLoadoutFixer
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
            if (_bound) return;

            try
            {
                Bind();
                _bound = true;
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

            Pull(settings);
            settings.PropertyChanged += OnChanged;
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
            Settings.EnableCaptureLoss = settings.EnableCaptureLoss;
            Settings.PlunderChance = settings.PlunderChance;
        }
    }
}
