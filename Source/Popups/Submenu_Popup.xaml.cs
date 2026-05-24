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
            Process.Start("https://discord.gg/myserver");

            Visibility = Visibility.Collapsed;
        }

        private void ButtonFacebook_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://facebook.com/myserver");

            Visibility = Visibility.Collapsed;
        }

        private void ButtonTwitter_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://twitter.com/myserver");

            Visibility = Visibility.Collapsed;
        }

        private void ButtonInstagram_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://instagram.com/myserver");

            Visibility = Visibility.Collapsed;
        }
    }
}
