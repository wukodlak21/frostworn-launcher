using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Oracle_Lite.Popups
{
    /// <summary>
    /// Interaction logic for Submenu_Popup.xaml
    /// </summary>
    public partial class Submenu_Popup : UserControl
    {
        public Submenu_Popup()
        {
            InitializeComponent();
        }

        private void ButtonDiscord_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/63KSeFK3B");

            Visibility = Visibility.Collapsed;
        }

        private void ButtonInstagram_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://instagram.com/frostwornicecrownpremium");

            Visibility = Visibility.Collapsed;
        }

        private void ButtonTikTok_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://www.tiktok.com/@frostwornicecrownpremium");

            Visibility = Visibility.Collapsed;
        }
    }
}
