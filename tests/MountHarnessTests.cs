namespace HeroLoadoutFixer.Tests
{
    /// <summary>
    /// Pins the harness/mount family-compatibility rule from Fix 1 of the
    /// final review pass: a harness is compatible with a mount only when
    /// ItemObject.ArmorComponent.FamilyType (the harness) equals
    /// ItemObject.HorseComponent.Monster.FamilyType (the mount) -- both
    /// confirmed to exist under exactly those names (both System.Int32) by
    /// reflecting the installed TaleWorlds.Core.dll before writing
    /// ItemCatalog.FindBestHarness (see the fix report).
    ///
    /// ItemCatalog.FindBestHarness itself cannot be exercised here: it
    /// references TaleWorlds.Core/CampaignSystem/Library/ObjectSystem, none
    /// of which are available to CoreTests.exe (see build/build-tests.ps1 --
    /// only src/Core and tests/ are compiled into it, never src/Game). So
    /// this test mirrors the one-line contract -- family compatibility is a
    /// plain int equality, nothing more -- as a local, pure function, the
    /// same technique TierVocabularyTests uses for the tier-vocabulary seam.
    /// This pins the design decision (and would catch, say, a mirror that
    /// drifted to inequality or to always-true) but it cannot catch
    /// ItemCatalog.cs itself reading the wrong property or comparing the
    /// wrong pair of items -- there is no shared code between this mirror
    /// and that call site, by construction. If the two ever diverge, that
    /// is a signal to re-check both, not a compiler error.
    /// </summary>
    public static class MountHarnessTests
    {
        /// <summary>Mirrors ItemCatalog.FindBestHarness's compatibility test exactly.</summary>
        private static bool IsCompatibleHarness(int harnessFamilyType, int mountFamilyType)
        {
            return harnessFamilyType == mountFamilyType;
        }

        public static void RunAll()
        {
            Check.True(IsCompatibleHarness(0, 0), "matching family type zero is compatible (not treated as a missing-value sentinel here)");
            Check.True(IsCompatibleHarness(3, 3), "matching family type is compatible");
            Check.False(IsCompatibleHarness(3, 5), "a harness modelled for one family does not fit a mount of another");
            Check.False(IsCompatibleHarness(5, 3), "...and the reverse direction agrees");
        }
    }
}
