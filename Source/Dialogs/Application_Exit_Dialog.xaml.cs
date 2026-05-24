using Oracle_Lite.Library;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Oracle_Lite.Dialogs
{
    /// <summary>
    /// Interaction logic for Application_Exit_Dialog.xaml
    /// </summary>
    public partial class Application_Exit_Dialog : UserControl
    {
        public Application_Exit_Dialog()
        {
            InitializeComponent();
        }

        public void Show()
        {
            Animations.FadeIn(this, 150);
        }

        private async void ButtonYes_Click(object sender, RoutedEventArgs e)
        {
            Animations.FadeOut(Application.Current.MainWindow, 300);
            await Task.Delay(350);
            Environment.Exit(0);
        }

        private async void ButtonNo_Click(object sender, RoutedEventArgs e)
        {
            Animations.FadeOut(this, 150);
            await Task.Delay(200);
            Visibility = Visibility.Collapsed;
        }
    }
}
