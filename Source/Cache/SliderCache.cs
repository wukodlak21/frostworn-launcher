using Oracle_Lite.Library;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Oracle_Lite.Cache
{
    internal class SliderCache
    {
        public static List<byte[]> Backgrounds = new List<byte[]> { };
        public static List<Newton_Workloader.HomeSliderResponse> homeSliderResponses;

        /// <summary>
        /// Best-effort fetch of the promo slider content and its background images.
        /// Never throws and never blocks the launcher from opening: if the API is
        /// unreachable, or an individual slide's image fails to download, that
        /// slide is simply skipped rather than aborting the whole slider (or, in
        /// the old behavior, leaving the player stuck on the loading screen).
        /// Slider.Start() is responsible for hiding the slider UI entirely when
        /// Backgrounds ends up empty.
        /// </summary>
        public static async Task<bool> Update(ProgressBar progressBar)
        {
            Backgrounds.Clear();

            List<Newton_Workloader.HomeSliderResponse> fetched = null;
            try
            {
                fetched = await Api_Caller.HomeSliderResponse();
            }
            catch
            {
                fetched = null;
            }

            if (fetched == null || fetched.Count == 0)
            {
                homeSliderResponses = new List<Newton_Workloader.HomeSliderResponse>();
                return true;
            }

            progressBar.Maximum = fetched.Count;
            progressBar.Value = 0;

            var loadedSlides = new List<Newton_Workloader.HomeSliderResponse>();

            foreach (Newton_Workloader.HomeSliderResponse slide in fetched)
            {
                try
                {
                    byte[] bytes = await DownloadImageBytes(slide.BackgroundUrl);
                    Backgrounds.Add(bytes);
                    loadedSlides.Add(slide);
                }
                catch
                {
                    // Skip this one slide - a single broken image must not take
                    // down the whole slider (or block the launcher from opening).
                }

                progressBar.Value++;
            }

            homeSliderResponses = loadedSlides;
            return true;
        }

        private static async Task<byte[]> DownloadImageBytes(string imageUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                return await client.GetByteArrayAsync(imageUrl);
            }
        }
    }
}
