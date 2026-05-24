using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Oracle_Lite.Library
{
    internal class Newton_Workloader
    {
        #region HOME SLIDER RESPONSE
        public partial class HomeSliderResponse
        {
            [JsonProperty("background_url")]
            public string BackgroundUrl { get; set; }

            [JsonProperty("tag")]
            public string Tag { get; set; }

            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }
        }

        public partial class HomeSliderResponse
        {
            public static List<HomeSliderResponse> FromJson(string json) => JsonConvert.DeserializeObject<List<HomeSliderResponse>>(json, JsonTool.Converter.Settings);
        }

        public static async Task<string> GetHomeSliderResponse()
        {
            return await JsonTool.GetStringFromPOST(Properties.Settings.Default.ApiTarget, new Dictionary<string, string>
            {
                { "execute", "1" },
            });
        }
        #endregion

        #region GAME FILES LIST RESPONSE
        public partial class GameFilesListResponse
        {
            [JsonProperty("Name")]
            public string Name { get; set; }

            [JsonProperty("Size")]
            public long Size { get; set; }

            [JsonProperty("Timestamp")]
            public int Timestamp { get; set; }

            [JsonProperty("IsHD")]
            public bool IsHD { get; set; }

            [JsonProperty("TargetPath")]
            public string TargetPath { get; set; }

            [JsonProperty("Url")]
            public string Url { get; set; }
        }

        public partial class GameFilesListResponse
        {
            public static List<GameFilesListResponse> FromJson(string json) => JsonConvert.DeserializeObject<List<GameFilesListResponse>>(json, JsonTool.Converter.Settings);
        }

        public static async Task<string> GetGameFilesListResponse()
        {
            return await JsonTool.GetStringFromPOST(Properties.Settings.Default.ApiTarget, new Dictionary<string, string>
            {
                { "execute", "2" },
            });
        }
        #endregion
    }

    internal class JsonTool
    {
        internal static class Converter
        {
            public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                DateParseHandling = DateParseHandling.None,
                Converters =
                {
                    new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
                },
            };
        }

        public static async Task<string> GetStringFromPOST(string URL, Dictionary<string, string> values)
        {
            try
            {
                var content = new FormUrlEncodedContent(values);
                using (var client = new HttpClient())
                {
                    var response = await client.PostAsync(URL, content);
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        public static async Task SendJsonPOST(string URL, Dictionary<string, string> values)
        {
            try
            {
                var content = new FormUrlEncodedContent(values);
                using (var client = new HttpClient())
                {
                    var response = await client.PostAsync(URL, content);
                }
            }
            catch
            {

            }
        }
    }
}
