using System;
using System.IO;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// Rotating file log under %LocalAppData%. Every call is guarded: logging
    /// must never be the reason a campaign fails.
    /// </summary>
    public static class ModLog
    {
        private const string FileName = "hlf.log";
        private static readonly object Gate = new object();
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
                    string line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + level + "] " + message + Environment.NewLine;
                    File.AppendAllText(path, line);
                }
            }
            catch
            {
                // Swallowed on purpose: a failed log write is not worth a crash.
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
