using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Oracle_Lite.Dialogs
{
    /// <summary>
    /// Drop-in replacement for System.Windows.MessageBox.Show(), styled to
    /// match the rest of the app (same rounded white card + Philosopher font
    /// as Game_Finder_Dialog/Application_Exit_Dialog) instead of showing the
    /// native Windows dialog chrome. Blocks synchronously via a nested
    /// Dispatcher frame - the exact same call-and-get-a-result usage as the
    /// native MessageBox.Show(), so every existing call site (including
    /// "if (result != Yes) return;" patterns in non-async methods) keeps
    /// working unchanged, just calling Custom_MessageBox.Show(...) instead.
    /// </summary>
    public partial class Custom_MessageBox : UserControl
    {
        private DispatcherFrame _frame;
        private MessageBoxResult _result;

        public Custom_MessageBox()
        {
            InitializeComponent();
        }

        public static MessageBoxResult Show(string message)
        {
            return Show(message, null, MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string message, string title)
        {
            return Show(message, title, MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
        {
            if (!(Application.Current?.MainWindow is Launcher launcher))
            {
                // No app window to host the custom dialog in (e.g. a crash
                // before the main window ever loaded) - fall back to the
                // native dialog rather than failing to show anything at all.
                return MessageBox.Show(message, title ?? string.Empty, buttons, icon);
            }

            return launcher.CustomMsgBox.ShowInternal(message, title, buttons);
        }

        internal MessageBoxResult ShowInternal(string message, string title, MessageBoxButton buttons)
        {
            MessageHolder.Text = message;

            if (string.IsNullOrEmpty(title))
            {
                TitleHolder.Visibility = Visibility.Collapsed;
            }
            else
            {
                TitleHolder.Text = title;
                TitleHolder.Visibility = Visibility.Visible;
            }

            ButtonYes.Visibility = Visibility.Collapsed;
            ButtonNo.Visibility = Visibility.Collapsed;
            ButtonCancel.Visibility = Visibility.Collapsed;
            ButtonOK.Visibility = Visibility.Collapsed;

            switch (buttons)
            {
                case MessageBoxButton.YesNo:
                    ButtonYes.Visibility = Visibility.Visible;
                    ButtonNo.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNoCancel:
                    ButtonYes.Visibility = Visibility.Visible;
                    ButtonNo.Visibility = Visibility.Visible;
                    ButtonCancel.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.OKCancel:
                    ButtonOK.Visibility = Visibility.Visible;
                    ButtonCancel.Visibility = Visibility.Visible;
                    break;
                default:
                    ButtonOK.Visibility = Visibility.Visible;
                    break;
            }

            Visibility = Visibility.Visible;

            // Blocks the calling thread here (while still pumping the UI
            // message loop) until one of the button handlers below calls
            // Close(), exactly like the native MessageBox.Show()'s own
            // modal loop - this is the same technique WPF's ShowDialog()
            // uses internally.
            _frame = new DispatcherFrame();
            Dispatcher.PushFrame(_frame);

            Visibility = Visibility.Collapsed;
            return _result;
        }

        private void Close(MessageBoxResult result)
        {
            _result = result;
            if (_frame != null)
                _frame.Continue = false;
        }

        private void ButtonYes_Click(object sender, RoutedEventArgs e) => Close(MessageBoxResult.Yes);
        private void ButtonNo_Click(object sender, RoutedEventArgs e) => Close(MessageBoxResult.No);
        private void ButtonCancel_Click(object sender, RoutedEventArgs e) => Close(MessageBoxResult.Cancel);
        private void ButtonOK_Click(object sender, RoutedEventArgs e) => Close(MessageBoxResult.OK);
    }
}
