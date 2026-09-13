using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// What a lord may spend on gear today, and who pays which half.
    ///
    /// The arithmetic lives in BudgetMath, which is pure and tested. This is the
    /// adapter that feeds it real heroes, plus the one piece of state the model
    /// needs: a per-clan record of what the house has already put towards gear
    /// today. It lives in memory and is cleared every morning, so it adds
    /// nothing to the save.
    ///
    /// Two things about the game's economy shape the whole class.
    ///
    /// First, `Clan.Gold` IS `Clan.Leader.Gold` -- verified by disassembly, the
    /// getter is get_Leader followed by Hero::get_Gold. There is no clan
    /// treasury. So for a clan leader, "his gold" and "the clan's gold" are one
    /// pot, and adding them is counting the same denars twice. That bug already
    /// bit once in the diagnostics.
    ///
    /// Second, and the reason a clan purse must be involved at all: campaign
    /// income is paid to the leader, and DailyTickHero only pays notables. A
    /// lord who does not lead never accumulates gold of his own, so a wallet
    /// built from Hero.Gold alone -- which is what NoblesBuyStuff does -- leaves
    /// most of the map unable to buy anything ever.
    /// </summary>
    public sealed class BudgetService
    {
        // The shipped defaults used to be declared here as well as in Settings,
        // and the second copy went stale the moment the live share moved from a
        // tenth to three. Nothing read DefaultReserveMultiplier at all;
        // DefaultSpendingShare was read by one census line, which therefore
        // counted how many clans could afford tier-6 gear at a third of the
        // real allowance and printed atShare=0.1 beside the answer. Its own
        // doc comment promised the figure was "reported every census so it can
        // become" a measurement rather than a borrowed guess -- and reporting
        // the constant instead of the setting is precisely what stopped that
        // from ever happening.
        //
        // Settings owns every default now. The reasoning that lived here is in
        // Settings.SpendingShare, where the number it describes actually is.

        private readonly Dictionary<string, ClanDay> _today = new Dictionary<string, ClanDay>();

        /// <summary>
        /// Read live rather than copied in, and that fixed two faults at once.
        ///
        /// The behaviour built this once with the values Settings held at
        /// campaign load, so a player who moved either slider in the options
        /// screen changed nothing until he restarted -- the service went on
        /// spending against the figures it had been born with. And the census
        /// built its own with the parameterless constructor, which meant it
        /// reported the shipped defaults however the player had configured the
        /// campaign: a man who set the share to a third read a survey of
        /// somebody else's tenth.
        ///
        /// Settings is the single source everything else in this mod reads, and
        /// there was never a reason for this one class to hold a copy.
        /// </summary>
        private static float ReserveMultiplier { get { return Settings.ReserveMultiplier; } }

        private static float SpendingShare
        {
            get { return Settings.SpendingShare < 0f ? 0f : Settings.SpendingShare; }
        }

        /// <summary>
        /// A clan's gear budget for one day: the purse as it stood when the
        /// house first went shopping, and what it has committed since.
        ///
        /// The snapshot is what keeps the ledger honest. Gold moves the instant
        /// a purchase completes, so subtracting the day's spending from a *live*
        /// balance would charge the clan twice for every denar. Measuring
        /// against the morning's purse instead means the ledger is the only
        /// place spending is counted -- and it has a second, wanted effect: a
        /// ransom that lands at noon does not reopen the budget until tomorrow,
        /// so a house spends at a steady rate rather than in spikes.
        /// </summary>
        private sealed class ClanDay
        {
            public int Purse;
            public int Spent;
        }

        /// <summary>
        /// Forgets yesterday. Called from the daily tick; without it the
        /// snapshot would go stale and a clan would shop against a purse it no
        /// longer has.
        /// </summary>
        public void StartDay()
        {
            _today.Clear();
        }

        /// <summary>
        /// Gold kept back so buying gear can never stop a clan paying its
        /// troops. Neither Lords Gear nor NoblesBuyStuff has anything like it --
        /// PartyGoldLowerThreshold does not appear in either binary -- and
        /// without it a house can spend itself to zero and watch its parties
        /// desert.
        /// </summary>
        public int Reserve(Clan clan)
        {
            if (clan == null) return 0;
            int parties = clan.WarPartyComponents != null ? clan.WarPartyComponents.Count : 0;
            return BudgetMath.Reserve(parties, ReserveMultiplier);
        }

        /// <summary>What the clan purse can still put towards gear today.</summary>
        public int ClanRoom(Clan clan)
        {
            if (clan == null || clan.Leader == null) return 0;

            ClanDay day = DayFor(clan);
            int room = day.Purse - day.Spent - Reserve(clan);
            return room < 0 ? 0 : room;
        }

        /// <summary>
        /// Everything this hero could reach: his own purse plus whatever the
        /// house can still contribute today.
        ///
        /// A clan leader contributes zero of "his own", because the clan purse
        /// already is his purse.
        /// </summary>
        public int Wallet(Hero hero)
        {
            if (hero == null) return 0;
            return OwnGold(hero) + ClanRoom(hero.Clan);
        }

        /// <summary>
        /// The most this hero may put into one trip: his share of the wallet.
        /// See Settings.SpendingShare for why a share and not the lot.
        /// </summary>
        public int Available(Hero hero)
        {
            // A caravan master shops against what he has earned, not against
            // his patron's treasury. See CaravanPurse for why this one hero is
            // treated apart: a caravan exists to make money and can be
            // measured, and a share of an unbounded purse is unbounded.
            if (CaravanPurse.IsCaravanLeader(hero)) return CaravanPurse.Budget(hero);

            return (int)(Wallet(hero) * SpendingShare);
        }

        /// <summary>
        /// What a whole shopping trip may cost him, to be divided between the
        /// slots he means to fill.
        ///
        /// The same arithmetic as Available and deliberately so: SpendingShare
        /// used to mean "of his wallet, per piece" and now means "of his wallet,
        /// per trip". The difference is where it is applied, not how it is
        /// worked out.
        ///
        /// Why it moved. Per piece, the share said nothing about how much a man
        /// needed -- a lord replacing his whole kit got the same allowance for
        /// his first piece as a lord replacing one strap, so the one who had
        /// just been robbed of everything could buy a magnificent helmet and
        /// then eleven rags. Per trip, a man who needs everything spreads his
        /// money thin and comes back in middling gear, and a man who needs one
        /// thing spends it all on that one thing. He improves by buying fewer
        /// pieces of better quality as he goes, which is what improving looks
        /// like.
        /// </summary>
        public int TripBudget(Hero hero)
        {
            return Available(hero);
        }

        /// <summary>
        /// Splits a price between the hero and his house, or reports that it
        /// cannot be paid.
        ///
        /// The share from BudgetMath is a starting point, not a constraint: a
        /// hero who cannot cover his part leans on the house, and a house at its
        /// reserve pushes the cost back onto the hero. Only when neither side
        /// can close the gap is the purchase refused. Nothing is moved here --
        /// call Record once the transaction has actually gone through.
        /// </summary>
        public bool TrySplit(Hero hero, int price, out int heroPart, out int clanPart)
        {
            heroPart = 0;
            clanPart = 0;
            if (hero == null || price <= 0) return false;

            // The share comes first and binds everything below it. A hero with
            // half a million behind him is still not allowed to spend it all on
            // one helmet.
            if (price > Available(hero)) return false;

            Clan clan = hero.Clan;
            Hero leader = clan != null ? clan.Leader : null;

            // The leader pays as the house, not as himself: it is one purse
            // either way, and routing it through the clan side is what keeps the
            // day's ledger tracking his own spending.
            if (leader == hero)
            {
                if (ClanRoom(clan) < price) return false;
                clanPart = price;
                return true;
            }

            int own = OwnGold(hero);
            int room = ClanRoom(clan);
            if (own + room < price) return false;

            // A caravan master pays the whole price himself, and can, because
            // Available already refused anything dearer than his savings. The
            // clan contributes nothing at all: he shops on a commission, and a
            // commission his patron then tops up at the counter would not be a
            // commission. ClanShare is not consulted -- it would hand part of
            // the bill to the clan on terms of its own and quietly turn this
            // back into the matched arrangement it replaced.
            if (CaravanPurse.IsCaravanLeader(hero))
            {
                if (own < price) return false;
                heroPart = price;
                clanPart = 0;
                return true;
            }

            float share = BudgetMath.ClanShare(false, clan != null ? clan.Tier : 0, own,
                                               clan != null ? clan.Gold : 0);
            clanPart = (int)(price * share);
            if (clanPart > room) clanPart = room;

            heroPart = price - clanPart;
            if (heroPart > own)
            {
                // The hero is short; the house covers the difference. Affordable
                // by the check above, so this cannot overrun the room.
                clanPart += heroPart - own;
                heroPart = own;
            }

            return true;
        }

        /// <summary>
        /// Records a completed purchase against the day's ledger. Only the clan
        /// side is tracked: a hero's own gold is read live and has already gone
        /// down.
        /// </summary>
        public void Record(Hero hero, int clanPart)
        {
            if (hero == null || clanPart <= 0) return;
            Clan clan = hero.Clan;
            if (clan == null || clan.Leader == null) return;

            DayFor(clan).Spent += clanPart;
        }

        /// <summary>
        /// A hero's own money, which for a clan leader is zero because the clan
        /// purse is the same gold. See the class comment.
        /// </summary>
        private static int OwnGold(Hero hero)
        {
            if (hero == null) return 0;
            if (hero.Clan != null && hero.Clan.Leader == hero) return 0;
            return hero.Gold < 0 ? 0 : hero.Gold;
        }

        /// <summary>
        /// The clan's entry for today, snapshotting the purse the first time the
        /// house is asked about. Lazy rather than swept at dawn: a clan nobody
        /// asks about has spent nothing, so there is nothing to remember.
        /// </summary>
        private ClanDay DayFor(Clan clan)
        {
            ClanDay day;
            if (_today.TryGetValue(clan.StringId, out day)) return day;

            day = new ClanDay();
            day.Purse = clan.Gold;
            _today.Add(clan.StringId, day);
            return day;
        }
    }
}
