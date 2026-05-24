using System;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Oracle_Lite.Library
{
    internal class Extensions
    {
        public static string GetCurrentCallerFileName([CallerFilePath] string fileName = "")
        {
            fileName = Path.GetFileName(fileName);
            return fileName;
        }
        public static async void SetImageSource(Image _image, string _uri, UriKind _uriKind)
        {
            try
            {
                if (_uri != null)
                {
                    if (_uriKind == UriKind.Absolute)
                    {
                        if (!Uri.IsWellFormedUriString(_uri, UriKind.Absolute))
                            return;

                        if (!await ImageExistsAtUrl(_uri))
                            return;
                    }

                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(_uri, _uriKind);
                    bitmapImage.EndInit();

                    _image.Source = bitmapImage;
                }
            }
            catch
            {

            }
        }

        public static async void SetImageBrushSource(ImageBrush imageBrush, string uri, UriKind uriKind)
        {
            try
            {
                if (uri != null)
                {
                    if (uriKind == UriKind.Absolute)
                    {
                        if (!Uri.IsWellFormedUriString(uri, UriKind.Absolute))
                            return;

                        if (!await ImageExistsAtUrl(uri))
                            return;
                    }
                    else if (uriKind == UriKind.Relative)
                    {
                        // Assuming the uri is a pack URI
                        uri = "pack://application:,,," + uri;
                    }

                    BitmapImage newImageSource = new BitmapImage(new Uri(uri));

                    imageBrush.ImageSource = newImageSource;
                }
            }
            catch
            {

            }
        }

        private static async Task<bool> ImageExistsAtUrl(string url)
        {
            HttpWebRequest httpReq = (HttpWebRequest)WebRequest.Create(url);
            try
            {
                HttpWebResponse httpRes = (HttpWebResponse)await httpReq.GetResponseAsync();
                if (httpRes.StatusCode == HttpStatusCode.NotFound)
                    return false;

                httpRes.Close();
            }
            catch
            {
                return false;
            }

            return true;
        }

        private static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        public static string SizeSuffix(long value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }
    }
}
