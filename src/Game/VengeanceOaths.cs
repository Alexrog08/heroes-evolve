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
    /// men of the dead man's clan each swear vengeance on the killer.
    ///
    /// So the oath is the whole ledger, and it is the game's. One oath is one
    /// robbery owed. It is sworn when a man is robbed (Swear), it is what
    /// makes his house dangerous to the robber's (Owed), and it is struck off
    /// when the house collects (Fulfil). Nothing else is consulted: not the
    /// standing between the two houses, which goes on meaning whatever it
    /// meant, and not a count kept anywhere else. It is saved with the
    /// campaign and survives a reload, an oath nobody collects lapses by the
    /// game's own weekly purge, and a campaign that has had this mod removed
    /// loads as it always did, with some sworn vengeances left in its history.
    ///
    /// The encyclopedia therefore shows exactly what is outstanding. A man's
    /// page lists who has sworn against him and whom he has sworn against,
    /// and a line disappears the day it is answered.
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
        /// Whether the captor's house holds an oath against the prisoner's:
        /// somebody of the one swore vengeance on somebody of the other, and
        /// nobody has collected it yet.
        /// </summary>
        public static bool Owed(Hero captor, Hero prisoner)
        {
            return Oldest(captor, prisoner) != null;
        }

        /// <summary>
        /// Strikes one oath off: the oldest the captor's house holds against
        /// the prisoner's. False when it held none.
        ///
        /// The log has no door for taking an entry out -- DeleteLogAtIndex is
        /// internal -- but it has two public ones that do it between them.
        /// LogEntry.AddLogEntry with a date re-dates the entry it is handed,
        /// and LogEntryHistory.DeleteOutdatedLogs is the purge the game runs
        /// every week, which drops whatever has outlived its keep. Dated to
        /// the beginning of time the oath is twenty years stale at once, and
        /// the purge takes it and the second reference re-dating left behind.
        /// All the purge can otherwise remove is what the game would have
        /// removed by the end of the week anyway.
        ///
        /// The oldest first, so that a house robbed twice is answered for the
        /// earlier robbery before the later one and no oath is left to lapse
        /// behind a newer one.
        /// </summary>
        public static bool Fulfil(Hero captor, Hero prisoner)
        {
            CharacterInsultedLogEntry oath = Oldest(captor, prisoner);
            if (oath == null) return false;

            LogEntry.AddLogEntry(oath, CampaignTime.Zero);
            Campaign.Current.LogEntryHistory.DeleteOutdatedLogs();

            return true;
        }

        /// <summary>
        /// Every oath outstanding, counted by who swore against whom: the key
        /// is Key(swore, against). For the census, which wants all of it at
        /// one reading rather than one pair at a time.
        ///
        /// leftAlone is the quarrels in the log that are not read as debts:
        /// the ones TaleWorlds wrote, and any oath whose two men have since
        /// ended up in one house or in none.
        /// </summary>
        internal static Dictionary<string, int> Outstanding(out int sworn, out int leftAlone)
        {
            sworn = 0;
            leftAlone = 0;
            Dictionary<string, int> owed = new Dictionary<string, int>();

            MBReadOnlyList<LogEntry> logs = Logs();
            if (logs == null) return owed;

            CampaignTime opened = CampaignOpened();

            for (int i = 0; i < logs.Count; i++)
            {
                Clan swore, against;
                if (!Read(logs[i], opened, out swore, out against))
                {
                    if (logs[i] is CharacterInsultedLogEntry) leftAlone++;
                    continue;
                }

                string key = Key(swore, against);
                int count;
                owed.TryGetValue(key, out count);
                owed[key] = count + 1;
                sworn++;
            }

            return owed;
        }

        /// <summary>The house that swore, then the house it swore against.</summary>
        internal static string Key(Clan swore, Clan against)
        {
            return (swore != null ? swore.StringId : "") + ">" + (against != null ? against.StringId : "");
        }

        private static CharacterInsultedLogEntry Oldest(Hero captor, Hero prisoner)
        {
            if (captor == null || prisoner == null) return null;

            Clan mine = captor.Clan;
            Clan theirs = prisoner.Clan;
            if (mine == null || theirs == null || mine == theirs) return null;

            MBReadOnlyList<LogEntry> logs = Logs();
            if (logs == null) return null;

            CampaignTime opened = CampaignOpened();

            // The log is kept oldest first.
            for (int i = 0; i < logs.Count; i++)
            {
                Clan swore, against;
                if (!Read(logs[i], opened, out swore, out against)) continue;

                if (swore == mine && against == theirs) return (CharacterInsultedLogEntry)logs[i];
            }

            return null;
        }

        /// <summary>
        /// Whether a log entry is an oath this mod reads, and between which
        /// houses.
        ///
        /// Two locks, and an entry has to pass both. The note that makes a
        /// quarrel a vengeance is a private field, so it is not one of them.
        ///
        /// It was written since the campaign opened. The game dates its own
        /// backstory quarrels years before that and adds none afterwards --
        /// the Aserai who swore vengeance for a murdered kinsman did it in
        /// 1080, and nothing in the game ever acts on that -- and an oath
        /// struck off is dated to nought, so it stops counting before the
        /// purge has even run.
        ///
        /// And it is one the game does not announce, which is what naming a
        /// common soldier makes it (Swear). Every quarrel TaleWorlds writes
        /// names a hero or nobody.
        ///
        /// Either lock keeps the feuds TaleWorlds wrote TaleWorlds' in the
        /// campaign as shipped. Both, so that a mod which moves the calendar,
        /// or writes quarrels of its own, cannot have this one collect a debt
        /// nobody owes it -- or strike somebody else's line off the record.
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
            if (oath.IsVisibleNotification) return false;

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
                // Without it every quarrel in the log counts except the ones
                // struck off, which are dated to nought and so are before it.
                return CampaignTime.Hours(1f);
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
        ///
        /// Held a frame, like the news of the robbery it follows
        /// (PendingNotices). That line is queued before this one, so the two
        /// print in the order they happened; sent at once, the oath would be
        /// on screen before the robbery it answers, and before the capture
        /// that led to both.
        /// </summary>
        private static void Tell(CharacterInsultedLogEntry oath, Hero victim, Hero robber)
        {
            Clan mine = Clan.PlayerClan;
            if (mine == null || (victim.Clan != mine && robber.Clan != mine)) return;

            try
            {
                PendingNotices.Queue(oath.GetEncyclopediaText().ToString(), Colors.Red);
            }
            catch
            {
                // A message that cannot be shown must not cost the oath.
            }
        }
    }
}
