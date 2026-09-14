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

            [JsonProperty("meta")]
            public string Meta { get; set; }

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

            // Optional: older API responses (or a request that hit an uncached
            // path) may omit this - Game_Updater falls back to size-only
            // comparison when it's null/empty rather than treating that as a mismatch.
            [JsonProperty("Sha256")]
            public string Sha256 { get; set; }

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

        #region LAUNCHER VERSION RESPONSE
        public partial class LauncherVersionResponse
        {
            [JsonProperty("version")]
            public string Version { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }
        }

        public partial class LauncherVersionResponse
        {
            public static LauncherVersionResponse FromJson(string json) => JsonConvert.DeserializeObject<LauncherVersionResponse>(json, JsonTool.Converter.Settings);
        }

        public static async Task<string> GetLauncherVersionResponse()
        {
            return await JsonTool.GetStringFromPOST(Properties.Settings.Default.ApiTarget, new Dictionary<string, string>
            {
                { "execute", "3" },
            });
        }
        #endregion

        #region SERVER STATUS RESPONSE
        public partial class ServerStatusRealm
        {
            [JsonProperty("key")]
            public string Key { get; set; }

            [JsonProperty("label")]
            public string Label { get; set; }

            [JsonProperty("online")]
            public int Online { get; set; }

            [JsonProperty("bots")]
            public int Bots { get; set; }
        }

        public partial class ServerStatusResponse
        {
            [JsonProperty("available")]
            public bool Available { get; set; }

            [JsonProperty("realms")]
            public List<ServerStatusRealm> Realms { get; set; }

            [JsonProperty("total")]
            public int Total { get; set; }
        }

        public partial class ServerStatusResponse
        {
            public static ServerStatusResponse FromJson(string json) => JsonConvert.DeserializeObject<ServerStatusResponse>(json, JsonTool.Converter.Settings);
        }

        public static async Task<string> GetServerStatusResponse()
        {
            return await JsonTool.GetStringFromPOST(Properties.Settings.Default.ApiTarget, new Dictionary<string, string>
            {
                { "execute", "4" },
            });
        }
        #endregion

        #region MOST WANTED RESPONSE
        public partial class WantedPlayer
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("level")]
            public int Level { get; set; }

            [JsonProperty("bounty")]
            public int Bounty { get; set; }
        }

        public partial class MostWantedResponse
        {
            [JsonProperty("available")]
            public bool Available { get; set; }

            [JsonProperty("realm")]
            public string Realm { get; set; }

            [JsonProperty("players")]
            public List<WantedPlayer> Players { get; set; }
        }

        public partial class MostWantedResponse
        {
            public static MostWantedResponse FromJson(string json) => JsonConvert.DeserializeObject<MostWantedResponse>(json, JsonTool.Converter.Settings);
        }

        public static async Task<string> GetMostWantedResponse(string realm)
        {
            return await JsonTool.GetStringFromPOST(Properties.Settings.Default.ApiTarget, new Dictionary<string, string>
            {
                { "execute", "5" },
                { "realm", realm },
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
