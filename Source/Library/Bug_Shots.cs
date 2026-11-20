using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Oracle_Lite.Library
{
    /// <summary>
    /// Sends the screenshot that the in-game addon "Frostworn Bug Report" took for a bug report, so the
    /// team sees exactly what the player saw.
    ///
    /// - The addon remembers every report the server accepted in its SavedVariables
    ///   (FrostwornBugReportDB.reports: code, time, shot). The game writes that file on logout / reload.
    /// - WoW saves the screenshot in "&lt;game&gt;\Screenshots" in the same second as the report.
    /// - The server takes an image only for a recent report with that code, once per code, so nothing
    ///   secret is needed here. Players who untick "Take a screenshot" in the addon send nothing.
    /// - Everything is silent and best-effort: no popups, failures are retried later, never blocks the UI.
    /// </summary>
    internal static class Bug_Shots
    {
        private const string Endpoint = "https://frostworn.com/vision_lite/bugshot.php?code=";
        private const int MaxAgeDays = 7;
        private const long MaxBytes = 4 * 1024 * 1024;

        private static int running;
        // codes the server did not know yet (report still queued in the game): wait before sending again
        private static readonly Dictionary<string, DateTime> notBefore = new Dictionary<string, DateTime>();
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        private static readonly Regex BlockRx = new Regex(@"\{([^{}]*)\}", RegexOptions.Compiled);
        private static readonly Regex CodeRx = new Regex(@"\[""code""\]\s*=\s*""([A-HJ-NP-Z2-9]{5})""", RegexOptions.Compiled);
        private static readonly Regex TimeRx = new Regex(@"\[""t""\]\s*=\s*(\d{9,11})", RegexOptions.Compiled);
        private static readonly Regex ShotRx = new Regex(@"\[""shot""\]\s*=\s*true", RegexOptions.Compiled);
        private static readonly Regex TextRx = new Regex(@"\[""text""\]", RegexOptions.Compiled);

        private static string StateFile => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Frostworn Launcher", "bugshots.txt");

        /// <summary>Checks all known game folders once; a second call while one is running does nothing.</summary>
        public static async Task RunAsync(IEnumerable<string> gameDirs)
        {
            if (Interlocked.Exchange(ref running, 1) == 1)
                return;
            try
            {
                var dirs = gameDirs
                    .Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (dirs.Count > 0)
                    await Task.Run(() => Work(dirs));
            }
            catch
            {
                // best-effort: never bother the player with this
            }
            finally
            {
                Interlocked.Exchange(ref running, 0);
            }
        }

        private static async Task Work(List<string> gameDirs)
        {
            var done = LoadState();
            bool changed = false;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            foreach (string game in gameDirs)
            {
                foreach (var r in FindReports(game))
                {
                    if (done.Contains(r.Code) || now - r.Time > MaxAgeDays * 86400L)
                        continue;
                    if (notBefore.TryGetValue(r.Code, out DateTime wait) && DateTime.UtcNow < wait)
                        continue;

                    string shot = FindScreenshot(game, r.Time);
                    if (shot == null)
                    {
                        // no file (deleted, or saved as .tga): give up once the game has surely written it
                        if (now - r.Time > 3600) { done.Add(r.Code); changed = true; }
                        continue;
                    }

                    var result = await Upload(r.Code, shot);
                    if (result == UploadResult.Done || result == UploadResult.Rejected)
                    {
                        done.Add(r.Code);
                        changed = true;
                    }
                    else
                    {
                        // report not on the server yet / network / busy: try again in 30 minutes
                        notBefore[r.Code] = DateTime.UtcNow.AddMinutes(30);
                    }
                }
            }

            if (changed)
                SaveState(done);
        }

        private struct Report
        {
            public string Code;
            public long Time;
        }

        private static IEnumerable<Report> FindReports(string game)
        {
            string accounts = Path.Combine(game, "WTF", "Account");
            if (!Directory.Exists(accounts))
                yield break;

            foreach (string acc in SafeDirs(accounts))
            {
                string sv = Path.Combine(acc, "SavedVariables", "FrostwornBugReport.lua");
                string text;
                try
                {
                    if (!File.Exists(sv) || new FileInfo(sv).Length > 2 * 1024 * 1024)
                        continue;
                    text = File.ReadAllText(sv);
                }
                catch
                {
                    continue;
                }

                // innermost { ... } blocks: sent reports have code + t (+ shot); queued ones also have text
                foreach (Match b in BlockRx.Matches(text))
                {
                    string body = b.Groups[1].Value;
                    if (TextRx.IsMatch(body) || !ShotRx.IsMatch(body))
                        continue;
                    var c = CodeRx.Match(body);
                    var t = TimeRx.Match(body);
                    if (c.Success && t.Success && long.TryParse(t.Groups[1].Value, out long time))
                        yield return new Report { Code = c.Groups[1].Value, Time = time };
                }
            }
        }

        /// <summary>The .jpg in Screenshots written closest to the report time (a few seconds window).</summary>
        private static string FindScreenshot(string game, long time)
        {
            string dir = Path.Combine(game, "Screenshots");
            if (!Directory.Exists(dir))
                return null;
            string best = null;
            long bestDiff = long.MaxValue;
            try
            {
                foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*.jpg"))
                {
                    long written = new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeSeconds();
                    long diff = written - time;
                    if (diff < -3 || diff > 30 || f.Length < 1024 || f.Length > MaxBytes)
                        continue;
                    if (Math.Abs(diff) < bestDiff)
                    {
                        bestDiff = Math.Abs(diff);
                        best = f.FullName;
                    }
                }
            }
            catch
            {
                return null;
            }
            return best;
        }

        private enum UploadResult { Done, Rejected, Later }

        private static async Task<UploadResult> Upload(string code, string file)
        {
            try
            {
                byte[] data = File.ReadAllBytes(file);
                using (var content = new ByteArrayContent(data))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                    using (var resp = await http.PostAsync(Endpoint + code, content))
                    {
                        switch (resp.StatusCode)
                        {
                            case HttpStatusCode.OK:
                                return UploadResult.Done;
                            case HttpStatusCode.NotFound:          // report not on the server yet
                            case (HttpStatusCode)429:             // too many at once
                                return UploadResult.Later;
                            default:
                                return (int)resp.StatusCode >= 500 ? UploadResult.Later : UploadResult.Rejected;
                        }
                    }
                }
            }
            catch
            {
                return UploadResult.Later;
            }
        }

        private static IEnumerable<string> SafeDirs(string path)
        {
            try
            {
                return Directory.GetDirectories(path);
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private static HashSet<string> LoadState()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                if (File.Exists(StateFile))
                    foreach (string line in File.ReadAllLines(StateFile))
                        if (line.Trim().Length == 5)
                            set.Add(line.Trim());
            }
            catch
            {
            }
            return set;
        }

        private static void SaveState(HashSet<string> done)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StateFile));
                // keep the file small: the newest codes are enough (older reports are past MaxAgeDays anyway)
                File.WriteAllLines(StateFile, done.Skip(Math.Max(0, done.Count - 500)));
            }
            catch
            {
            }
        }
    }
}
