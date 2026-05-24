using Oracle_Lite.Cache;
using System.Windows;

namespace Oracle_Lite
{
    /// <summary>
    /// Interaction logic for Loading_Window.xaml
    /// </summary>
    public partial class Loading_Window : Window
    {
        public Loading_Window()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (await SliderCache.Update(LoadingBar))
            {
                Launcher launcher = new Launcher();
                Application.Current.MainWindow = launcher;
                launcher.Show();
                Close();
            }
            else
            {
                MessageBox.Show("Could not load launcher data, please contact and administrator!");
            }
        }
    }
}
