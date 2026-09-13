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
        /// The share of a caravan's daily profit its master keeps for himself.
        ///
        /// Fixed rather than exposed, and the arithmetic says why. With a
        /// commission c and a financing multiplier M, a master accumulates cG a
        /// day and may spend cGM, of which he pays cG and his patron pays
        /// cG(M-1). The patron therefore keeps
        ///
        ///     (1-c)G - cG(M-1)  =  G(1 - cM)
        ///
        /// of everything the caravan earns, and the time to afford a piece of
        /// price P is P / (cGM) days. Both depend on the PRODUCT and on nothing
        /// else: c and M never appear apart. Two sliders for one number is a
        /// trap -- a player moves both and lands somewhere he did not intend --
        /// so the product is the setting and this is a constant.
        ///
        /// Which makes the boundary legible: at cM = 1 the master spends
        /// precisely everything the caravan makes, and the caravan stops being
        /// a source of income at all. That is the top of the slider, and it is
        /// a real choice rather than a misconfiguration.
        /// </summary>
        public const float Commission = 0.10f;

        /// <summary>The threshold and divisor AddIncomeFromParty applies, read from it.</summary>
        private const int IncomeFloor = 10000;
        private const int IncomeDivisor = 10;

        /// <summary>
        /// How much of his own money a master's patron matches. Derived from the
        /// share the player actually set, so the setting means what it says.
        /// </summary>
        public static float Multiplier()
        {
            float share = Settings.CaravanGearShare;
            if (share <= 0f) return 0f;
            return share / Commission;
        }

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
        /// What this master may spend on gear: his savings, matched by his
        /// patron.
        ///
        /// Zero when the share is zero, which switches the whole thing off and
        /// leaves him shopping on whatever he happens to be carrying -- not on
        /// the clan purse. That is the point of the feature and not a corner of
        /// it, so it holds at every setting.
        /// </summary>
        public static int Budget(Hero hero)
        {
            if (hero == null) return 0;

            int purse = hero.Gold;
            if (purse <= 0) return 0;

            float matched = purse * Multiplier();
            if (matched <= 0f) return 0;
            return matched > int.MaxValue ? int.MaxValue : (int)matched;
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
