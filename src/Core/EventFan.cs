using System;

namespace HeroesEvolve.Core
{
    /// <summary>
    /// Hands one notice to every subscriber of an event, each on its own.
    ///
    /// Other mods subscribe to this one's public events (HeroesEvolve.Api),
    /// and a plain invocation of a multicast delegate would stop at the first
    /// subscriber that threw -- the rest would never hear, and the exception
    /// would climb back into whatever this mod was doing when it raised the
    /// notice: a capture, a purchase, a conversation. The game catches nothing
    /// on the way up. So each subscriber is called alone, its failure is
    /// counted and reported, and the next one is called all the same.
    ///
    /// Called after the thing it reports has been done in full, which is the
    /// other half of the promise: a subscriber that fails cannot leave a
    /// robbery half made.
    /// </summary>
    public static class EventFan
    {
        /// <summary>
        /// Calls each subscriber with the same arguments and returns how many
        /// threw. failed, when given, hears each failure: the subscriber that
        /// threw and what it threw. A reporter that throws in turn is ignored,
        /// because the one thing worse than a lost log line is a lost robbery.
        /// </summary>
        public static int Raise<T>(Action<T> handlers, T args, Action<Delegate, Exception> failed)
        {
            if (handlers == null) return 0;

            int failures = 0;
            Delegate[] each = handlers.GetInvocationList();

            for (int i = 0; i < each.Length; i++)
            {
                try
                {
                    ((Action<T>)each[i])(args);
                }
                catch (Exception ex)
                {
                    failures++;
                    if (failed == null) continue;

                    try
                    {
                        failed(each[i], ex);
                    }
                    catch
                    {
                        // See above: the report is the expendable part.
                    }
                }
            }

            return failures;
        }
    }
}
