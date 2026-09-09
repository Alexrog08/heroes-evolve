using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
{
    public static class TierCeilingTests
    {
        public static void RunAll()
        {
            // Skill inputs are written as multiples of the step rather than as
            // raw numbers. The step is a calibration and has already been
            // remeasured once; these cases are about blending, truncation,
            // rounding and clamping, none of which should break when it moves
            // again.
            const int Step = TierCeiling.DefaultSkillPerTier;

            // The weights the blend was designed around. Production now passes
            // clanWeight 0 -- a campaign census found clan tier neither
            // correlates with a lord's gear nor separates the population, since
            // by midgame every clan is tier 4 or better -- but the blend itself
            // still has to be correct for any weights, so it is tested here for
            // the ones the spec named.
            const float ClanW = 0.5f;
            const float SkillW = 1.0f;
            const int Min = 1;

            // Clan tier 3, skillTier 6 -> (3*0.5 + 6*1)/1.5 = 5.0 -> 5
            Check.Equal(5, TierCeiling.Compute(3, 6 * Step, ClanW, SkillW, Min), "clan 3, six steps of skill");

            // Clan tier 6, skillTier 2 -> (6*0.5 + 2*1)/1.5 = 3.33 -> 3
            Check.Equal(3, TierCeiling.Compute(6, 2 * Step, ClanW, SkillW, Min), "clan 6, two steps of skill");

            // Clan 4, skillTier 5 -> (2 + 5)/1.5 = 4.67 -> 5
            Check.Equal(5, TierCeiling.Compute(4, 5 * Step, ClanW, SkillW, Min), "clan 4, five steps of skill");

            // Fresh eighteen-year-old: half a step is no step at all.
            Check.Equal(1, TierCeiling.Compute(3, Step / 2, ClanW, SkillW, Min), "rookie stays at one");

            // The minimum floor lifts a result that would otherwise be lower.
            Check.Equal(3, TierCeiling.Compute(0, 0, ClanW, SkillW, 3), "minimum floor applies");

            // Skill weight zero means clan tier alone decides.
            Check.Equal(6, TierCeiling.Compute(6, 0, 1.0f, 0.0f, Min), "clan-only weighting");

            // Clan weight zero means skill alone decides. This is what
            // production uses.
            Check.Equal(6, TierCeiling.Compute(0, 6 * Step, 0.0f, 1.0f, Min), "skill-only weighting");
            Check.Equal(3, TierCeiling.Compute(6, 3 * Step, 0.0f, 1.0f, Min),
                        "under skill-only weighting a great house buys a lord nothing");

            // Never exceeds six even with absurd skill.
            Check.Equal(6, TierCeiling.Compute(6, 100 * Step, ClanW, SkillW, Min), "capped at six");

            // Never below one even with a zero minimum.
            Check.Equal(1, TierCeiling.Compute(0, 0, ClanW, SkillW, 0), "floored at one");

            // Both weights zero is degenerate input; fall back to the minimum.
            Check.Equal(2, TierCeiling.Compute(5, 6 * Step, 0.0f, 0.0f, 2), "zero weights fall back to minimum");

            // --- Boundary coverage ---
            // None of the fixtures above lands on a fresh tier step, a genuine
            // .5 rounding midpoint, or a case that isolates the internal
            // skill-tier cap from the final clamp. A broken implementation can
            // pass all of them and still be wrong.

            // Integer-division truncation: one point short of a step stays in
            // the lower tier. Float division would round it up and collide with
            // the case below.
            Check.Equal(1, TierCeiling.Compute(0, 2 * Step - 1, 0.0f, 1.0f, Min),
                        "one point short of a step truncates down");
            Check.Equal(2, TierCeiling.Compute(0, 2 * Step, 0.0f, 1.0f, Min),
                        "landing exactly on a step crosses it");

            // Exact .5 midpoint: (2*1 + 3*1)/2 = 2.5. The blend must round away
            // from zero (2.5 -> 3); .NET's default banker's rounding would round
            // 2.5 down to the even 2 instead.
            Check.Equal(3, TierCeiling.Compute(2, 3 * Step, 1.0f, 1.0f, Min),
                        "exact .5 blend rounds away from zero, not to even");

            // The cap on skillTier, isolated from the final 1..6 clamp. A high
            // clan tier would hide a missing cap because the blend already
            // reaches 6; a low one exposes it. Capped: (1 + 6)/2 = 3.5 -> 4.
            // Uncapped it would be far higher and clamp to 6.
            Check.Equal(4, TierCeiling.Compute(1, 100 * Step, 1.0f, 1.0f, Min),
                        "the skill tier cap applies before blending, not only at the end");

            // Negative and nonsense inputs must clamp to zero before blending,
            // not merely rely on the final 1..6 clamp to hide the damage. Each
            // case picks weights so the unclamped path lands on a different
            // in-range wrong answer rather than being masked by a bound.
            Check.Equal(3, TierCeiling.Compute(-6, 6 * Step, 1.0f, 1.0f, Min), "negative clan tier clamps to zero");
            Check.Equal(3, TierCeiling.Compute(6, -6 * Step, 1.0f, 1.0f, Min), "negative combat skill clamps to zero");
            Check.Equal(2, TierCeiling.Compute(4, 2 * Step, -0.5f, 1.0f, Min), "negative clan weight clamps to zero");
            Check.Equal(2, TierCeiling.Compute(2, 6 * Step, 1.0f, -0.5f, Min), "negative skill weight clamps to zero");

            // The hard 1..6 clamp must win even when what feeds it is itself out
            // of range, two independent ways: an out-of-range minimumTier, and a
            // clan tier that is never capped on the way in, only floored.
            Check.Equal(6, TierCeiling.Compute(0, 0, ClanW, SkillW, 10), "minimum tier above the ceiling still clamps to six");
            Check.Equal(6, TierCeiling.Compute(10, 6 * Step, 1.0f, 0.0f, Min), "an unrealistic clan tier still clamps to six");
        }
    }
}
