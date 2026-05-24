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

        public static async Task<bool> Update(ProgressBar progressBar)
        {
            try
            {
                homeSliderResponses = await Api_Caller.HomeSliderResponse();

                if (homeSliderResponses != null)
                {
                    progressBar.Maximum = homeSliderResponses.Count;
                    progressBar.Value = 0;

                    foreach (Newton_Workloader.HomeSliderResponse slide in homeSliderResponses)
                    {
                        Backgrounds.Add(await DownloadImageBytes(slide.BackgroundUrl));
                        progressBar.Value++;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            return false;
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
