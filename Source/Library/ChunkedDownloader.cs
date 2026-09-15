using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Oracle_Lite.Library
{
    internal enum DownloadOutcome
    {
        Completed,
        Paused
    }

    /// <summary>
    /// Shared resumable download engine used for both the standard and HD
    /// client full-zip installs. Streams the response directly to disk (never
    /// buffers the whole file in memory) via HTTP Range requests, so a
    /// Pause/app-close/network drop can always resume from wherever the local
    /// file left off instead of starting a multi-GB download over from zero.
    ///
    /// Transient failures (dropped connection, server hiccup) are retried
    /// automatically with exponential backoff. Before each retry it first
    /// re-checks that the partial file on disk is still a sane prefix of the
    /// *current* remote resource (a cheap HEAD request) rather than blindly
    /// trusting stale local state and firing another Range request - if the
    /// check finds the local file can no longer be trusted, it is discarded so
    /// the next attempt starts clean instead of looping on a bad resume point.
    /// </summary>
    internal class ChunkedDownloader
    {
        private const int MaxRetries = 6;
        private const int BufferSize = 81920;

        /// <summary>Polled between chunks (and between retries) to stop cleanly on Pause.</summary>
        public Func<bool> IsPaused;

        /// <summary>Called periodically (a few times a second) with progress: totalRead, totalBytes, bytesPerSecond.</summary>
        public Action<long, long, double> OnProgress;

        /// <summary>Called with a short human-readable status line, e.g. during a retry backoff.</summary>
        public Action<string> OnStatus;

        public async Task<DownloadOutcome> DownloadAsync(string url, string destinationPath, long knownSizeFallback)
        {
            int attempt = 0;

            while (true)
            {
                try
                {
                    return await DownloadOnceAsync(url, destinationPath, knownSizeFallback);
                }
                catch when (attempt < MaxRetries && !(IsPaused?.Invoke() ?? false))
                {
                    attempt++;
                    int delaySeconds = Math.Min(60, (int)Math.Pow(2, attempt));

                    OnStatus?.Invoke($"Connection interrupted - checking progress, retrying in {delaySeconds}s... (attempt {attempt}/{MaxRetries})");

                    // "Check" step before resuming: confirm the partial file on
                    // disk is still trustworthy against the *current* remote
                    // resource before firing another Range request against it.
                    if (!await IsResumePointStillValidAsync(url, destinationPath))
                    {
                        OnStatus?.Invoke("Saved progress no longer matches the server - restarting this file.");
                        if (File.Exists(destinationPath))
                            File.Delete(destinationPath);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                    if (IsPaused?.Invoke() ?? false)
                        return DownloadOutcome.Paused;
                }
            }
        }

        /// <summary>
        /// Best-effort check that a local partial file is still a valid prefix
        /// of the remote file before resuming from it: it must not be larger
        /// than the server's current Content-Length. If the check itself can't
        /// complete (HEAD blocked/unsupported, still offline), progress is NOT
        /// discarded on that basis alone - only a confirmed mismatch does that.
        /// </summary>
        private static async Task<bool> IsResumePointStillValidAsync(string url, string destinationPath)
        {
            if (!File.Exists(destinationPath))
                return true;

            long localSize = new FileInfo(destinationPath).Length;
            if (localSize <= 0)
                return true;

            try
            {
                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(30);

                    using (var head = new HttpRequestMessage(HttpMethod.Head, url))
                    using (HttpResponseMessage response = await http.SendAsync(head))
                    {
                        if (!response.IsSuccessStatusCode)
                            return true;

                        if (response.Content.Headers.ContentLength.HasValue
                            && localSize > response.Content.Headers.ContentLength.Value)
                        {
                            return false;
                        }
                    }
                }
            }
            catch
            {
                return true;
            }

            return true;
        }

        private async Task<DownloadOutcome> DownloadOnceAsync(string url, string destinationPath, long knownSizeFallback)
        {
            long existingBytes = File.Exists(destinationPath) ? new FileInfo(destinationPath).Length : 0;

            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromHours(24);

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (existingBytes > 0)
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);

                using (HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                    {
                        // We asked to resume past the end of what the server has -
                        // the local partial file no longer matches; drop it and
                        // let the retry loop above start this attempt over clean.
                        if (File.Exists(destinationPath))
                            File.Delete(destinationPath);
                        throw new IOException("Requested range not satisfiable - resume point was stale.");
                    }

                    response.EnsureSuccessStatusCode();

                    long totalBytes = existingBytes;
                    if (response.Content.Headers.ContentLength.HasValue)
                        totalBytes += response.Content.Headers.ContentLength.Value;
                    if (totalBytes <= 0)
                        totalBytes = knownSizeFallback;

                    using (Stream httpStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(destinationPath, existingBytes > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write))
                    {
                        byte[] buffer = new byte[BufferSize];
                        long totalRead = existingBytes;
                        long bytesSinceUpdate = 0;
                        DateTime lastUpdate = DateTime.Now;
                        Stopwatch speedWatch = Stopwatch.StartNew();

                        int read;
                        while ((read = await httpStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;
                            bytesSinceUpdate += read;

                            if ((DateTime.Now - lastUpdate).TotalMilliseconds >= 250)
                            {
                                lastUpdate = DateTime.Now;
                                double seconds = speedWatch.Elapsed.TotalSeconds;
                                double bytesPerSecond = seconds > 0 ? bytesSinceUpdate / seconds : 0;
                                OnProgress?.Invoke(totalRead, totalBytes, bytesPerSecond);
                                speedWatch.Restart();
                                bytesSinceUpdate = 0;
                            }

                            if (IsPaused?.Invoke() ?? false)
                                return DownloadOutcome.Paused;
                        }
                    }
                }
            }

            return DownloadOutcome.Completed;
        }

        /// <summary>
        /// Best-effort post-download integrity check: fetches "&lt;url&gt;.sha256"
        /// (a plain-text sidecar file published next to the zip, containing
        /// just the 64-character hex digest) and compares it against a
        /// freshly computed SHA256 of the local file. Returns true if it
        /// matches, false if it's a CONFIRMED mismatch (the file is
        /// corrupted and should be discarded so the player doesn't sit
        /// through an extraction that will fail anyway), or null if the
        /// check itself couldn't be completed (sidecar missing, network
        /// hiccup) - a null result must never block the player, since this
        /// is a bonus check on top of the zip format's own per-entry CRC32
        /// validation during extraction, not the only line of defense.
        /// </summary>
        public static async Task<bool?> VerifyIntegrityAsync(string url, string filePath, Action<double> onProgress)
        {
            string expectedHash;
            try
            {
                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(20);
                    expectedHash = (await http.GetStringAsync(url + ".sha256")).Trim().ToLowerInvariant();
                }
            }
            catch
            {
                return null;
            }

            if (expectedHash.Length != 64 || !IsHex(expectedHash))
                return null;

            string actualHash = await ComputeSha256WithProgressAsync(filePath, onProgress);
            return string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHex(string s)
        {
            foreach (char c in s)
            {
                bool isHexChar = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!isHexChar) return false;
            }
            return true;
        }

        private static async Task<string> ComputeSha256WithProgressAsync(string filePath, Action<double> onProgress)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] buffer = new byte[1024 * 1024];
                long totalRead = 0;
                long totalSize = stream.Length;
                DateTime lastUpdate = DateTime.Now;

                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    sha256.TransformBlock(buffer, 0, read, buffer, 0);
                    totalRead += read;

                    if ((DateTime.Now - lastUpdate).TotalMilliseconds >= 250)
                    {
                        lastUpdate = DateTime.Now;
                        onProgress?.Invoke(totalSize > 0 ? (double)totalRead / totalSize * 100 : 0);
                    }
                }

                sha256.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(sha256.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
