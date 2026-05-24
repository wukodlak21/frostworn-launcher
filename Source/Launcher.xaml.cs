using Oracle_Lite.Controllers;
using Oracle_Lite.Library;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace Oracle_Lite
{
    /// <summary>
    /// Interaction logic for Launcher.xaml
    /// </summary>
    public partial class Launcher : Window
    {
        private Game_Updater gameUpdater = new Game_Updater();
        public bool isUpdating = false;

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

            if (string.IsNullOrEmpty(Properties.Settings.Default.GamePath) || string.IsNullOrWhiteSpace(Properties.Settings.Default.GamePath))
            {
                GameFinderDialog.Show();
            }
            else
            {
                CheckForUpdates();
            }
        }

        // Property to get the application version.
        public string AppVersion
        {
            get { return GetAppVersion(); }
        }

        // Helper method to retrieve the application version.
        private string GetAppVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        public async void CheckForUpdates()
        {
            PlayButton.IsEnabled = false;
            ButtonSettings.IsEnabled = false;
            ButtonCheckUpdates.IsEnabled = false;
            CheckBoxHD.IsEnabled = false;

            if (await gameUpdater.UpdateList())
            {
                PlayButton.Visibility = Visibility.Collapsed; // hide
                UpdateButton.Visibility = Visibility.Visible; // show
                CancelUpdateButton.Visibility = Visibility.Collapsed; // hide
                TipHolder.Visibility = Visibility.Visible; // show

                StatusHolder.Text = "UPDATES AVAILABLE!";
                TipHolder.Text = "Press Update button to start downloading!";
            }
            else
            {
                TipHolder.Visibility = Visibility.Visible; // show

                StatusHolder.Text = "UP TO DATE";
                TipHolder.Text = "Press the Start button to run the game.";
                DownloadBar.Value = 100;
            }

            PlayButton.IsEnabled = true;
            ButtonSettings.IsEnabled = true;
            ButtonCheckUpdates.IsEnabled = true;
            CheckBoxHD.IsEnabled = true;
        }

        // Minimizes the launcher
        private void ButtonMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        // Displays the application exist dialog
        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            ExitDialog.Show();
        }

        private void ButtonHome_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://localhost/home");
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
            Process.Start("http://localhost/forums");
        }

        private void ButtonSupport_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://localhost/support");
        }

        private void ButtonDonate_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://localhost/donate");
        }

        private void ButtonVote_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://localhost/vote");
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
                        var dir = new DirectoryInfo($"{gamepath}\\Cache");
                        dir.Delete(true); // true => recursive delete
                    }

                    // set realmlist
                    string configWTFPath = $@"{gamepath}\WTF\Config.wtf";
                    SetRealmlistPerLocale();

                    if (File.Exists(configWTFPath))
                    {
                        var oldLines = File.ReadAllLines(configWTFPath);

                        // reads all lines except the lines that contains SET portal
                        var newLines = oldLines.Where(line => !line.ToLower().Contains("set realmlist"));

                        File.WriteAllLines(configWTFPath, newLines);

                        using (var outputFile = new StreamWriter(configWTFPath, true))
                            outputFile.WriteLine($"SET realmList \"{Properties.Settings.Default.RealmList}\"");
                    }

                    await Task.Delay(2000);

                    WindowState = WindowState.Minimized;

                    Process.Start(WowExePath);

                    PlayButton.Content = "PLAY";
                    PlayButton.IsEnabled = true;
                    //await RunGameAsync(WowExePath);
                }
                catch (Exception ex)
                {
                    string message = $"[File '{Extensions.GetCurrentCallerFileName()}' - Method 'PlayButton_Click']\r\nException error: {ex.Message}";
                    MessageBox.Show(message);
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

        private static void SetRealmlistPerLocale()
        {
            string gamepath = Properties.Settings.Default.GamePath;

            try
            {
                string[] locales = new string[] { "enUS", "esMX", "ptBR", "deDE", "enGB", "esES", "frFR", "itIT", "ruRU", "koKR", "zhTW", "zhCN" };

                foreach (var d in Directory.GetDirectories($@"{gamepath}\data"))
                {
                    var dir = new DirectoryInfo(d);
                    var dirName = dir.Name;

                    if (locales.Contains(dirName))
                    {
                        string configWTFPath = $@"{gamepath}\data\{dirName}\Realmlist.wtf";

                        if (File.Exists(configWTFPath))
                        {
                            var oldLines = File.ReadAllLines(configWTFPath);

                            // reads all lines except the lines that contains SET portal
                            var newLines = oldLines.Where(line => !line.ToLower().Contains("set realmlist"));

                            File.WriteAllLines(configWTFPath, newLines);

                            using (var outputFile = new StreamWriter(configWTFPath, true))
                                outputFile.WriteLine($"SET realmList \"{Properties.Settings.Default.RealmList}\"");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string message = $"[File '{Extensions.GetCurrentCallerFileName()}' - Method 'SetRealmlistPerLocale']\r\nException error: {ex.Message}";
                MessageBox.Show(message);
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (gameUpdater.Start())
            {
                isUpdating = true;

                DownloadBar.Value = 0;
                PlayButton.Visibility = Visibility.Collapsed; // hide
                UpdateButton.Visibility = Visibility.Collapsed; // hide
                CancelUpdateButton.Visibility = Visibility.Visible; // show
                TipHolder.Visibility = Visibility.Hidden; // hide
                spDownloadStatus.Visibility = Visibility.Visible; // show
                ButtonSettings.IsEnabled = false;
                ButtonCheckUpdates.IsEnabled = false;
                CheckBoxHD.IsEnabled = false;

                gameUpdater.ProgressChangedEvent += OnGameUpdateProgressChanged;
                gameUpdater.CompletedEvent += OnGameUpdateCompleted;
            }
            else
            {
                // do what if updater didnt start, means nothing to download or?
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
            PlayButton.Visibility = Visibility.Visible; // show
            UpdateButton.Visibility = Visibility.Collapsed; // hide
            CancelUpdateButton.Visibility = Visibility.Collapsed; // hide
            spDownloadStatus.Visibility = Visibility.Hidden; // hide
            TipHolder.Visibility = Visibility.Visible; // show
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
            PlayButton.Visibility = Visibility.Collapsed; // hide
            UpdateButton.Visibility = Visibility.Visible; // show
            CancelUpdateButton.Visibility = Visibility.Collapsed; // hide
            TipHolder.Visibility = Visibility.Visible; // show
            spDownloadStatus.Visibility = Visibility.Hidden; // hide
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

                            string target = Path.Combine(gamePath, rel.Replace('/', Path.DirectorySeparatorChar));

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
            CheckBoxHD.IsEnabled = true;
            spDownloadStatus.Visibility = Visibility.Hidden;

            CheckForUpdates();
        }
    }
}