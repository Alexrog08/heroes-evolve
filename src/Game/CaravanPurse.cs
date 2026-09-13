using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace HeroesEvolve
{
    /// <summary>
    /// A caravan master's own money, and what it lets him wear.
    ///
    /// Every other hero in this mod shops out of the clan purse, which for the
    /// player's own house is literally the player's gold -- Clan.Gold is
    /// Clan.Leader.Gold, with no treasury in between. That is right for a lord
    /// sent off with troops: a war party does not exist to make money, so
    /// limiting its leader by what he earns would make no sense. It is wrong
    /// for a caravan, which exists to make money and can be measured.
    ///
    /// A live campaign measured what the old rule did. A caravan master with a
    /// tier-6 ceiling and a rich patron accumulated 332,862 denars of gear in
    /// fifty-six days -- one body armour of 272,758 among it -- against a
    /// caravan earning some 1,600 a day. Across seven caravans the gear took
    /// roughly half their gross. The purse was never the constraint the design
    /// assumed, because a share of an unbounded purse is unbounded.
    ///
    /// So a caravan master is paid a commission on what he brings in, and that
    /// commission -- not his patron's treasury -- is what he shops against.
    /// A caravan that earns well buys its leader good armour; a poor one does
    /// not. He cannot outspend what he produces, by construction rather than by
    /// a tuned constant, which is the property the whole thing is for.
    ///
    /// Hero.Gold is the store, and that choice carries the design. This mod
    /// writes nothing into the save by deliberate policy, so a ledger of our
    /// own would reset on every load and the accumulation -- which is the whole
    /// idea -- would never survive a night's sleep. The game already keeps a
    /// per-hero purse and already saves it. Using it means years of earnings
    /// persist with no mod save data at all.
    ///
    /// No Harmony either. DefaultClanFinanceModel.AddIncomeFromParty cannot be
    /// intercepted without patching, but it does not need to be: the commission
    /// is computed from the same input the engine reads, PartyTradeGold, so the
    /// two agree without one having to watch the other.
    /// </summary>
    public static class CaravanPurse
    {
        /// <summary>
        /// The share of a caravan's daily profit its master keeps for himself,
        /// and the whole of the mechanism.
        ///
        /// This was briefly a commission plus a financing multiplier: he kept a
        /// tenth, and his patron matched it several times over at the counter.
        /// The arithmetic was identical either way -- with commission c and
        /// multiplier M the patron keeps G(1 - cM) and a piece of price P takes
        /// P/(cGM) days, so c and M never appear apart and only their product
        /// means anything.
        ///
        /// It came out because of WHEN the money moved rather than how much.
        /// The matched half was taken at the moment of purchase, which is a
        /// sum leaving the player's purse suddenly, at a time he did not
        /// choose, for a decision he did not make. A commission is a standing
        /// cost he can plan around. Same denars, better manners.
        ///
        /// What that gave up is worth writing down, because it is the one real
        /// cost of the change: under the multiplier an idle master was cheap,
        /// since his patron paid the small commission daily and the large
        /// matching part only when something was actually bought. Now the whole
        /// share is paid every day whether he spends it or not, and a master
        /// who has reached his ceiling in every slot accumulates money he has
        /// no use for.
        ///
        /// Nothing is done about that yet, deliberately. Every cap anyone can
        /// name is a number pulled out of the air -- stop at twice the kit he
        /// wears, stop at some figure in denars -- and the one rule that sounds
        /// principled, stop once he has saved more than he is wearing, would
        /// freeze a poorly dressed master below the price of any upgrade he
        /// might want. The case also needs a hero at his ceiling in all eleven
        /// slots, which a census of six caravan masters found exactly once. The
        /// money is not destroyed either: his ceiling rises as his skills do,
        /// and a companion who is later made a lord takes his savings with him
        /// as his clan's treasury. Left alone until a campaign says it matters.
        /// </summary>
        public static float Commission
        {
            get
            {
                float share = Settings.CaravanGearShare;
                if (share < 0f) return 0f;
                return share > 1f ? 1f : share;
            }
        }

        /// <summary>The threshold and divisor AddIncomeFromParty applies, read from it.</summary>
        private const int IncomeFloor = 10000;
        private const int IncomeDivisor = 10;

        /// <summary>
        /// Every hero the daily pass found leading a caravan, by id.
        ///
        /// Recorded from the party side because the hero side cannot be
        /// trusted, and a live campaign proved it. Larstan the Sea-Raider was
        /// listed as a caravan leader by the census -- which walks
        /// MobileParty.All and reads party.LeaderHero -- in the census before
        /// his purchase and in the census after it. Between them he bought a
        /// 13,323-denar harness on a purse of 752, which is the wallet path and
        /// not the purse path: seventeen times what his savings allowed. The
        /// only test that separates the two is the one asked of
        /// hero.PartyBelongedTo, so that lookup disagreed with the walk that
        /// had just found him twice.
        ///
        /// Rather than guess which of the two is right in which frame, the
        /// answer is taken from the side that demonstrably works and kept.
        /// Session-local and rebuilt every day, like every other state here, so
        /// it adds nothing to the save.
        /// </summary>
        private static readonly HashSet<string> _leaders = new HashSet<string>();

        /// <summary>
        /// Whether this hero leads a caravan, which is the one place in the
        /// campaign where a hero who is not a clan leader has an income that
        /// can be measured.
        ///
        /// Either view will do and neither is trusted alone. The live lookup
        /// catches a man given a caravan since this morning, whom the record
        /// cannot know about yet; the record catches the case above, where the
        /// live lookup fails on a hero the daily walk can see perfectly well.
        /// Both failures are of the same kind -- a caravan leader treated as an
        /// ordinary hero and handed the clan treasury -- so the union is the
        /// safe combination rather than the lazy one.
        /// </summary>
        public static bool IsCaravanLeader(Hero hero)
        {
            if (hero == null) return false;

            MobileParty party = hero.PartyBelongedTo;
            if (party != null && party.IsCaravan && party.LeaderHero == hero) return true;

            return hero.StringId != null && _leaders.Contains(hero.StringId);
        }

        /// <summary>
        /// What this master may spend on gear: his savings, and not a denar
        /// more.
        ///
        /// At a share of zero nothing is paid in, so a master left long enough
        /// spends down to nothing and shops no further -- on his own money to
        /// the end, never on the clan purse. That is the point of the feature
        /// rather than a corner of it, so it has to hold at every setting.
        /// </summary>
        public static int Budget(Hero hero)
        {
            if (hero == null) return 0;
            return hero.Gold > 0 ? hero.Gold : 0;
        }

        /// <summary>
        /// Called when a campaign is loaded; statics outlive one campaign.
        ///
        /// The roll is empty until the first daily tick fills it, so for that
        /// one day a caravan master is known only by the live lookup. That is
        /// the weaker of the two views and the reason this class exists, but a
        /// single day of it after a load is a far smaller hole than carrying
        /// yesterday's roll into a campaign that may not contain the same
        /// heroes -- or the same caravans.
        /// </summary>
        public static void ResetSession()
        {
            _leaders.Clear();
        }

        /// <summary>
        /// Pays every caravan master his cut of the day.
        ///
        /// Taken from the man whose gold the caravan's income lands in, which
        /// is the clan leader, because Clan.Gold is Clan.Leader.Gold. Moving it
        /// through GiveGoldAction rather than writing Hero.Gold directly keeps
        /// it a transaction the game knows about, the same way every purchase
        /// in this mod already settles.
        ///
        /// A patron who cannot pay pays what he has. Nobody is driven negative
        /// to fund a companion's armour.
        /// </summary>
        public static void DailyTick()
        {
            // The roll is rebuilt whatever the share is set to. At zero nobody
            // is paid, but a caravan master must still be known for one, or
            // switching the feature off would quietly hand him the clan purse
            // again -- the opposite of what off means.
            _leaders.Clear();

            foreach (MobileParty party in MobileParty.All)
            {
                try
                {
                    if (party == null || !party.IsCaravan) continue;

                    Hero leader = party.LeaderHero;
                    if (leader == null || leader.IsDead) continue;

                    if (leader.StringId != null) _leaders.Add(leader.StringId);
                    if (Settings.CaravanGearShare <= 0f) continue;

                    // The engine's own daily payment, computed from the same
                    // number it reads. See DefaultClanFinanceModel.
                    int profit = party.PartyTradeGold > IncomeFloor
                        ? (party.PartyTradeGold - IncomeFloor) / IncomeDivisor
                        : 0;
                    if (profit <= 0) continue;

                    int cut = (int)(profit * Commission);
                    if (cut <= 0) continue;

                    Hero payer = party.Party != null ? party.Party.Owner : null;
                    if (payer != null && payer.Clan != null && payer.Clan.Leader != null)
                    {
                        payer = payer.Clan.Leader;
                    }
                    if (payer == null || payer == leader) continue;

                    if (cut > payer.Gold) cut = payer.Gold;
                    if (cut <= 0) continue;

                    GiveGoldAction.ApplyBetweenCharacters(payer, leader, cut, true);
                }
                catch
                {
                    // One unreadable caravan must not cost the rest their wages.
                }
            }
        }
    }
}
