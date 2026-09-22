using System.Collections.Generic;
using TaleWorlds.Library;

namespace HeroesEvolve
{
    /// <summary>
    /// Lines held back by one frame so they print after the game's own.
    ///
    /// The mod's robbery notice belongs directly under "X has been taken
    /// prisoner by Y", and for a while this project believed it landed there
    /// because listeners run in the order they registered. They do not.
    /// MbEvent.AddNonSerializedListener PREPENDS -- the new node becomes the
    /// head and its Next is the old head -- and InvokeList walks head to Next.
    /// The last listener registered runs FIRST. SubModule.xml depends on
    /// Sandbox, so DefaultLogsCampaignBehavior registers before this mod does,
    /// which puts this mod's listener at the head and its line on screen
    /// first. The message feed prints oldest at the top, so the robbery was
    /// reading above the capture that caused it.
    ///
    /// There is no ordering trick from this side of the event: to run last a
    /// listener must register first, and a module that declares its
    /// dependencies honestly can never do that. The same conclusion was
    /// already reached once before on HeroComesOfAgeEvent, and the fix there
    /// was the same shape as this one -- let the game finish, then act.
    ///
    /// So the line waits for the next tick. By then every other listener on
    /// that event has run and printed, and a tick on the campaign map is the
    /// next frame, which is to say no delay anybody can perceive.
    ///
    /// Held here rather than sent with a delay because nothing in the campaign
    /// API takes one. Nothing is persisted: a queue that a save interrupts
    /// loses a sentence, which is the right thing for it to lose.
    /// </summary>
    public static class PendingNotices
    {
        private struct Notice
        {
            public string Text;
            public Color Colour;
        }

        private static readonly List<Notice> Waiting = new List<Notice>();

        /// <summary>How many lines are still held. For tests and the census.</summary>
        public static int Count
        {
            get { return Waiting.Count; }
        }

        public static void ResetSession()
        {
            Waiting.Clear();
        }

        /// <summary>
        /// Holds a line for the next tick. Rendered here rather than at flush
        /// time: a TextObject reads the game's text system, and doing that on
        /// the event is both cheaper and safer than holding an object whose
        /// variables might be reused before it is drawn.
        /// </summary>
        public static void Queue(string text, Color colour)
        {
            if (string.IsNullOrEmpty(text)) return;

            Notice notice;
            notice.Text = text;
            notice.Colour = colour;
            Waiting.Add(notice);
        }

        /// <summary>
        /// Prints everything held, in the order it was queued. Cheap to call
        /// on every tick: the common answer is an empty list and one branch.
        /// </summary>
        public static void Flush()
        {
            if (Waiting.Count == 0) return;

            // Copied and cleared before printing. DisplayMessage reaches the
            // UI, and a line queued from there -- or a throw halfway down the
            // list -- must not leave the same sentences to be printed again on
            // the next tick.
            Notice[] due = Waiting.ToArray();
            Waiting.Clear();

            for (int i = 0; i < due.Length; i++)
            {
                try
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage(due[i].Text, due[i].Colour));
                }
                catch
                {
                    // A line that cannot be shown is dropped, not retried.
                }
            }
        }
    }
}
