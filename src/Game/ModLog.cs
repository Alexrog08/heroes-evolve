using System;
using System.IO;

namespace HeroesEvolve
{
    /// <summary>
    /// File log under %LocalAppData%, rolled over when it gets large. Every call
    /// is guarded: logging must never be the reason a campaign fails.
    ///
    /// It used to say it rotated and did not, which in a published mod means a
    /// file that grows for the life of the installation -- ours reached four
    /// megabytes from testing alone. One previous run is kept beside the current
    /// one, which is what anybody reporting a bug actually needs.
    /// </summary>
    public static class ModLog
    {
        private const string FileName = "hev.log";
        private const string OldFileName = "hev.log.old";

        /// <summary>
        /// Size at which the log rolls over. Two megabytes is tens of thousands
        /// of lines -- more campaign history than any report needs -- and the
        /// cost of keeping one previous file is bounded at twice it.
        /// </summary>
        private const long MaxBytes = 2L * 1024L * 1024L;

        /// <summary>
        /// Writes between size checks. Checking every line would stat the file
        /// on every write; never rechecking would let one long session grow
        /// without limit.
        /// </summary>
        private const int WritesBetweenChecks = 500;

        private static readonly object Gate = new object();
        private static int _sinceCheck = int.MaxValue;

        public static bool Enabled = true;

        public static void Info(string message) { Write("INFO", message); }
        public static void Error(string message) { Write("ERROR", message); }

        private static void Write(string level, string message)
        {
            if (!Enabled) return;
            try
            {
                lock (Gate)
                {
                    string folder = GetFolder();
                    if (folder == null) return;
                    string path = Path.Combine(folder, FileName);
                    RollIfLarge(folder, path);

                    string line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + level + "] " + message + Environment.NewLine;
                    File.AppendAllText(path, line);
                }
            }
            catch
            {
                // Swallowed on purpose: a failed log write is not worth a crash.
            }
        }

        /// <summary>
        /// Moves the log aside once it passes the cap, keeping exactly one
        /// previous file. Called under the same lock as the write.
        /// </summary>
        private static void RollIfLarge(string folder, string path)
        {
            if (_sinceCheck < WritesBetweenChecks) { _sinceCheck++; return; }
            _sinceCheck = 0;

            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists || info.Length < MaxBytes) return;

                string old = Path.Combine(folder, OldFileName);
                if (File.Exists(old)) File.Delete(old);
                File.Move(path, old);
            }
            catch
            {
                // A log that cannot be rolled is still a log. Nothing here is
                // worth interrupting a campaign for.
            }
        }

        private static string GetFolder()
        {
            try
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(root)) return null;
                string folder = Path.Combine(Path.Combine(root, "Mount and Blade II Bannerlord"), "logs");
                Directory.CreateDirectory(folder);
                return folder;
            }
            catch
            {
                return null;
            }
        }
    }
}
