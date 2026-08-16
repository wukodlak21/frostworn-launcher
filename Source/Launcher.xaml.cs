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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ButtonSettings.IsEnabled = false;
            ButtonCheckUpdates.IsEnabled = false;
            CheckBoxHD.IsEnabled = false;
            spDownloadStatus.Visibility = Visibility.Hidden;
            TipHolder.Visibility = Visibility.Hidden;

            Slider.Start();

            serverStatusTimer.Tick += ServerStatusTimer_Tick;
            serverStatusTimer.Start();
            _ = RefreshServerStatus();

            if (string.IsNullOrEmpty(Properties.Settings.Default.GamePath) || string.IsNullOrWhiteSpace(Properties.Settings.Default.GamePath))
            {
                GameFinderDialog.Show();
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

                using (var wc = new WebClient())
                {
                    wc.DownloadProgressChanged += (s, e) => DownloadBar.Value = e.ProgressPercentage;
                    await wc.DownloadFileTaskAsync(new Uri(response.Url), updatePath);
                }

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
            Process.Start("https://discord.gg/VGPh2WzdE");
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

        public async void StartGameDownload()
        {
            string gamePath = Properties.Settings.Default.GamePath;
            string installDir = Path.Combine(gamePath, "Frostworn WoW 3.3.5a");
            Directory.CreateDirectory(installDir);
            string zipUrl = "https://frostworn.com/download/World%20of%20Warcraft%203.3.5a.zip";
            string tempZip = Path.Combine(gamePath, "WoW_install.zip");

            PlayButton.IsEnabled = false;
            PlayButton.Visibility = Visibility.Collapsed;
            UpdateButton.Visibility = Visibility.Collapsed;
            CancelUpdateButton.Visibility = Visibility.Collapsed;
            ButtonSettings.IsEnabled = false;
            ButtonCheckUpdates.IsEnabled = false;
            CheckBoxHD.IsEnabled = false;
            spDownloadStatus.Visibility = Visibility.Visible;
            TipHolder.Visibility = Visibility.Visible;
            TipHolder.Text = "Downloading World of Warcraft 3.3.5a...";
            StatusHolder.Text = "DOWNLOADING GAME...";
            DownloadBar.Value = 0;

            try
            {
                using (var wc = new WebClient())
                {
                    wc.DownloadProgressChanged += (s, e) =>
                    {
                        if (e.ProgressPercentage >= 0)
                            DownloadBar.Value = e.ProgressPercentage;
                        DownloadedHolder.Text = Extensions.SizeSuffix(e.BytesReceived, 2);
                        long total = e.TotalBytesToReceive > 0 ? e.TotalBytesToReceive : 17179869184L;
                        TotalSizeHolder.Text = Extensions.SizeSuffix(total, 2);
                        SpeedHolder.Text = "";
                    };

                    await wc.DownloadFileTaskAsync(new Uri(zipUrl), tempZip);
                }

                StatusHolder.Text = "EXTRACTING...";
                TipHolder.Text = "Please wait, extracting game files...";
                DownloadBar.IsIndeterminate = true;

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
            }
            catch (Exception ex)
            {
                DownloadBar.IsIndeterminate = false;
                if (File.Exists(tempZip))
                    File.Delete(tempZip);
                MessageBox.Show($"Error: {ex.Message}", "Download Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            PlayButton.Visibility = Visibility.Visible;
            PlayButton.IsEnabled = true;
            ButtonSettings.IsEnabled = true;
            ButtonCheckUpdates.IsEnabled = true;
            spDownloadStatus.Visibility = Visibility.Hidden;

            CheckForUpdates();
        }
    }
}
