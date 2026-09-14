using Oracle_Lite.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;

namespace Oracle_Lite.Library
{
    internal class Game_Updater
    {
        // Define a delegate for the progress change event
        public delegate void ProgressChangedEventHandler(object sender, DownloadProgressChangedEventArgs e);
        // Define a delegate for the completed event
        public delegate void CompletedEventHandler(object sender, AsyncCompletedEventArgs e);

        // Declare the progress change event using the delegate
        public event ProgressChangedEventHandler ProgressChangedEvent;
        // Declare the completed event using the delegate
        public event CompletedEventHandler CompletedEvent;
        // Declare the completed event using the delegate
        public event CompletedEventHandler StoppedEvent;

        // Hook for ProgressChangedEvent
        private void OnProgressChanged(DownloadProgressChangedEventArgs e) => ProgressChangedEvent?.Invoke(this, e);
        // Hook for CompletedEvent
        private void OnCompleted(AsyncCompletedEventArgs e) => CompletedEvent?.Invoke(this, e);
        // Hook for StoppedEvent
        private void OnStopped(AsyncCompletedEventArgs e) => StoppedEvent?.Invoke(this, e);

        private List<Newton_Workloader.GameFilesListResponse> DownloadList = new List<Newton_Workloader.GameFilesListResponse>();
        private WebClient updater = new WebClient();
        public Stopwatch SWSpeed = new Stopwatch();
        private DateTime WstartTime;

        public bool StopRequested = false;

        public long TotalSizeToDownload; // in bytes
        public long TotalSizeDownloaded; // in bytes

        /// <summary>
        /// Returns true only if there is something new to download
        /// </summary>
        /// <returns>true or false</returns>
        public async Task<bool> UpdateList()
        {
            DownloadList.Clear();
            TotalSizeToDownload = 0;
            TotalSizeDownloaded = 0;

            List<Newton_Workloader.GameFilesListResponse> gameFilesList = await Api_Caller.GameFilesListResponse();

            if (gameFilesList != null)
            {
                Launcher launcherWindow = Application.Current.MainWindow as Launcher;

                foreach (Newton_Workloader.GameFilesListResponse file in gameFilesList)
                {
                    file.TargetPath = Properties.Settings.Default.GamePath + file.TargetPath;

                    await Task.Delay(5);
                    launcherWindow.StatusHolder.Text = $"CHECKING FILE {file.Name}..";

                    if (!Properties.Settings.Default.HDTextures) // if hd textures is disabled
                    {
                        if (!file.IsHD) // if file is NOT hd texture, add to download list
                        {
                            if (FileIsDifferentAsync(file))
                            {
                                DownloadList.Add(file);
                            }
                        }
                    }
                    else // if hd textures is enabled, adds any file to download list
                    {
                        if (FileIsDifferentAsync(file))
                        {
                            DownloadList.Add(file);
                        }
                    }
                }

                TotalSizeToDownload = DownloadList.Sum(item => item.Size);

                return DownloadList.Count > 0;
            }
            else
            {
                // ..
            }

            return false;
        }

        /// <summary>
        /// Returns true or false if file is different or doesn't exist. A size
        /// match alone doesn't prove the content is intact - a corrupted file
        /// of the exact same byte length (bit-flip, partial overwrite) used to
        /// be silently treated as up to date. When the server provides a
        /// Sha256, a same-size local file is hashed and compared too.
        /// </summary>
        /// <param name="file"></param>
        /// <returns>true or false</returns>
        private bool FileIsDifferentAsync(Newton_Workloader.GameFilesListResponse file)
        {
            try
            {
                if (file.TargetPath.ToLower().Contains("config.wtf"))
                {
                    return false;
                }

                FileInfo localFile = new FileInfo(file.TargetPath);

                if (localFile.Length != file.Size)
                    return true;

                if (!string.IsNullOrEmpty(file.Sha256))
                {
                    string localHash = ComputeSha256(file.TargetPath);
                    return !string.Equals(localHash, file.Sha256, StringComparison.OrdinalIgnoreCase);
                }

                return false;
            }
            catch (Exception)
            {
                // file doesn't exist
                return true;
            }
        }

        private static string ComputeSha256(string path)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Starts downloading new or missing game files
        /// </summary>
        public bool Start()
        {
            StopRequested = false;

            Launcher launcherWindow = Application.Current.MainWindow as Launcher;
            launcherWindow.spDownloadStatus.Visibility = Visibility.Visible;

            if (DownloadList.Count > 0)
            {
                Newton_Workloader.GameFilesListResponse file = DownloadList[0]; // Get the first file

                if (!string.IsNullOrEmpty(Path.GetDirectoryName(file.TargetPath))
                    && !string.IsNullOrWhiteSpace(Path.GetDirectoryName(file.TargetPath)))
                    if (!Directory.Exists(Path.GetDirectoryName(file.TargetPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(file.TargetPath));

                launcherWindow.StatusHolder.Text = $"DOWNLOADING {file.Name}";
                DownloadFile(file.Url, file.TargetPath);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Stops the downloader but doesn't delete what's downloaded
        /// </summary>
        public void Stop()
        {
            StopRequested = true;
        }

        /// <summary>
        /// Downloads a file from specified url to destionation path
        /// </summary>
        /// <param name="url"></param>
        /// <param name="destination"></param>
        private void DownloadFile(string url, string destination)
        {
            WstartTime = DateTime.Now;

            using (updater = new WebClient())
            {
                updater.DownloadProgressChanged += new DownloadProgressChangedEventHandler(ProgressChanged); // progress change event

                updater.DownloadFileCompleted += new AsyncCompletedEventHandler(Completed); // completed event

                Uri downloadURL = new Uri(url);

                SWSpeed.Start(); // Start the stopwatch which we will be using to calculate the download speed

                updater.DownloadFileAsync(downloadURL, destination);
            }
        }

        /// <summary>
        /// Event for download progress
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            TimeSpan span = DateTime.Now - WstartTime;

            if (span.TotalMilliseconds >= 250)
            {
                WstartTime = DateTime.Now;

                OnProgressChanged(e);
            }

            if (StopRequested)
            {
                CancelUpdating();
            }
        }

        /// <summary>
        /// Cancels the downloader
        /// </summary>
        private void CancelUpdating()
        {
            updater.CancelAsync();
        }

        private const int MaxFileRetries = 5;
        private int currentFileRetryCount = 0;

        /// <summary>
        /// Event for download completed. Previously this only checked
        /// e.Cancelled and treated ANY other outcome as success - a real
        /// network failure mid-file (e.Error != null) was silently marked
        /// done, the file removed from the queue, and the launcher would go
        /// on to report "UP TO DATE"/"COMPLETED" with a truncated/corrupt file
        /// left on disk. Now a failed file is deleted and retried a few times
        /// (with a short, growing delay) - a "check, then continue" step -
        /// before giving up and surfacing the failure instead of hiding it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Completed(object sender, AsyncCompletedEventArgs e)
        {
            SWSpeed.Reset(); // download speed timer

            if (e.Cancelled == true)
            {
                currentFileRetryCount = 0;
                OnStopped(e);
                return;
            }

            if (e.Error != null)
            {
                currentFileRetryCount++;

                if (currentFileRetryCount <= MaxFileRetries && DownloadList.Count > 0)
                {
                    string failedPath = DownloadList[0].TargetPath;
                    if (File.Exists(failedPath))
                    {
                        try { File.Delete(failedPath); }
                        catch { /* best-effort - the retry will overwrite it anyway */ }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(10, 2 * currentFileRetryCount)));
                    Start(); // retries DownloadList[0]
                    return;
                }

                // Retries exhausted - stop rather than silently marking this file done.
                currentFileRetryCount = 0;
                string failedName = DownloadList.Count > 0 ? DownloadList[0].Name : "a game file";
                Custom_MessageBox.Show(
                    $"Failed to download {failedName} after several attempts:\n{e.Error.Message}\n\nCheck your connection and press Update again to retry.",
                    "Update Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                OnStopped(e); // e.Error is already set (AsyncCompletedEventArgs' constructor is protected, so this is reused, not rebuilt)
                return;
            }

            currentFileRetryCount = 0;
            TotalSizeDownloaded += DownloadList[0].Size;

            DownloadList.RemoveAt(0);

            if (DownloadList.Count > 0) // continue what's left
            {
                Start();
            }
            else // all downloads completed
            {
                OnCompleted(e);
            }
        }
    }
}
