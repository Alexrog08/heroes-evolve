using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class TierCeilingTests
    {
        public static void RunAll()
        {
            // Defaults from the spec: clan weight 0.5, skill weight 1.0, minimum 1.
            const float ClanW = 0.5f;
            const float SkillW = 1.0f;
            const int Min = 1;

            // Clan tier 3, skill 240 -> tierSkill 6 -> (3*0.5 + 6*1)/1.5 = 5.0 -> 5
            Check.Equal(5, TierCeiling.Compute(3, 240, ClanW, SkillW, Min), "clan 3 skill 240");

            // Clan tier 6, skill 80 -> tierSkill 2 -> (6*0.5 + 2*1)/1.5 = 3.33 -> 3
            Check.Equal(3, TierCeiling.Compute(6, 80, ClanW, SkillW, Min), "clan 6 skill 80");

            // Median lord: clan 4, skill 210 -> tierSkill 5 -> (2 + 5)/1.5 = 4.67 -> 5
            Check.Equal(5, TierCeiling.Compute(4, 210, ClanW, SkillW, Min), "median lord");

            // Fresh eighteen-year-old: clan 3, skill 20 -> tierSkill 0 -> (1.5 + 0)/1.5 = 1 -> 1
            Check.Equal(1, TierCeiling.Compute(3, 20, ClanW, SkillW, Min), "rookie stays at one");

            // The minimum floor lifts a result that would otherwise be lower.
            Check.Equal(3, TierCeiling.Compute(0, 0, ClanW, SkillW, 3), "minimum floor applies");

            // Skill weight zero means clan tier alone decides.
            Check.Equal(6, TierCeiling.Compute(6, 0, 1.0f, 0.0f, Min), "clan-only weighting");

            // Clan weight zero means skill alone decides.
            Check.Equal(6, TierCeiling.Compute(0, 240, 0.0f, 1.0f, Min), "skill-only weighting");

            // Never exceeds six even with absurd skill.
            Check.Equal(6, TierCeiling.Compute(6, 1000, ClanW, SkillW, Min), "capped at six");

            // Never below one even with a zero minimum.
            Check.Equal(1, TierCeiling.Compute(0, 0, ClanW, SkillW, 0), "floored at one");

            // Both weights zero is degenerate input; fall back to the minimum.
            Check.Equal(2, TierCeiling.Compute(5, 240, 0.0f, 0.0f, 2), "zero weights fall back to minimum");

            // --- Extra boundary coverage beyond the brief's fixtures ---
            // None of the ten fixtures above happen to land on a fresh tier
            // step, a genuine .5 rounding midpoint, or a case that isolates
            // the internal skill-tier cap from the final clamp. A broken
            // implementation can pass all ten and still be wrong; the cases
            // below each target one such gap.

            // Integer-division truncation of maxCombatSkill / SkillPerTier:
            // 79 stays in tier 1, 80 crosses into tier 2. Float division would
            // round 79/40 = 1.975 up to 2 and collide with the 80 case.
            Check.Equal(1, TierCeiling.Compute(0, 79, 0.0f, 1.0f, Min), "skill 79 truncates down, one short of the next tier step");
            Check.Equal(2, TierCeiling.Compute(0, 80, 0.0f, 1.0f, Min), "skill 80 lands exactly on the next tier step");

            // Exact .5 midpoint: (2*1 + 3*1)/2 = 2.5. The spec's blend must
            // round away from zero (2.5 -> 3); .NET's default banker's
            // rounding would round 2.5 down to the even 2 instead.
            Check.Equal(3, TierCeiling.Compute(2, 120, 1.0f, 1.0f, Min), "exact .5 blend rounds away from zero, not to even");

            // The min(6, skill/40) cap on tierSkill, isolated from the final
            // 1..6 clamp. The brief's own "capped at six" fixture uses clan
            // tier 6, so its blend already equals 6 even if this cap is
            // deleted (28/1.5 -> 19, still clamped to 6 at the end) -- it
            // cannot catch a missing cap. A low clan tier exposes it: capped,
            // (1*1 + 6*1)/2 = 3.5 -> 4; uncapped, (1*1 + 25*1)/2 = 13 -> 6.
            Check.Equal(4, TierCeiling.Compute(1, 1000, 1.0f, 1.0f, Min), "skill tier cap at six applies before blending, not only at the end");

            // Negative/nonsense inputs must clamp to zero before blending,
            // not merely rely on the final 1..6 clamp to hide the damage.
            // Each case picks weights so the un-clamped path lands on a
            // different in-range wrong answer rather than being masked by
            // the floor or ceiling.

            // Negative clan tier -> 0. Un-clamped: (-6*1 + 6*1)/2 = 0, which
            // the minimum floor would then push to 1 instead of 3.
            Check.Equal(3, TierCeiling.Compute(-6, 240, 1.0f, 1.0f, Min), "negative clan tier clamps to zero");

            // Negative combat skill -> 0. Un-clamped: skillTier -240/40 = -6,
            // (6*1 + -6*1)/2 = 0, floored to 1 instead of 3.
            Check.Equal(3, TierCeiling.Compute(6, -240, 1.0f, 1.0f, Min), "negative combat skill clamps to zero");

            // Negative clan weight -> 0. Un-clamped: totalWeight 0.5,
            // (4*-0.5 + 2*1)/0.5 = 0, floored to 1 instead of 2.
            Check.Equal(2, TierCeiling.Compute(4, 80, -0.5f, 1.0f, Min), "negative clan weight clamps to zero");

            // Negative skill weight -> 0. Un-clamped: totalWeight 0.5,
            // (2*1 + 6*-0.5)/0.5 = -2, floored to 1 instead of 2.
            Check.Equal(2, TierCeiling.Compute(2, 240, 1.0f, -0.5f, Min), "negative skill weight clamps to zero");

            // The hard 1..6 clamp must win even when what feeds it is itself
            // out of range, exercised two independent ways: an out-of-range
            // minimumTier, and a blend that overshoots six on its own (clan
            // tier is never capped on the way in, only floored at zero).
            Check.Equal(6, TierCeiling.Compute(0, 0, ClanW, SkillW, 10), "minimum tier above the ceiling still clamps to six");
            Check.Equal(6, TierCeiling.Compute(10, 240, 1.0f, 0.0f, Min), "an unrealistically high clan tier still clamps to six");
        }
    }
}
