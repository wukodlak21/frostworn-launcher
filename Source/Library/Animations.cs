using System;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows;

namespace Oracle_Lite.Library
{
    internal class Animations
    {
        /// <summary>
        /// Fades an element in while sliding it in from a horizontal offset.
        /// </summary>
        public static void SlideIn(FrameworkElement element, int millisecondsDuration, double fromOffsetX = 40)
        {
            element.Visibility = Visibility.Visible;

            TranslateTransform transform = element.RenderTransform as TranslateTransform;
            if (transform == null)
            {
                transform = new TranslateTransform();
                element.RenderTransform = transform;
            }

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

            DoubleAnimation SlideAnimation = new DoubleAnimation()
            {
                Duration = TimeSpan.FromMilliseconds(millisecondsDuration),
                From = fromOffsetX,
                To = 0,
                EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(SlideAnimation, transform);
            Storyboard.SetTargetProperty(SlideAnimation, new PropertyPath(TranslateTransform.XProperty));
            storyboard.Children.Add(SlideAnimation);

            storyboard.Begin();
        }

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
