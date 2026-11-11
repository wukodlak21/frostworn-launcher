using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Oracle_Lite.Library
{
    /// <summary>
    /// Classless realm support (Launcher 1.1). The Classless realm runs on the enGB data set,
    /// which is the stock enUS locale files under enGB names plus the Classless patches the
    /// Launcher downloads into Data\enGB. Those 2+ GB are never downloaded: each enGB file is a
    /// hard link to its enUS twin (same file on disk, no extra space). On a drive without hard
    /// links (FAT32/exFAT) a copy is made instead. After an enUS file is replaced by an update
    /// the link would still point at the old data, so every call checks and relinks.
    /// </summary>
    internal static class Locale_Set
    {
        private const string StockLocale = "enUS";
        private const string HeroLocale = "enGB";

        // Files the Launcher delivers into Data\enGB itself; never linked from enUS.
        private static readonly HashSet<string> Delivered = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "patch-enGB-V.MPQ",
            "patch-enGB-W.MPQ",
        };

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [StructLayout(LayoutKind.Sequential)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        private static bool TryGetFileId(string path, out BY_HANDLE_FILE_INFORMATION info)
        {
            info = default(BY_HANDLE_FILE_INFORMATION);
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    return GetFileInformationByHandle(fs.SafeFileHandle, out info);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSameFile(string a, string b)
        {
            BY_HANDLE_FILE_INFORMATION ia, ib;
            return TryGetFileId(a, out ia) && TryGetFileId(b, out ib)
                && ia.VolumeSerialNumber == ib.VolumeSerialNumber
                && ia.FileIndexHigh == ib.FileIndexHigh
                && ia.FileIndexLow == ib.FileIndexLow;
        }

        /// <summary>
        /// True when this game folder has the Classless patches (only the standard client gets them).
        /// </summary>
        public static bool HasClassless(string gamePath)
        {
            return !string.IsNullOrWhiteSpace(gamePath)
                && File.Exists(Path.Combine(gamePath, "Data", HeroLocale, "patch-enGB-V.MPQ"));
        }

        /// <summary>
        /// Makes Data\enGB match Data\enUS. Safe to call any time; does nothing when the folder has
        /// no Classless patches. Returns null when everything is in place, otherwise a short
        /// message for the player (for example when a file is held open by the running game).
        /// </summary>
        public static string Ensure(string gamePath)
        {
            if (!HasClassless(gamePath))
                return null;

            string stockDir = Path.Combine(gamePath, "Data", StockLocale);
            string heroDir = Path.Combine(gamePath, "Data", HeroLocale);
            if (!Directory.Exists(stockDir))
                return null;

            int failed = 0;
            string firstError = null;

            foreach (string source in Directory.GetFiles(stockDir))
            {
                string name = Path.GetFileName(source);
                if (name.IndexOf(".bak", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string heroName = name.Replace(StockLocale, HeroLocale);
                if (Delivered.Contains(heroName))
                    continue;

                string target = Path.Combine(heroDir, heroName);
                try
                {
                    // realmlist.wtf is tiny and the player may edit it: keep a plain copy in step.
                    if (name.Equals("realmlist.wtf", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!File.Exists(target) || File.ReadAllText(target) != File.ReadAllText(source))
                            File.Copy(source, target, true);
                        continue;
                    }

                    if (File.Exists(target))
                    {
                        if (IsSameFile(source, target))
                            continue; // linked, up to date

                        FileInfo s = new FileInfo(source);
                        FileInfo t = new FileInfo(target);
                        if (s.Length == t.Length && s.LastWriteTimeUtc == t.LastWriteTimeUtc)
                            continue; // a copy (drive without hard links) that is still current

                        File.Delete(target); // stale: the enUS file was replaced by an update
                    }

                    if (!CreateHardLink(target, source, IntPtr.Zero))
                    {
                        File.Copy(source, target, true);
                        File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source));
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    if (firstError == null)
                        firstError = heroName + ": " + ex.Message;
                }
            }

            if (failed == 0)
                return null;

            return "The Classless realm files could not be fully prepared (" + failed + " file(s)).\n"
                + "Close World of Warcraft and press Check for Updates again.\n\n" + firstError;
        }
    }
}
