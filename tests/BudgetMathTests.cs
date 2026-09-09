using HeroesEvolve.Core;

namespace HeroesEvolve.Tests
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

            // Spec case 17: the cost split. ClanShare moved from an overwrite
            // chain to additive contributions (see BudgetMath.cs); every
            // expected value below was recomputed by hand for the new rule,
            // not copied from the old implementation.
            Check.Equal(35, (int)(BudgetMath.ClanShare(false, 2, 5000, 10000) * 100f), "established clan (+5) and rich hero (-30) net below base");
            Check.Equal(50, (int)(BudgetMath.ClanShare(false, 2, 5000, 50000) * 100f), "established clan (+5) and rich clan (+15) offset most of the rich-hero deduction (-30)");
            Check.Equal(70, (int)(BudgetMath.ClanShare(true, 0, 100, 100) * 100f), "poor clan leader is covered by the house");
            Check.Equal(65, (int)(BudgetMath.ClanShare(false, 3, 100, 100) * 100f), "established clan adds five points on top of base");
            Check.Equal(60, (int)(BudgetMath.ClanShare(false, 0, 100, 100) * 100f), "base share");

            // The share never leaves the ten to eighty band.
            float low = BudgetMath.ClanShare(false, 6, 999999, 999999);
            Check.True(low >= 0.10f, "share never below ten percent");
            float high = BudgetMath.ClanShare(true, 0, 0, 0);
            Check.True(high <= 0.80f, "share never above eighty percent");

            // --- Extra boundary coverage beyond the brief's fixtures ---
            //
            // Superseded finding: this comment used to record that the 10%-80%
            // clamp was dead code, because the old overwrite chain could only
            // ever return one of five fixed literals ({0.20, 0.30, 0.40, 0.60,
            // 0.70}), all comfortably inside the band. ClanShare is now
            // additive (see BudgetMath.cs), and that is no longer true: the
            // upper clamp is genuinely reachable, proved below by pinning the
            // exact combination that hits it. The lower clamp is still
            // unreachable today (minimum achievable is 0.30) -- see
            // BudgetMath.cs for why it is kept anyway -- and remains covered
            // only by the generic "never below ten percent" check above.

            // Finding 2 fixture: an overwrite chain let an almost-broke leader
            // of a rich clan (tier 0, so the tier bonus does not apply) end up
            // with LESS help (old: 0.20) than a well-off leader of a modest
            // clan (old: 0.30) -- backwards, since the two axes should add, not
            // compete. This pins the corrected relationship directly, rather
            // than just checking two numbers that happen to be right: whatever
            // the exact literals, the rich-clan leader must come out strictly
            // ahead of the modest-clan one.
            float brokeLeaderOfRichClan = BudgetMath.ClanShare(true, 0, 100, 50000);
            float wellOffLeaderOfModestClan = BudgetMath.ClanShare(true, 3, 3000, 10000);
            Check.True(brokeLeaderOfRichClan > wellOffLeaderOfModestClan,
                "an almost-broke leader of a rich clan gets a strictly higher clan share than a well-off leader of a modest clan");

            // Same brokeLeaderOfRichClan case, pinned to its exact value: raw
            // sum is 0.60 base + 0.10 leader + 0.15 clan-wealth = 0.85 (tier is
            // 0, so the +0.05 tier bonus does not apply, and heroGold is 100,
            // so the -0.30 hero-wealth deduction does not apply either). The
            // 80% clamp must bring that down to exactly 0.80, not leave it at
            // 0.85 -- proving the upper clamp is live, not dead code.
            Check.Equal(80, (int)(brokeLeaderOfRichClan * 100f), "upper clamp brings the 0.85 raw sum down to exactly 0.80");

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

            // Finding 1: pendingClanSpend and reserve used to feed the same
            // subtraction unguarded, even though heroGold was already clamped.
            // A negative value on either one flips the subtraction into
            // addition and inflates the clan's apparent contribution above
            // what it actually holds -- exactly what the reserve exists to
            // prevent. A clan holding 1000 must never appear to offer 1500.
            Check.Equal(1000, BudgetMath.Available(0, 1000, -500, 0), "negative pending clan spend clamps to zero instead of inflating the clan's contribution above its actual 1000 gold");
            Check.Equal(1000, BudgetMath.Available(0, 1000, 0, -500), "negative reserve clamps to zero instead of inflating the clan's contribution above its actual 1000 gold");
            Check.Equal(1000, BudgetMath.Available(0, 1000, -500, -500), "negative pending clan spend and negative reserve both clamp at once, still capped at the clan's actual 1000 gold");

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
            Check.Equal(75, (int)(BudgetMath.ClanShare(false, 0, 100, 40001) * 100f), "one gold over the clan-wealth threshold: the +15 clan-wealth contribution now applies on top of base");

            // clanTier's own threshold ('>= 1') is never tested exactly at 1 by
            // the brief (it uses 0, 2, 3 and 6): this doubles as that boundary
            // case and as an additive-combination case. isClanLeader is also
            // true here, so this also proves the tier bonus (+5) and the
            // leader bonus (+10) both apply together instead of one
            // overwriting the other: base 0.60 + leader 0.10 + tier 0.05 =
            // 0.75, not just one of the two ten/five-point bonuses alone.
            Check.Equal(75, (int)(BudgetMath.ClanShare(true, 1, 100, 100) * 100f), "clan tier exactly one crosses the tier threshold and adds on top of the leader bonus, rather than overwriting it");

            // Full additive-combination coverage: a hero can satisfy several
            // ClanShare conditions simultaneously, and now that the rule is
            // additive every one of them must contribute, not just whichever
            // was checked last. Neither of the brief's own multi-condition
            // fixtures ever makes isClanLeader true alongside the others, so a
            // bug that dropped or double-applied one contribution when several
            // fire together would ship unnoticed by the brief alone.
            Check.Equal(45, (int)(BudgetMath.ClanShare(true, 3, 3000, 100) * 100f), "leader, established clan and rich hero all at once: 0.60 base + 0.10 leader + 0.05 tier - 0.30 hero-wealth = 0.45");
            Check.Equal(60, (int)(BudgetMath.ClanShare(true, 3, 3000, 50000) * 100f), "all four conditions true at once: 0.60 base + 0.10 leader + 0.05 tier + 0.15 clan-wealth - 0.30 hero-wealth = 0.60");
        }
    }
}
