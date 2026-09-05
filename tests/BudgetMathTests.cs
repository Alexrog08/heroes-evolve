using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class BudgetMathTests
    {
        public static void RunAll()
        {
            Check.Equal(5000, BudgetMath.PartyGoldLowerThreshold, "threshold matches the game constant");

            Check.Equal(20000, BudgetMath.Reserve(4, 1.0f), "four parties reserve twenty thousand");
            Check.Equal(0, BudgetMath.Reserve(0, 1.0f), "no parties, no reserve");
            Check.Equal(10000, BudgetMath.Reserve(4, 0.5f), "multiplier halves the reserve");

            // Spec case 15: clan with 100k and four parties.
            int reserve = BudgetMath.Reserve(4, 1.0f);
            Check.Equal(85000, BudgetMath.Available(5000, 100000, 0, reserve), "first lord sees hero plus clan minus reserve");
            Check.Equal(35000, BudgetMath.Available(5000, 100000, 50000, reserve), "pending spend reduces what the next lord sees");
            Check.Equal(5000, BudgetMath.Available(5000, 100000, 90000, reserve), "at the reserve only hero gold remains");
            Check.Equal(5000, BudgetMath.Available(5000, 100000, 200000, reserve), "over-committed never goes negative");

            // A poor clan contributes nothing but the hero can still spend his own.
            Check.Equal(300, BudgetMath.Available(300, 1000, 0, reserve), "poor clan contributes nothing");

            // Spec case 17: the cost split.
            Check.Equal(30, (int)(BudgetMath.ClanShare(false, 2, 5000, 10000) * 100f), "rich hero pays most of it");
            Check.Equal(20, (int)(BudgetMath.ClanShare(false, 2, 5000, 50000) * 100f), "rich clan covers less as the hero is rich too");
            Check.Equal(70, (int)(BudgetMath.ClanShare(true, 0, 100, 100) * 100f), "poor clan leader is covered by the house");
            Check.Equal(40, (int)(BudgetMath.ClanShare(false, 3, 100, 100) * 100f), "established clan covers less than base");
            Check.Equal(60, (int)(BudgetMath.ClanShare(false, 0, 100, 100) * 100f), "base share");

            // The share never leaves the ten to eighty band.
            float low = BudgetMath.ClanShare(false, 6, 999999, 999999);
            Check.True(low >= 0.10f, "share never below ten percent");
            float high = BudgetMath.ClanShare(true, 0, 0, 0);
            Check.True(high <= 0.80f, "share never above eighty percent");

            // --- Extra boundary coverage beyond the brief's fixtures ---
            //
            // Finding: given the five fixed shares this rule table assigns
            // (0.60 base, 0.70 leader, 0.40 tier, 0.30 hero-rich, 0.20 clan-rich),
            // the last-applicable rule always wins (see the ordering block below)
            // and every one of those five literals already sits inside [0.10, 0.80].
            // No combination of the four booleans/thresholds can produce anything
            // outside {0.20, 0.30, 0.40, 0.60, 0.70}. That means the closing
            // `if (share < 0.10f)` / `if (share > 0.80f)` clamp in ClanShare can
            // never actually fire through this public signature: it is dead code
            // given these fixed values, not a reachable safety net. The two
            // "never below ten / above eighty" checks above hold trivially (0.20
            // and 0.70 both clear their bound with room to spare) and were never
            // going to catch a broken clamp either way. Recorded here rather than
            // faked with a test that pretends some input can reach the clamp.

            // Reserve: the brief only exercises warPartyCount 0 and 4, both
            // non-negative. A negative party count is a real input shape (a
            // clan record could be malformed, or a caller could pass a raw
            // difference); `warPartyCount <= 0` is what makes it share the
            // zero-parties branch instead of falling through to a negative
            // reserve. `== 0` would look identical for every case the brief
            // checks and still be wrong here.
            Check.Equal(0, BudgetMath.Reserve(-4, 1.0f), "negative party count takes the same zero-reserve branch as zero parties");

            // Same clamp shape, other parameter: the brief only exercises
            // multiplier 1.0 and 0.5, both non-negative, so the multiplier's own
            // negative guard is completely untested. Mirrors the case above.
            Check.Equal(0, BudgetMath.Reserve(4, -1.0f), "negative multiplier clamps to zero before multiplying, mirroring the party-count guard");

            // Available: the brief only ever passes non-negative heroGold (5000
            // or 300). A hero's personal gold should never go negative in a
            // real campaign, but Available takes it as a raw parameter with no
            // enforcement upstream, and the implementation explicitly guards it
            // -- untested by the brief's own fixtures.
            Check.Equal(80000, BudgetMath.Available(-500, 100000, 0, 20000), "negative hero gold clamps to zero before adding the clan's contribution");
            Check.Equal(0, BudgetMath.Available(-500, 1000, 0, 20000), "negative hero gold and an exhausted clan both clamp, leaving nothing");

            // ClanShare's heroGold threshold: the brief only tests 5000 (above)
            // and 100 (below), never anywhere near 2000 itself, and the rule
            // uses a strict '>'. Below/above alone cannot tell '>' from '>=';
            // only the boundary value can, since it is the one input where the
            // two operators disagree.
            Check.Equal(60, (int)(BudgetMath.ClanShare(false, 0, 1999, 100) * 100f), "one gold under the hero-wealth threshold: base share still applies");
            Check.Equal(60, (int)(BudgetMath.ClanShare(false, 0, 2000, 100) * 100f), "exactly on the hero-wealth threshold: '>' is strict, so it does not fire yet");
            Check.Equal(30, (int)(BudgetMath.ClanShare(false, 0, 2001, 100) * 100f), "one gold over the hero-wealth threshold: now it fires");

            // Same shape for the clanGold threshold at 40000, isolated from the
            // heroGold rule (heroGold stays at 100, well under its own threshold).
            Check.Equal(60, (int)(BudgetMath.ClanShare(false, 0, 100, 39999) * 100f), "one gold under the clan-wealth threshold: base share still applies");
            Check.Equal(60, (int)(BudgetMath.ClanShare(false, 0, 100, 40000) * 100f), "exactly on the clan-wealth threshold: '>' is strict, so it does not fire yet");
            Check.Equal(20, (int)(BudgetMath.ClanShare(false, 0, 100, 40001) * 100f), "one gold over the clan-wealth threshold: now it fires");

            // clanTier's own threshold ('>= 1') is never tested exactly at 1 by
            // the brief (it uses 0, 2, 3 and 6): this doubles as that boundary
            // case and as an ordering case. isClanLeader is also true here, so
            // it also proves the tier rule is applied *after* the leader rule
            // and overwrites it, per the design doc's "cuota del clan, por
            // orden de aplicacion" table (base -> leader -> tier -> hero ->
            // clan-wealth) -- a hero can be both his clan's leader and belong
            // to a clan of tier >= 1 at once, and the later rule must win.
            Check.Equal(40, (int)(BudgetMath.ClanShare(true, 1, 100, 100) * 100f), "clan tier exactly one both crosses the tier threshold and, applied after the leader rule, overwrites it");

            // Full ordering coverage: a hero can satisfy several ClanShare
            // conditions simultaneously, and the design doc specifies the rules
            // apply in a fixed order where a later match overwrites an earlier
            // one. Neither of the brief's own multi-condition fixtures ever
            // makes isClanLeader true alongside the others, so a bug that let
            // "is clan leader" dominate (e.g. applied last, or short-circuiting
            // an else-if chain) would ship unnoticed by the brief alone.
            Check.Equal(30, (int)(BudgetMath.ClanShare(true, 3, 3000, 100) * 100f), "leader, established clan and hero wealth all true: hero wealth is applied last among the three and wins");
            Check.Equal(20, (int)(BudgetMath.ClanShare(true, 3, 3000, 50000) * 100f), "all four conditions true at once: clan wealth is applied last of all and wins over leader, tier and hero wealth alike");
        }
    }
}
