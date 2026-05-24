using System;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using System.Windows;

namespace Oracle_Lite.Library
{
    internal class Animations
    {
        public static void FadeIn(FrameworkElement element, int millisecondsDuration)
        {
            element.Visibility = Visibility.Visible;

            // Animate opacity
            Storyboard storyboard = new Storyboard();
            DoubleAnimation FadeInAnimation = new DoubleAnimation()
            {
                Duration = TimeSpan.FromMilliseconds(millisecondsDuration),
                From = 0,
                To = 1
            };
            Storyboard.SetTarget(FadeInAnimation, element);
            Storyboard.SetTargetProperty(FadeInAnimation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(FadeInAnimation);
            storyboard.Begin();
        }

        public static async void FadeOut(FrameworkElement element, int millisecondsDuration)
        {
            // Animate opacity
            Storyboard storyboard = new Storyboard();
            DoubleAnimation FadeOutAnimation = new DoubleAnimation()
            {
                Duration = TimeSpan.FromMilliseconds(millisecondsDuration),
                From = 1,
                To = 0
            };
            Storyboard.SetTarget(FadeOutAnimation, element);
            Storyboard.SetTargetProperty(FadeOutAnimation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(FadeOutAnimation);
            storyboard.Begin();

            await Task.Delay(millisecondsDuration);
            element.Visibility = Visibility.Hidden;
        }
    }
}
