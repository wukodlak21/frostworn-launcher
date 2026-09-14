using Oracle_Lite.Controllers;
using Oracle_Lite.Library;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Oracle_Lite
{
    public partial class Launcher : Window
    {
        private Game_Updater gameUpdater = new Game_Updater();
        public bool isUpdating = false;

        private readonly DispatcherTimer serverStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private int secondsSinceStatusRefresh = 0;
        private const int StatusRefreshIntervalSeconds = 30;

        private string lastWantedRealmKey = null;

        public Launcher()
        {
            InitializeComponent();
            DataContext = this;
            VersionHolder.Text = GetAppVersion();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        /// <summary>
        /// Forces this window to the foreground even when Windows would normally
        /// deny it (e.g. right after the self-update relaunch, where the OS treats
        /// the new process as a background app and just flashes its taskbar icon
        /// instead of activating it). Works by briefly attaching this thread's
        /// input to whatever thread currently owns the foreground window, which
        /// Windows allows to call SetForegroundWindow on our own window.
        /// </summary>
        private void ForceActivate()
        {
            try
            {
                var hWnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                IntPtr foreground = GetForegroundWindow();
                uint foregroundThreadId = GetWindowThreadProcessId(foreground, IntPtr.Zero);
                uint thisThreadId = GetCurrentThreadId();

                if (foregroundThreadId != thisThreadId)
                    AttachThreadInput(thisThreadId, foregroundThreadId, true);

                ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
                Activate();
                Topmost = true;
                Topmost = false;
                Focus();

                if (foregroundThreadId != thisThreadId)
                    AttachThreadInput(thisThreadId, foregroundThreadId, false);
            }
            catch
            {
                // Best-effort - never block startup over focus stealing
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ForceActivate();

            ButtonSettings.IsEnabled = false;
            ButtonCheckUpdates.IsEnabled = false;
            CheckBoxHD.IsEnabled = false;
            spDownloadStatus.Visibility = Visibility.Hidden;
            TipHolder.Visibility = Visibility.Hidden;

            Slider.Start();

            serverStatusTimer.Tick += ServerStatusTimer_Tick;
            serverStatusTimer.Start();
            _ = RefreshServerStatus();

            // Automatic HD install check: if a previously remembered HD folder
            // was moved or deleted, forget it now rather than silently acting
            // as if the HD client were still installed the next time the HD
            // button (or CheckForUpdates) looks at this setting.
            string savedHDPath = Properties.Settings.Default.HDGamePath;
            if (!string.IsNullOrWhiteSpace(savedHDPath) && !File.Exists(Path.Combine(savedHDPath, "Wow.exe")))
            {
                Properties.Settings.Default.HDGamePath = "";
                Properties.Settings.Default.Save();
            }

            if (string.IsNullOrEmpty(Properties.Settings.Default.GamePath) || string.IsNullOrWhiteSpace(Properties.Settings.Default.GamePath))
            {
                GameFinderDialog.Show();

                // ButtonSettings starts disabled above and is normally only
                // re-enabled by CheckForUpdates() completing - which never
                // happens on a first run with no GamePath yet. Without this,
                // clicking Cancel on the dialog left the gear icon (the only
                // way to reopen it) permanently dead for the rest of the
                // session, with no way back in short of restarting the app.
                ButtonSettings.IsEnabled = true;
            }
            else
            {
                CheckForUpdates();
            }
        }

        private void ServerStatusTimer_Tick(object sender, EventArgs e)
        {
            secondsSinceStatusRefresh++;
            StatusRefreshedHolder.Text = $"refreshed {secondsSinceStatusRefresh}s ago";

            if (secondsSinceStatusRefresh >= StatusRefreshIntervalSeconds)
            {
                _ = RefreshServerStatus();
            }
        }

        private async Task RefreshServerStatus()
        {
            var response = await Api_Caller.ServerStatusResponse();
            secondsSinceStatusRefresh = 0;

            var offlineBrush = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x87));
            var onlineBrush = new SolidColorBrush(Color.FromRgb(0x21, 0xA8, 0x43));

            if (response == null || !response.Available || response.Realms == null)
            {
                IccOnlineHolder.Text = "—";
                PrgOnlineHolder.Text = "—";
                IccBotsHolder.Text = "";
                PrgBotsHolder.Text = "";
                TotalOnlineHolder.Text = "status unavailable";
                StatusRefreshedHolder.Text = "retrying…";
                StatusPulseDot.Fill = offlineBrush;
                IccStatusDot.Fill = offlineBrush;
                PrgStatusDot.Fill = offlineBrush;
                return;
            }

            var icc = response.Realms.FirstOrDefault(r => r.Key == "ICC");
            var prg = response.Realms.FirstOrDefault(r => r.Key == "PRG");

            IccOnlineHolder.Text = icc != null ? icc.Online.ToString("N0") : "—";
            PrgOnlineHolder.Text = prg != null ? prg.Online.ToString("N0") : "—";
            IccBotsHolder.Text = icc != null ? $" · {icc.Bots:N0} bots" : "";
            PrgBotsHolder.Text = prg != null ? $" · {prg.Bots:N0} bots" : "";
            TotalOnlineHolder.Text = $"{response.Total:N0} real players";
            StatusRefreshedHolder.Text = "refreshed just now";
            StatusPulseDot.Fill = onlineBrush;
            IccStatusDot.Fill = onlineBrush;
            PrgStatusDot.Fill = onlineBrush;
        }

        public void RefreshWantedForTag(string slideTag)
        {
            string realmKey = "ICC";
            string realmLabel = "LEGACY X5";

            if (string.Equals(slideTag, "THUNDERSTORM X1", StringComparison.OrdinalIgnoreCase))
            {
                realmKey = "PRG";
                realmLabel = "THUNDERSTORM X1";
            }

            if (realmKey == lastWantedRealmKey) return;
            lastWantedRealmKey = realmKey;

            WantedRealmHolder.Text = realmLabel;
            _ = RefreshWanted(realmKey);
        }

        private async Task RefreshWanted(string realmKey)
        {
            var response = await Api_Caller.MostWantedResponse(realmKey);

            var rows = new[]
            {
                (Name: Wanted1Name, Stars: Wanted1Stars, Bounty: Wanted1Bounty),
                (Name: Wanted2Name, Stars: Wanted2Stars, Bounty: Wanted2Bounty),
                (Name: Wanted3Name, Stars: Wanted3Stars, Bounty: Wanted3Bounty),
            };

            var players = (response != null && response.Available && response.Players != null)
                ? response.Players
                : new List<Newton_Workloader.WantedPlayer>();

            for (int i = 0; i < rows.Length; i++)
            {
                if (i < players.Count)
                {
                    var player = players[i];
                    int stars = Math.Max(0, Math.Min(5, player.Level));
                    rows[i].Name.Text = player.Name;
                    rows[i].Stars.Text = new string('★', stars) + new string('☆', 5 - stars);
                    rows[i].Bounty.Text = $"{player.Bounty:N0}g";
                }
                else
                {
                    rows[i].Name.Text = "—";
                    rows[i].Stars.Text = "☆☆☆☆☆";
                    rows[i].Bounty.Text = "";
                }
            }

            WantedFootHolder.Text = players.Count > 0 ? "claim a bounty · kill on sight" : "no active bounties right now";
        }

        public string AppVersion
        {
            get { return GetAppVersion(); }
        }

        private string GetAppVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        /// <summary>
        /// Checks if a new launcher version is available and auto-updates if so.
        /// Downloads new exe, writes a batch updater, launches it, then exits.
        /// </summary>
        private async Task CheckForLauncherUpdate()
        {
            try
            {
                var response = await Api_Caller.LauncherVersionResponse();
                if (response == null) return;

                string currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                if (response.Version == currentVersion) return;

                StatusHolder.Text = "UPDATING LAUNCHER...";
                TipHolder.Visibility = Visibility.Visible;
                TipHolder.Text = $"New launcher version {response.Version} — please wait...";

                string launcherPath = Assembly.GetExecutingAssembly().Location;
                string launcherDir  = Path.GetDirectoryName(launcherPath);
                string updatePath   = Path.Combine(launcherDir, "_launcher_update.exe");
                string batPath      = Path.Combine(launcherDir, "_update_launcher.bat");

                var downloader = new ChunkedDownloader
                {
                    IsPaused = () => false, // self-update isn't user-pausable
                    OnProgress = (totalRead, totalBytes, bytesPerSecond) =>
                        DownloadBar.Value = (double)totalRead / totalBytes * 100
                };
                await downloader.DownloadAsync(response.Url, updatePath, new FileInfo(launcherPath).Length);

                File.WriteAllText(batPath,
                    "@echo off\r\n" +
                    "timeout /t 2 /nobreak > nul\r\n" +
                    $"copy /y \"{updatePath}\" \"{launcherPath}\"\r\n" +
                    $"del \"{updatePath}\"\r\n" +
                    $"del \"%~f0\"\r\n" +
                    $"start \"\" \"{launcherPath}\"\r\n"
                );

                Process.Start(new ProcessStartInfo(batPath)
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                Application.Current.Shutdown();
            }
            catch
            {
                // Silent fail — launcher update is not critical
            }
        }

        public async void CheckForUpdates()
        {
            PlayButton.IsEnabled = false;
            ButtonSettings.IsEnabled = false;
            ButtonCheckUpdates.IsEnabled = false;
            CheckBoxHD.IsEnabled = false;
            ButtonHDClient.IsEnabled = false;

            // Check for launcher self-update first
            await CheckForLauncherUpdate();

            if (await gameUpdater.UpdateList())
            {
                PlayButton.Visibility = Visibility.Collapsed;
                UpdateButton.Visibility = Visibility.Visible;
                CancelUpdateButton.Visibility = Visibility.Collapsed;
                TipHolder.Visibility = Visibility.Visible;

                StatusHolder.Text = "UPDATES AVAILABLE!";
                TipHolder.Text = "Press Update button to start downloading!";
            }
            else
            {
                TipHolder.Visibility = Visibility.Visible;

                StatusHolder.Text = "UP TO DATE";
                TipHolder.Text = "Press the Start button to run the game.";
                DownloadBar.Value = 100;
            }

            PlayButton.IsEnabled = true;
            ButtonSettings.IsEnabled = true;
            ButtonCheckUpdates.IsEnabled = true;
            CheckBoxHD.IsEnabled = true;
            ButtonHDClient.IsEnabled = true;
        }

        private void ButtonMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            ExitDialog.Show();
        }

        private void ButtonHome_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://frostworn.com");
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Animations.FadeOut(SocialsPopup, 300);
        }

        private void ButtonSocials_Click(object sender, RoutedEventArgs e)
        {
            if (SocialsPopup.Visibility != Visibility.Visible)
                Animations.FadeIn(SocialsPopup, 300);
            else
                Animations.FadeOut(SocialsPopup, 300);
        }

        private void ButtonCommunity_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/63KSeFK3B");
        }

        private void ButtonSupport_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://frostworn.com/#contact");
        }

        private void ButtonDonate_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://frostworn.com/shop.php");
        }

        private void ButtonVote_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://frostworn.com/vote_redirect.php");
        }

        private void ButtonLearnMore_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(ButtonLearnMore.Tag.ToString());
        }

        private void ButtonSettings_Click(object sender, RoutedEventArgs e)
        {
            GameFinderDialog.Show();
        }

        private void ButtonCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            CheckForUpdates();
        }

        private void SlideButtonLeft_Click(object sender, RoutedEventArgs e)
        {
            Slider.GoPrevious();
        }

        private void SlideButtonRight_Click(object sender, RoutedEventArgs e)
        {
            Slider.GoNext();
        }

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            string gamepath = Properties.Settings.Default.GamePath;
            string WowExePath = gamepath + "\\Wow.exe";

            if (File.Exists(WowExePath))
            {
                PlayButton.Content = "LAUNCHING...";
                PlayButton.IsEnabled = false;

                try
                {
                    // clear cache
                    if (Directory.Exists($"{gamepath}\\Cache") && Properties.Settings.Default.ClearCache)
                    { 
                       try
                       {
                        var dir = new DirectoryInfo($"{gamepath}\\Cache");
                        dir.Delete(true);
                       }
                       catch { }
                    }

                    await Task.Delay(2000);
                    WindowState = WindowState.Minimized;
                    Process.Start(WowExePath);

                    PlayButton.Content = "PLAY";
                    PlayButton.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    string message = $"[File '{Extensions.GetCurrentCallerFileName()}' - Method 'PlayButton_Click']\r\nException error: {ex.Message}";
                    MessageBox.Show(message);
                    PlayButton.Content = "PLAY";
                    PlayButton.IsEnabled = true;
                }
            }
            else
            {
                GameFinderDialog.Show();
                Dispatcher.Invoke(() =>
                {
                    PlayButton.IsEnabled = true;
                    PlayButton.Content = "PLAY";
                });
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (gameUpdater.Start())
            {
                isUpdating = true;
                DownloadBar.Value = 0;
                PlayButton.Visibility = Visibility.Collapsed;
                UpdateButton.Visibility = Visibility.Collapsed;
                CancelUpdateButton.Visibility = Visibility.Visible;
                // CancelUpdateButton is shared with the standard-client full
                // download's Pause/Resume toggle, which overwrites this Content
                // with "Pause"/"Resume" - reset it back to its Stop label here
                // since the two flows never run at the same time.
                CancelUpdateButton.Content = "STOP";
                TipHolder.Visibility = Visibility.Hidden;
                spDownloadStatus.Visibility = Visibility.Visible;
                ButtonSettings.IsEnabled = false;
                ButtonCheckUpdates.IsEnabled = false;
                CheckBoxHD.IsEnabled = false;

                gameUpdater.ProgressChangedEvent += OnGameUpdateProgressChanged;
                gameUpdater.CompletedEvent += OnGameUpdateCompleted;
            }
        }

        private void CancelUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (stdDownloadActive)
            {
                // Same Pause/Resume toggle as the HD client's button - this one
                // just doubles up on the Stop button used by the (separate,
                // older) incremental patch updater below, since the two never
                // run at the same time.
                if (stdDownloadPaused)
                {
                    stdDownloadPaused = false;
                    CancelUpdateButton.Content = "Pause";
                    StatusHolder.Text = "DOWNLOADING GAME...";
                    TipHolder.Text = "Downloading World of Warcraft 3.3.5a...";
                    RunStandardClientDownload();
                }
                else
                {
                    stdDownloadPaused = true;
                    CancelUpdateButton.Content = "Resume";
                    StatusHolder.Text = "PAUSED";
                    TipHolder.Text = "Download paused. Click Resume to continue.";
                }
                return;
            }

            isUpdating = false;
            gameUpdater.Stop();
            gameUpdater.StoppedEvent += OnGameUpdateStopped;
        }

        private void OnGameUpdateProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            DownloadBar.Value = (long)((double)(gameUpdater.TotalSizeDownloaded + e.BytesReceived) / gameUpdater.TotalSizeToDownload * 100);
            DownloadedHolder.Text = Extensions.SizeSuffix(gameUpdater.TotalSizeDownloaded + e.BytesReceived, 2);
            TotalSizeHolder.Text = Extensions.SizeSuffix(gameUpdater.TotalSizeToDownload, 2);
            SpeedHolder.Text = $"({e.BytesReceived / 1024d / 1024d / gameUpdater.SWSpeed.Elapsed.TotalSeconds:0.00} MB/S)";
        }

        private void OnGameUpdateCompleted(object sender, AsyncCompletedEventArgs e)
        {
            isUpdating = false;
            DownloadBar.Value = 100;
            PlayButton.Visibility = Visibility.Visible;
            UpdateButton.Visibility = Visibility.Collapsed;
            CancelUpdateButton.Visibility = Visibility.Collapsed;
            spDownloadStatus.Visibility = Visibility.Hidden;
            TipHolder.Visibility = Visibility.Visible;
            TipHolder.Text = "Press the Start button to run the game.";
            StatusHolder.Text = "COMPLETED";
            ButtonSettings.IsEnabled = true;
            ButtonCheckUpdates.IsEnabled = true;
            CheckBoxHD.IsEnabled = true;
        }

        private void OnGameUpdateStopped(object sender, AsyncCompletedEventArgs e)
        {
            isUpdating = false;
            DownloadBar.Value = 0;
            PlayButton.Visibility = Visibility.Collapsed;
            UpdateButton.Visibility = Visibility.Visible;
            CancelUpdateButton.Visibility = Visibility.Collapsed;
            TipHolder.Visibility = Visibility.Visible;
            spDownloadStatus.Visibility = Visibility.Hidden;
            ButtonSettings.IsEnabled = true;
            ButtonCheckUpdates.IsEnabled = true;
            CheckBoxHD.IsEnabled = true;
            StatusHolder.Text = "UPDATES AVAILABLE!";
        }

        private void ButtonSettings_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            SettingsPopup.TextHolder.Text = "Game Location";
            Animations.FadeIn(SettingsPopup, 300);
        }

        private void ButtonSettings_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Animations.FadeOut(SettingsPopup, 300);
        }

        private void ButtonCheckUpdates_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            CheckUpdatesPopup.TextHolder.Text = "Force check for updates";
            Animations.FadeIn(CheckUpdatesPopup, 300);
        }

        private void ButtonCheckUpdates_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Animations.FadeOut(CheckUpdatesPopup, 300);
        }

        private void CheckBoxCache_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ClearCache = true;
            Properties.Settings.Default.Save();
        }

        private void CheckBoxCache_Unchecked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ClearCache = false;
            Properties.Settings.Default.Save();
        }

        private void CheckBoxHD_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.HDTextures = true;
            Properties.Settings.Default.Save();
        }

        private void CheckBoxHD_Unchecked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.HDTextures = false;
            Properties.Settings.Default.Save();
        }

        private const string StandardClientZipUrl = "https://pub-8ee94134dece4549a4d9961c8bcdaa0f.r2.dev/World%20of%20Warcraft%203.3.5a.zip";
        private const long StandardClientKnownSize = 17179869184L; // ~16 GiB fallback if the server ever omits Content-Length

        private bool stdDownloadActive = false;
        private bool stdDownloadPaused = false;
        private string stdInstallDir;
        private string stdTempZip;

        /// <summary>
        /// Starts (or resumes, if a partial WoW_install.zip is already sitting
        /// in the target folder from a previous attempt) downloading the
        /// standard client. Uses the same ChunkedDownloader engine, resumable
        /// Pause/Resume toggle (via CancelUpdateButton) and retry-with-backoff
        /// behavior as the HD client download.
        /// </summary>
        public void StartGameDownload()
        {
            string gamePath = Properties.Settings.Default.GamePath;
            stdInstallDir = Path.Combine(gamePath, "Frostworn WoW 3.3.5a");
            Directory.CreateDirectory(stdInstallDir);
            stdTempZip = Path.Combine(stdInstallDir, "WoW_install.zip");

            stdDownloadActive = true;
            stdDownloadPaused = false;

            PlayButton.Visibility = Visibility.Collapsed;
            UpdateButton.Visibility = Visibility.Collapsed;
            CancelUpdateButton.Visibility = Visibility.Visible;
            CancelUpdateButton.IsEnabled = true;
            CancelUpdateButton.Content = "Pause";
            ButtonSettings.IsEnabled = false;
            ButtonCheckUpdates.IsEnabled = false;
            ButtonHDClient.IsEnabled = false;
            CheckBoxHD.IsEnabled = false;
            spDownloadStatus.Visibility = Visibility.Visible;
            TipHolder.Visibility = Visibility.Visible;
            TipHolder.Text = "Downloading World of Warcraft 3.3.5a...";
            StatusHolder.Text = "DOWNLOADING GAME...";
            DownloadBar.Value = 0;

            RunStandardClientDownload();
        }

        private async void RunStandardClientDownload()
        {
            try
            {
                var downloader = new ChunkedDownloader
                {
                    IsPaused = () => stdDownloadPaused,
                    OnStatus = status => TipHolder.Text = status,
                    OnProgress = (totalRead, totalBytes, bytesPerSecond) =>
                    {
                        DownloadBar.Value = (double)totalRead / totalBytes * 100;
                        DownloadedHolder.Text = Extensions.SizeSuffix(totalRead, 2);
                        TotalSizeHolder.Text = Extensions.SizeSuffix(totalBytes, 2);
                        SpeedHolder.Text = $"({bytesPerSecond / 1024d / 1024d:0.00} MB/S)";
                    }
                };

                DownloadOutcome outcome = await downloader.DownloadAsync(StandardClientZipUrl, stdTempZip, StandardClientKnownSize);

                if (outcome == DownloadOutcome.Paused)
                {
                    // Leave the partial file in place; next click resumes via Range header
                    return;
                }

                StatusHolder.Text = "EXTRACTING...";
                TipHolder.Text = "Please wait, extracting game files...";
                DownloadBar.IsIndeterminate = true;
                CancelUpdateButton.IsEnabled = false;

                string installDir = stdInstallDir;
                string tempZip = stdTempZip;

                await Task.Run(() =>
                {
                    using (var zip = ZipFile.OpenRead(tempZip))
                    {
                        string topDir = "";
                        if (zip.Entries.Count > 0)
                        {
                            string firstEntry = zip.Entries[0].FullName;
                            int slashIdx = firstEntry.IndexOf('/');
                            if (slashIdx > 0)
                            {
                                string candidate = firstEntry.Substring(0, slashIdx + 1);
                                bool allMatch = true;
                                foreach (var entry in zip.Entries)
                                {
                                    if (!entry.FullName.StartsWith(candidate))
                                    {
                                        allMatch = false;
                                        break;
                                    }
                                }
                                if (allMatch) topDir = candidate;
                            }
                        }

                        foreach (var entry in zip.Entries)
                        {
                            string rel = entry.FullName.Substring(topDir.Length);
                            if (string.IsNullOrEmpty(rel)) continue;

                            string target = Path.Combine(installDir, rel.Replace('/', Path.DirectorySeparatorChar));

                            if (entry.FullName.EndsWith("/"))
                            {
                                Directory.CreateDirectory(target);
                            }
                            else
                            {
                                string dir = Path.GetDirectoryName(target);
                                if (!string.IsNullOrEmpty(dir))
                                    Directory.CreateDirectory(dir);
                                entry.ExtractToFile(target, overwrite: true);
                            }
                        }
                    }

                    File.Delete(tempZip);
                });

                DownloadBar.IsIndeterminate = false;
                DownloadBar.Value = 100;

                Properties.Settings.Default.GamePath = installDir;
                Properties.Settings.Default.Save();

                ResetStandardDownloadButtonState();
                CheckForUpdates();
            }
            catch (Exception ex)
            {
                // Same philosophy as the HD download: ChunkedDownloader already
                // retried transient failures with backoff, so landing here means
                // it needs a person - leave it in a Resume state instead of a
                // full reset, since the partial file is still on disk either way.
                DownloadBar.IsIndeterminate = false;
                MessageBox.Show($"Error: {ex.Message}\n\nYour progress was saved - click Resume to continue.", "Download Failed", MessageBoxButton.OK, MessageBoxImage.Error);

                stdDownloadPaused = true;
                CancelUpdateButton.Content = "Resume";
                CancelUpdateButton.IsEnabled = true;
                StatusHolder.Text = "PAUSED";
                TipHolder.Text = "Download paused. Click Resume to continue.";
            }
        }

        private void ResetStandardDownloadButtonState()
        {
            stdDownloadActive = false;
            stdDownloadPaused = false;

            PlayButton.Visibility = Visibility.Visible;
            PlayButton.IsEnabled = true;
            CancelUpdateButton.Visibility = Visibility.Collapsed;
            CancelUpdateButton.IsEnabled = true;
            CancelUpdateButton.Content = "STOP";
            ButtonSettings.IsEnabled = true;
            ButtonCheckUpdates.IsEnabled = true;
            ButtonHDClient.IsEnabled = true;
            CheckBoxHD.IsEnabled = true;
            spDownloadStatus.Visibility = Visibility.Hidden;
        }

        private const string HDClientZipUrl = "https://pub-8ee94134dece4549a4d9961c8bcdaa0f.r2.dev/Frostworn%20-%20World%20of%20Warcraft%203.3.5a%20HD%20Client.zip";
        private const long HDClientKnownSize = 41916314794L;

        private bool hdDownloadActive = false;
        private bool hdDownloadPaused = false;
        private string hdInstallDir;
        private string hdTempZip;

        private void ButtonHDClient_Click(object sender, RoutedEventArgs e)
        {
            if (hdDownloadActive)
            {
                // Acts like the Start button turning into Pause: toggle pause/resume
                if (hdDownloadPaused)
                {
                    hdDownloadPaused = false;
                    ButtonHDClient.Content = "Pause";
                    StatusHolder.Text = "DOWNLOADING HD CLIENT...";
                    TipHolder.Text = "Downloading Frostworn HD Client...";
                    RunHDClientDownload();
                }
                else
                {
                    hdDownloadPaused = true;
                    ButtonHDClient.Content = "Resume";
                    StatusHolder.Text = "PAUSED";
                    TipHolder.Text = "Download paused. Click Resume to continue.";
                }
                return;
            }

            string existingHDPath = Properties.Settings.Default.HDGamePath;
            if (!string.IsNullOrWhiteSpace(existingHDPath) && File.Exists(Path.Combine(existingHDPath, "Wow.exe")))
            {
                if (string.Equals(Properties.Settings.Default.GamePath, existingHDPath, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        $"You already have the HD Client installed and active.\n\nLocation: {existingHDPath}",
                        "HD Client",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                MessageBoxResult switchToHD = MessageBox.Show(
                    $"You already have the HD Client installed.\n\nLocation: {existingHDPath}\n\nSwitch to playing the HD version now?",
                    "HD Client Already Installed",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (switchToHD == MessageBoxResult.Yes)
                {
                    Properties.Settings.Default.GamePath = existingHDPath;
                    Properties.Settings.Default.Save();
                    CheckForUpdates();
                }
                return;
            }

            StartHDClientDownload();
        }

        /// <summary>
        /// Mirrors the standard client's Game_Finder_Dialog flow: before
        /// assuming the HD Client isn't installed and starting a fresh ~40 GB
        /// download, first offers to Browse to an existing installation (moved
        /// drive, reinstalled Windows but kept the files, etc.) - same as how
        /// picking an existing game folder for the standard client just uses it
        /// instead of blindly redownloading. Only falls through to the download
        /// flow if the player says they don't have it, or Browse didn't find
        /// Wow.exe in the folder they picked.
        /// </summary>
        public void StartHDClientDownload()
        {
            MessageBoxResult haveIt = MessageBox.Show(
                "The Frostworn HD Client isn't set up in this launcher yet.\n\nYES - I already have it downloaded somewhere, let me point you to the folder.\nNO - Download it now (~40 GB).",
                "HD Client Setup",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (haveIt == MessageBoxResult.Cancel)
                return;

            if (haveIt == MessageBoxResult.Yes)
            {
                string existingFolder;
                using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                {
                    fbd.Description = "Select your existing Frostworn HD Client folder";
                    if (fbd.ShowDialog().ToString() != "OK" || string.IsNullOrWhiteSpace(fbd.SelectedPath))
                        return;
                    existingFolder = fbd.SelectedPath;
                }

                if (File.Exists(Path.Combine(existingFolder, "Wow.exe")))
                {
                    Properties.Settings.Default.HDGamePath = existingFolder;
                    Properties.Settings.Default.Save();

                    MessageBoxResult playHD = MessageBox.Show(
                        $"Found it!\n\nLocation: {existingFolder}\n\nSwitch to playing the HD version now?",
                        "HD Client Located",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (playHD == MessageBoxResult.Yes)
                    {
                        Properties.Settings.Default.GamePath = existingFolder;
                        Properties.Settings.Default.Save();
                        CheckForUpdates();
                    }
                    return;
                }

                MessageBoxResult downloadInstead = MessageBox.Show(
                    "Wow.exe was not found in that folder.\n\nDownload the HD Client instead? (~40 GB)",
                    "HD Client Not Found",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (downloadInstead != MessageBoxResult.Yes)
                    return;
            }

            string chosenFolder;
            using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
            {
                fbd.Description = "Choose a folder to install the Frostworn HD Client";
                System.Windows.Forms.DialogResult result = fbd.ShowDialog();

                if (result.ToString() != "OK" || string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    return;

                chosenFolder = fbd.SelectedPath;
            }

            hdInstallDir = Path.Combine(chosenFolder, "Frostworn WoW 3.3.5a HD");
            Directory.CreateDirectory(hdInstallDir);
            hdTempZip = Path.Combine(hdInstallDir, "WoW_HD_install.zip");

            hdDownloadActive = true;
            hdDownloadPaused = false;

            ButtonHDClient.Content = "Pause";
            PlayButton.IsEnabled = false;
            UpdateButton.Visibility = Visibility.Collapsed;
            CancelUpdateButton.Visibility = Visibility.Collapsed;
            ButtonSettings.IsEnabled = false;
            ButtonCheckUpdates.IsEnabled = false;
            CheckBoxHD.IsEnabled = false;
            spDownloadStatus.Visibility = Visibility.Visible;
            TipHolder.Visibility = Visibility.Visible;
            TipHolder.Text = "Downloading Frostworn HD Client...";
            StatusHolder.Text = "DOWNLOADING HD CLIENT...";
            DownloadBar.Value = 0;

            RunHDClientDownload();
        }

        /// <summary>
        /// Downloads (or resumes downloading) the HD client zip via the shared
        /// ChunkedDownloader: HTTP Range resume, automatic retry-with-backoff
        /// (re-checking the resume point before each retry) on transient
        /// failures, and a Pause button that stops it cleanly between chunks
        /// without losing progress. When the download finishes, extracts the zip.
        /// </summary>
        private async void RunHDClientDownload()
        {
            try
            {
                var downloader = new ChunkedDownloader
                {
                    IsPaused = () => hdDownloadPaused,
                    OnStatus = status => TipHolder.Text = status,
                    OnProgress = (totalRead, totalBytes, bytesPerSecond) =>
                    {
                        DownloadBar.Value = (double)totalRead / totalBytes * 100;
                        DownloadedHolder.Text = Extensions.SizeSuffix(totalRead, 2);
                        TotalSizeHolder.Text = Extensions.SizeSuffix(totalBytes, 2);
                        SpeedHolder.Text = $"({bytesPerSecond / 1024d / 1024d:0.00} MB/S)";
                    }
                };

                DownloadOutcome outcome = await downloader.DownloadAsync(HDClientZipUrl, hdTempZip, HDClientKnownSize);

                if (outcome == DownloadOutcome.Paused)
                {
                    // Leave the partial file in place; next click resumes via Range header
                    return;
                }

                // Fully downloaded - extract
                StatusHolder.Text = "EXTRACTING...";
                TipHolder.Text = "Please wait, extracting HD client files...";
                DownloadBar.IsIndeterminate = true;
                ButtonHDClient.IsEnabled = false;

                string installDir = hdInstallDir;
                string tempZip = hdTempZip;

                await Task.Run(() =>
                {
                    using (var zip = ZipFile.OpenRead(tempZip))
                    {
                        string topDir = "";
                        if (zip.Entries.Count > 0)
                        {
                            string firstEntry = zip.Entries[0].FullName;
                            int slashIdx = firstEntry.IndexOf('/');
                            if (slashIdx > 0)
                            {
                                string candidate = firstEntry.Substring(0, slashIdx + 1);
                                bool allMatch = true;
                                foreach (var entry in zip.Entries)
                                {
                                    if (!entry.FullName.StartsWith(candidate))
                                    {
                                        allMatch = false;
                                        break;
                                    }
                                }
                                if (allMatch) topDir = candidate;
                            }
                        }

                        foreach (var entry in zip.Entries)
                        {
                            string rel = entry.FullName.Substring(topDir.Length);
                            if (string.IsNullOrEmpty(rel)) continue;

                            string target = Path.Combine(installDir, rel.Replace('/', Path.DirectorySeparatorChar));

                            if (entry.FullName.EndsWith("/"))
                            {
                                Directory.CreateDirectory(target);
                            }
                            else
                            {
                                string dir = Path.GetDirectoryName(target);
                                if (!string.IsNullOrEmpty(dir))
                                    Directory.CreateDirectory(dir);
                                entry.ExtractToFile(target, overwrite: true);
                            }
                        }
                    }

                    File.Delete(tempZip);

                    // Force the correct realmlist regardless of what shipped inside the
                    // HD client archive - a stale/test realmlist.wtf (e.g. 127.0.0.1)
                    // baked into the zip would otherwise silently break every install.
                    string realmlistLine = "set realmlist logon.frostworn.com";
                    string dataDir = Path.Combine(installDir, "data");
                    if (!Directory.Exists(dataDir))
                        dataDir = Path.Combine(installDir, "Data");

                    foreach (var localeDir in Directory.Exists(dataDir) ? Directory.GetDirectories(dataDir) : new string[0])
                    {
                        string realmlistPath = Path.Combine(localeDir, "realmlist.wtf");
                        if (File.Exists(realmlistPath))
                            File.WriteAllText(realmlistPath, realmlistLine + Environment.NewLine);
                    }
                });

                DownloadBar.IsIndeterminate = false;
                DownloadBar.Value = 100;

                Properties.Settings.Default.HDGamePath = installDir;
                Properties.Settings.Default.Save();

                MessageBoxResult playHD = MessageBox.Show(
                    $"HD Client installed successfully!\n\nLocation: {installDir}\n\nDo you want to switch to playing the HD version now?",
                    "HD Client Ready",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (playHD == MessageBoxResult.Yes)
                {
                    Properties.Settings.Default.GamePath = installDir;
                    Properties.Settings.Default.Save();
                }

                ResetHDClientButtonState();
                CheckForUpdates();
            }
            catch (Exception ex)
            {
                // The ChunkedDownloader already retried transient network
                // failures several times with backoff before giving up, so
                // landing here means it needs a person to look at it (or it's
                // an extraction-time error like a full disk). Leave the button
                // in a Resume state rather than a full reset - the partial file
                // is still on disk either way, so a click just picks up where
                // it left off instead of forcing the player back through the
                // whole "where do you want to install it" flow again.
                DownloadBar.IsIndeterminate = false;
                MessageBox.Show($"Error: {ex.Message}\n\nYour progress was saved - click Resume to continue.", "HD Client Download Failed", MessageBoxButton.OK, MessageBoxImage.Error);

                hdDownloadPaused = true;
                ButtonHDClient.Content = "Resume";
                ButtonHDClient.IsEnabled = true;
                StatusHolder.Text = "PAUSED";
                TipHolder.Text = "Download paused. Click Resume to continue.";
            }
        }

        private void ResetHDClientButtonState()
        {
            hdDownloadActive = false;
            hdDownloadPaused = false;

            ButtonHDClient.Content = "Download HD Client";
            ButtonHDClient.IsEnabled = true;
            PlayButton.Visibility = Visibility.Visible;
            PlayButton.IsEnabled = true;
            ButtonSettings.IsEnabled = true;
            ButtonCheckUpdates.IsEnabled = true;
            CheckBoxHD.IsEnabled = true;
            spDownloadStatus.Visibility = Visibility.Hidden;
        }
    }
}
