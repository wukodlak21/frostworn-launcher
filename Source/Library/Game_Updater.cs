using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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
        /// Returns true or false if file is different or doesn't exist
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

                return localFile.Length != file.Size;
            }
            catch (Exception)
            {
                // file doesn't exist
                return true;
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

        /// <summary>
        /// Event for download completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Completed(object sender, AsyncCompletedEventArgs e)
        {
            SWSpeed.Reset(); // download speed timer

            if (e.Cancelled == true)
            {
                OnStopped(e);
            }
            else
            {
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
}
