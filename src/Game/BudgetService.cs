using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
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
        /// <summary>
        /// Scales the untouchable reserve. 1.0 keeps exactly the game's own
        /// PartyGoldLowerThreshold per war party.
        /// </summary>
        public const float DefaultReserveMultiplier = 1.0f;

        /// <summary>
        /// The share of a hero's whole wallet he may put into one purchase.
        ///
        /// This is what makes gold matter. A campaign measured 876,000 as the
        /// median wallet against a 2,924 median purchase: without a share, money
        /// is not a constraint and never becomes one, and the tier ceiling is
        /// the only thing standing between a lord and the best item in the shop.
        ///
        /// With a share, the price of a thing is measured against the wealth of
        /// the house that buys it. A tier-6 piece runs about 43,000, so at a
        /// tenth it takes a house holding some 430,000 to afford one; tier 5 at
        /// 15,700 needs about 157,000. A young clan is priced out of the good
        /// stuff and grows into it, which is the shape the ceiling alone cannot
        /// express.
        ///
        /// A tenth is Lords Gear's own default territory -- its
        /// AIGoldSpendingPercentage and ClanGoldSpendingPercentage multiply the
        /// same wallet (hero gold plus clan gold, less pending) and its log line
        /// literally reads "Wealth: {1}, Limit: {2}". It is the one number here
        /// taken from someone else's play experience rather than a measurement,
        /// and it is reported every census so it can become one.
        /// </summary>
        public const float DefaultSpendingShare = 0.10f;

        private readonly float _reserveMultiplier;
        private readonly float _spendingShare;
        private readonly Dictionary<string, ClanDay> _today = new Dictionary<string, ClanDay>();

        public BudgetService() : this(DefaultReserveMultiplier, DefaultSpendingShare) { }

        public BudgetService(float reserveMultiplier, float spendingShare)
        {
            _reserveMultiplier = reserveMultiplier;
            _spendingShare = spendingShare < 0f ? 0f : spendingShare;
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
            return BudgetMath.Reserve(parties, _reserveMultiplier);
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
        /// The most this hero may put into one purchase: his share of the
        /// wallet. See DefaultSpendingShare for why a share and not the lot.
        /// </summary>
        public int Available(Hero hero)
        {
            return (int)(Wallet(hero) * _spendingShare);
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
