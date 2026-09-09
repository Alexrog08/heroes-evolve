using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    /// <summary>
    /// Pins the vocabulary boundary at the seam between TierCeiling (Core,
    /// 1-based: MinTier = 1 .. MaxTier = 6) and the game's ItemObject.Tier
    /// (0-based TaleWorlds.Core ItemTiers enum: Tier1 = 0 .. Tier6 = 5) --
    /// exactly where Critical 2 lived: ItemCatalog.PassesCommonFilters used
    /// to compare "(int)item.Tier > maxTier" raw, so every hero was allowed
    /// one tier above what they earned, and from ceiling 5 upward the filter
    /// excluded nothing at all.
    ///
    /// ItemCatalog itself cannot be exercised here: it references
    /// TaleWorlds.Core/CampaignSystem/Library/ObjectSystem, none of which are
    /// available to CoreTests.exe (see build/build-tests.ps1 -- only
    /// src/Core and tests/ are compiled into it, never src/Game). So this
    /// test does the two things that ARE possible without those types:
    ///
    ///   1. Pins TierCeiling's documented 1-based contract, so the vocabulary
    ///      on that side of the seam is nailed down by a test rather than
    ///      only by a doc comment.
    ///   2. Mirrors the exact conversion now in
    ///      ItemCatalog.PassesCommonFilters -- "(int)item.Tier + 1 > maxTier"
    ///      -- as a local, pure, int-only comparison (PassesTierCeiling
    ///      below), so the boundary itself has a regression test even though
    ///      the real ItemObject-typed call site cannot be linked into
    ///      CoreTests.exe. If ItemCatalog.cs and this local mirror ever
    ///      diverge, that is a signal to re-check both, not a compiler error
    ///      -- there is no shared code between them, by construction.
    /// </summary>
    public static class TierVocabularyTests
    {
        /// <summary>
        /// Local mirror of ItemCatalog.PassesCommonFilters' tier check.
        /// itemTierZeroBased is the game's 0-based ItemTiers value
        /// (Tier1 = 0 .. Tier6 = 5); maxTierOneBased is a TierCeiling.Compute
        /// result (1..6). True means the item passes the ceiling.
        /// </summary>
        private static bool PassesTierCeiling(int itemTierZeroBased, int maxTierOneBased)
        {
            return itemTierZeroBased + 1 <= maxTierOneBased;
        }

        public static void RunAll()
        {
            // --- Side 1 of the seam: TierCeiling's contract is 1-based. ---
            Check.Equal(1, TierCeiling.MinTier, "TierCeiling.MinTier is 1-based (Tier1 = 0 once converted), not 0");
            Check.Equal(6, TierCeiling.MaxTier, "TierCeiling.MaxTier is 1-based (Tier6 = 5 once converted), not 5");

            // The design spec's own censused case: clan tier 4, median combat
            // skill 210 -> ceiling 5 (see TierCeilingTests' "median lord").
            // This is the exact scenario Critical 2 named: at ceiling 5 the
            // unconverted comparison excluded nothing, so a median-skill lord
            // could be handed Tier6 (0-based 5) gear, the best in the game.
            const float ClanW = 0.5f;
            const float SkillW = 1.0f;
            const int Min = 1;
            int medianCeiling = TierCeiling.Compute(4, 210, ClanW, SkillW, Min);
            Check.Equal(5, medianCeiling, "median lord (clan 4, skill 210) still computes ceiling 5");

            // --- Side 2 of the seam: the converted comparison at that ceiling. ---
            //
            // ItemTiers.Tier6 = 5 (0-based) is one full tier above ceiling 5
            // (1-based) and must be rejected -- this is the item the bug let
            // through, priced by the design spec at ~476,000 denars.
            Check.False(PassesTierCeiling(5, medianCeiling), "ceiling 5 must reject a 0-based tier-5 (Tier6) item");

            // ItemTiers.Tier5 = 4 (0-based) is exactly at ceiling 5 and must
            // still be allowed -- the fix must not be an off-by-one in the
            // other direction.
            Check.True(PassesTierCeiling(4, medianCeiling), "ceiling 5 must allow a 0-based tier-4 (Tier5) item");

            // Boundary sweep across every ceiling 1..6: an item one full
            // 1-based tier above the ceiling must always be rejected, and an
            // item exactly at the ceiling must always be allowed -- including
            // ceiling 6, the old MaxTier, and ceiling 5, where the pre-fix
            // comparison stopped constraining anything at all.
            for (int ceiling = TierCeiling.MinTier; ceiling <= TierCeiling.MaxTier; ceiling++)
            {
                Check.False(PassesTierCeiling(ceiling, ceiling), "ceiling " + ceiling + " must reject the item one tier above it");
                Check.True(PassesTierCeiling(ceiling - 1, ceiling), "ceiling " + ceiling + " must allow an item exactly at it");
            }
        }
    }
}
