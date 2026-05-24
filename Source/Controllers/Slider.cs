using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Oracle_Lite.Library;
using Oracle_Lite.Cache;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Oracle_Lite.Buttons;

namespace Oracle_Lite.Controllers
{
    internal class Slider
    {
        private static int slider_index;

        private static DispatcherTimer slider_timer = new DispatcherTimer()
        { 
            Interval = TimeSpan.FromSeconds(Properties.Settings.Default.SliderDelaySeconds),
        };

        public static void Start()
        {
            SpawnDots();

            slider_index = 0;

            SetSelectedVisualDot(slider_index);

            DisplayBackgroundIndex(slider_index);

            DisplayDetails(slider_index);

            slider_timer.Tick += Slider_timer_Tick;

            slider_timer.Start();
        }

        private static void Slider_timer_Tick(object sender, EventArgs e)
        {
            slider_index = (slider_index + 1) % SliderCache.Backgrounds.Count;

            DisplayBackgroundIndex(slider_index);

            DisplayDetails(slider_index);

            SetSelectedVisualDot(slider_index);
        }

        private static async void DisplayBackgroundIndex(int index)
        {
            if (Application.Current.MainWindow is Launcher mainWindow)
            {
                try
                {
                    Animations.FadeOut(mainWindow.ArtworkBackgroundGrid, 100);
                    Animations.FadeOut(mainWindow.spSlideDetails, 100);

                    await Task.Delay(150);

                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new System.IO.MemoryStream(SliderCache.Backgrounds[index]);
                    bitmap.EndInit();
                    mainWindow.ArtworkBackgroundHolder.ImageSource = bitmap;

                    Animations.FadeIn(mainWindow.ArtworkBackgroundGrid, 500);
                    Animations.FadeIn(mainWindow.spSlideDetails, 1500);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex.Message}\r\nFailed to load SliderCache Background bytes reference index: {index}");
                }
            }
        }

        private static void DisplayDetails(int index)
        {
            if (Application.Current.MainWindow is Launcher mainWindow)
            {
                try
                {
                    Newton_Workloader.HomeSliderResponse slide = SliderCache.homeSliderResponses[index];

                    mainWindow.SlideTagHolder.Text = slide.Tag;
                    mainWindow.SlideTitleHolder.Text = slide.Title;
                    mainWindow.ButtonLearnMore.Tag = slide.Url;

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex.Message}\r\nFailed to load slide cache details index: {index}");
                }
            }
        }

        private static void SpawnDots()
        {
            if (Application.Current.MainWindow is Launcher mainWindow)
            {
                mainWindow.spSliderDots.Children.Clear();

                int index = 0;

                try
                {
                    foreach (Newton_Workloader.HomeSliderResponse slide in SliderCache.homeSliderResponses)
                    {
                        SliderDot sliderDot = new SliderDot() { Slider_Index = index };

                        sliderDot.Click += (s, e) =>
                        {
                            slider_timer.Stop();

                            slider_index = sliderDot.Slider_Index;

                            DisplayBackgroundIndex(sliderDot.Slider_Index);

                            DisplayDetails(sliderDot.Slider_Index);

                            SetSelectedVisualDot(sliderDot.Slider_Index);

                            slider_timer.Start();
                        };

                        mainWindow.spSliderDots.Children.Add(sliderDot);

                        index++;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex.Message}\r\nFailed at SpawnDots, index: {index }");
                }
            }
        }

        private static void SetSelectedVisualDot(int index)
        {
            if (Application.Current.MainWindow is Launcher mainWindow)
            {
                foreach (SliderDot dot in mainWindow.spSliderDots.Children.OfType<SliderDot>())
                {
                    if (dot.Slider_Index == index)
                    {
                        dot.IsEnabled = false;
                    }
                    else
                    {
                        dot.IsEnabled = true;
                    }
                }
            }
        }

        public static void GoPrevious()
        {
            slider_timer.Stop();

            slider_index = (slider_index - 1) % SliderCache.Backgrounds.Count;

            if (slider_index < 0)
            {
                slider_index = SliderCache.Backgrounds.Count - 1;
            }

            DisplayBackgroundIndex(slider_index);

            DisplayDetails(slider_index);

            SetSelectedVisualDot(slider_index);

            slider_timer.Start();
        }

        public static void GoNext()
        {
            slider_timer.Stop();

            slider_index = (slider_index + 1) % SliderCache.Backgrounds.Count;

            if (slider_index > SliderCache.Backgrounds.Count - 1)
            {
                slider_index = 0;
            }

            DisplayBackgroundIndex(slider_index);

            DisplayDetails(slider_index);

            SetSelectedVisualDot(slider_index);

            slider_timer.Start();
        }
    }
}
