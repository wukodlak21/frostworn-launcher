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
            // The promo slider is decorative - a slow/unavailable API or a single
            // broken slide image must never prevent the player from reaching the
            // Play button, so SliderCache.Update() is best-effort and this always
            // proceeds to open the main window regardless of what it returns.
            try
            {
                await SliderCache.Update(LoadingBar);
            }
            catch
            {
                // Ignored - Slider.Start() handles an empty/partial cache gracefully.
            }

            Launcher launcher = new Launcher();
            Application.Current.MainWindow = launcher;
            launcher.Show();
            Close();
        }
    }
}
