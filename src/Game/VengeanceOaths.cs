using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Core;
using TaleWorlds.Library;
using HeroesEvolve.Core;

namespace HeroesEvolve
{
    /// <summary>
    /// Who has sworn vengeance on whom, kept where the game keeps it.
    ///
    /// A robbery is answered by the house that was robbed, and for that the
    /// mod has to know which of two houses robbed the other. Their standing
    /// cannot say: it is one number and both of them wear it. Nothing else the
    /// game stores between two houses has a direction either, and this mod
    /// writes nothing of its own into a save.
    ///
    /// The game does keep one directed record between two lords, and it is
    /// exactly this one. CharacterInsultedLogEntry with the note
    /// VengeanceQuarrel prints "{A} has sworn vengeance against {B}", is kept
    /// for 1680 days -- twenty years -- shows on both men's encyclopedia pages,
    /// and is what a lord cites when asked why he is somebody's enemy
    /// (LordConversationsCampaignBehavior.GetReasonForEnmity). The game writes
    /// them itself, in BackstoryCampaignBehavior.OnNewGameCreated: when one
    /// Aserai lord has murdered another before the campaign opens, the young
    /// men of the dead man's clan each swear vengeance on the killer, and the
    /// clan's standing with him falls by seventy-five. A robbery here does the
    /// same two things at half an execution's price (RobberyReckoning), with
    /// the same record.
    ///
    /// So the oath is the game's and so is its upkeep. It is saved with the
    /// campaign and survives a reload, it expires by the game's own weekly
    /// purge (Campaign.OnWeeklyTick, LogEntryHistory.DeleteOutdatedLogs), and
    /// a campaign that has had this mod removed loads as it always did, with
    /// some sworn vengeances left in its history.
    ///
    /// What the log cannot hold is that an oath has been answered: entries
    /// can be added and never taken out, and a second entry to say "settled"
    /// would print as a second quarrel. So the oath says who and the standing
    /// between the houses says whether anything is still outstanding
    /// (PlunderRules.Claim).
    /// </summary>
    public static class VengeanceOaths
    {
        /// <summary>
        /// The robbed man swears vengeance on the man who robbed him.
        ///
        /// Not sworn inside one house, which settles its own affairs, and not
        /// on a band with no name to swear against.
        ///
        /// The third argument is what the quarrel is over. The game reads it
        /// for one thing outside a courtship quarrel: whether to announce the
        /// entry to the whole map, which it does when the subject is a hero
        /// or there is none (CharacterInsultedLogEntry.IsVisibleNotification)
        /// -- with a notification text that is only the swearer's name, since
        /// the game never adds one of these after a campaign has started and
        /// never needed the sentence. Naming a common soldier of his people
        /// keeps it out of the feed; the player is told in a line of the
        /// mod's own when the oath concerns his house.
        /// </summary>
        public static void Swear(Hero victim, Hero robber)
        {
            if (victim == null || robber == null || victim == robber) return;
            if (victim.Clan == null || robber.Clan == null || victim.Clan == robber.Clan) return;
            if (Campaign.Current == null || Campaign.Current.LogEntryHistory == null) return;

            CharacterInsultedLogEntry oath = new CharacterInsultedLogEntry(
                victim, robber, Subject(victim), ActionNotes.VengeanceQuarrel);

            LogEntry.AddLogEntry(oath);
            RobberyTally.Oath();

            Tell(oath, victim, robber);
        }

        /// <summary>
        /// Which of the two houses has something to answer for, seen from the
        /// captor's side. The last oath between them decides, read newest
        /// first, and only while their standing is below nought.
        /// </summary>
        public static PlunderRules.Claim Between(Hero captor, Hero prisoner)
        {
            if (captor == null || prisoner == null) return PlunderRules.Claim.None;

            Clan mine = captor.Clan;
            Clan theirs = prisoner.Clan;
            if (mine == null || theirs == null || mine == theirs) return PlunderRules.Claim.None;

            // The cheap half first: most pairs of houses have nothing
            // outstanding, and the log need not be read to know it.
            int standing = RobberyReckoning.Standing(captor, prisoner);
            if (standing >= 0) return PlunderRules.Claim.None;

            MBReadOnlyList<LogEntry> logs = Logs();
            if (logs == null) return PlunderRules.Claim.None;

            CampaignTime opened = CampaignOpened();

            for (int i = logs.Count - 1; i >= 0; i--)
            {
                Clan sworn, against;
                if (!Read(logs[i], opened, out sworn, out against)) continue;

                bool thisPair = (sworn == mine && against == theirs) || (sworn == theirs && against == mine);
                if (!thisPair) continue;

                return PlunderRules.ClaimFor(sworn.StringId, against.StringId,
                                             mine.StringId, theirs.StringId, standing);
            }

            return PlunderRules.Claim.None;
        }

        /// <summary>
        /// Every pair of houses with an oath between them, and which of the
        /// two swore last. For the census: sworn is every oath still in the
        /// log.
        /// </summary>
        internal static Dictionary<string, Clan> LastSworn(out int sworn)
        {
            sworn = 0;
            Dictionary<string, Clan> last = new Dictionary<string, Clan>();

            MBReadOnlyList<LogEntry> logs = Logs();
            if (logs == null) return last;

            CampaignTime opened = CampaignOpened();

            // Oldest first, so that a later oath between the same two houses
            // overwrites an earlier one.
            for (int i = 0; i < logs.Count; i++)
            {
                Clan swore, against;
                if (!Read(logs[i], opened, out swore, out against)) continue;

                sworn++;
                last[Pair(swore, against)] = swore;
            }

            return last;
        }

        /// <summary>One key for two houses, whichever way round they are named.</summary>
        internal static string Pair(Clan one, Clan other)
        {
            string a = one != null ? one.StringId : "";
            string b = other != null ? other.StringId : "";
            return string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
        }

        /// <summary>
        /// Whether a log entry is an oath this mod reads, and between which
        /// houses.
        ///
        /// Every quarrel written since the campaign opened. The note that
        /// makes one a vengeance is a private field, so it is not read; the
        /// date is. The game dates its own backstory quarrels years before
        /// the campaign begins and adds none afterwards, so anything later is
        /// one of ours -- and the feuds TaleWorlds wrote stay TaleWorlds'.
        /// </summary>
        private static bool Read(LogEntry entry, CampaignTime opened, out Clan swore, out Clan against)
        {
            swore = null;
            against = null;

            CharacterInsultedLogEntry oath = entry as CharacterInsultedLogEntry;
            if (oath == null) return false;
            if (oath.GameTime < opened) return false;

            // The game's own naming, which reads backwards: its Insultee is
            // the man who swears and its Insulter the man sworn against, as
            // the backstory fills them and as GetEncyclopediaText prints them.
            if (oath.Insultee == null || oath.Insulter == null) return false;

            swore = oath.Insultee.Clan;
            against = oath.Insulter.Clan;
            return swore != null && against != null && swore != against;
        }

        private static MBReadOnlyList<LogEntry> Logs()
        {
            if (Campaign.Current == null || Campaign.Current.LogEntryHistory == null) return null;
            return Campaign.Current.LogEntryHistory.GameActionLogs;
        }

        private static CampaignTime CampaignOpened()
        {
            try
            {
                return Campaign.Current.Models.CampaignTimeModel.CampaignStartTime;
            }
            catch
            {
                // Without it every quarrel in the log counts, which errs
                // toward honouring the oaths the game wrote itself.
                return CampaignTime.Zero;
            }
        }

        /// <summary>A common soldier of the victim's people, or failing that anybody who is not a hero.</summary>
        private static CharacterObject Subject(Hero victim)
        {
            CharacterObject soldier = victim.Culture != null ? victim.Culture.BasicTroop : null;
            if (soldier != null && !soldier.IsHero) return soldier;

            foreach (CharacterObject character in CharacterObject.All)
            {
                if (character != null && !character.IsHero) return character;
            }

            return null;
        }

        /// <summary>
        /// Says so in the feed when the oath concerns the player's house, in
        /// the game's own sentence for it.
        ///
        /// On either side. An oath sworn against his house is the warning that
        /// matters most -- it is who will be looking for him -- and one sworn
        /// by his house is how he learns he has something to collect.
        /// </summary>
        private static void Tell(CharacterInsultedLogEntry oath, Hero victim, Hero robber)
        {
            Clan mine = Clan.PlayerClan;
            if (mine == null || (victim.Clan != mine && robber.Clan != mine)) return;

            try
            {
                InformationManager.DisplayMessage(
                    new InformationMessage(oath.GetEncyclopediaText().ToString(), Colors.Red));
            }
            catch
            {
                // A message that cannot be shown must not cost the oath.
            }
        }
    }
}
