using Oracle_Lite.Library;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Oracle_Lite.Dialogs
{
    public partial class Game_Finder_Dialog : UserControl
    {
        public Game_Finder_Dialog()
        {
            InitializeComponent();
        }

        public void Show()
        {
            Animations.FadeIn(this, 150);

            if (string.IsNullOrEmpty(Properties.Settings.Default.GamePath) || string.IsNullOrWhiteSpace(Properties.Settings.Default.GamePath))
            {
                pathHolder.Text = "None..";
            }
            else
            {
                pathHolder.Text = Properties.Settings.Default.GamePath;
            }
        }

        private async void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is Launcher mainWindow)
            {
                try
                {
                    using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        System.Windows.Forms.DialogResult result = fbd.ShowDialog();

                        if (result.ToString() == "OK" && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                        {
                            Properties.Settings.Default.GamePath = fbd.SelectedPath;
                            Properties.Settings.Default.Save();

                            if (!File.Exists(Path.Combine(fbd.SelectedPath, "Wow.exe")))
                            {
                                var dlResult = MessageBox.Show(
                                    "World of Warcraft not found in the selected folder.\n\nDownload game client? (~17 GB)",
                                    "Game Not Found",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question);

                                if (dlResult == MessageBoxResult.Yes)
                                {
                                    Animations.FadeOut(this, 150);
                                    await Task.Delay(200);
                                    Visibility = Visibility.Collapsed;
                                    mainWindow.StartGameDownload();
                                    return;
                                }
                            }

                            // Fix read only permissions on game folder
                            Process cmd = new Process();
                            cmd.StartInfo.FileName = "cmd.exe";
                            cmd.StartInfo.RedirectStandardInput = true;
                            cmd.StartInfo.RedirectStandardOutput = true;
                            cmd.StartInfo.CreateNoWindow = true;
                            cmd.StartInfo.UseShellExecute = false;
                            cmd.Start();
                            cmd.StandardInput.WriteLine("taskkill /im Wow.exe");
                            cmd.StandardInput.WriteLine("cd /d " + @fbd.SelectedPath);
                            cmd.StandardInput.WriteLine("attrib -r * /s");
                            cmd.StandardInput.Flush();
                            cmd.StandardInput.Close();
                            cmd.WaitForExit();

                            mainWindow.CheckForUpdates();

                            Animations.FadeOut(this, 150);
                            await Task.Delay(200);
                            Visibility = Visibility.Collapsed;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private async void ButtonNo_Click(object sender, RoutedEventArgs e)
        {
            Animations.FadeOut(this, 150);
            await Task.Delay(200);
            Visibility = Visibility.Collapsed;
        }
    }
}
