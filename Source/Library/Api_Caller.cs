using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace Oracle_Lite.Library
{
    internal class Api_Caller
    {
        public static async Task<List<Newton_Workloader.HomeSliderResponse>> HomeSliderResponse()
        {
            try
            {
                string json = await Newton_Workloader.GetHomeSliderResponse();

                return Newton_Workloader.HomeSliderResponse.FromJson(json);
            }
            catch
            {
                // Silent fail - the promo slider is decorative and SliderCache
                // already handles an empty/failed result gracefully. A raw
                // technical MessageBox here on a routine network hiccup would
                // scare players for no reason (this used to block the whole
                // launcher from opening at all).
            }

            return null;
        }

        public static async Task<List<Newton_Workloader.GameFilesListResponse>> GameFilesListResponse()
        {
            try
            {
                string json = await Newton_Workloader.GetGameFilesListResponse();

                return Newton_Workloader.GameFilesListResponse.FromJson(json);
            }
            catch
            {
                // Silent fail - Game_Updater.UpdateList() treats a null result
                // as "nothing to update" and CheckForUpdates() just reports
                // "UP TO DATE" until the next check succeeds, instead of
                // showing a raw technical popup for a routine network hiccup.
            }

            return null;
        }

        public static async Task<Newton_Workloader.LauncherVersionResponse> LauncherVersionResponse()
        {
            string json = string.Empty;

            try
            {
                json = await Newton_Workloader.GetLauncherVersionResponse();

                return Newton_Workloader.LauncherVersionResponse.FromJson(json);
            }
            catch
            {
                // Silent fail - self-update is not critical
            }

            return null;
        }

        public static async Task<Newton_Workloader.ServerStatusResponse> ServerStatusResponse()
        {
            string json = string.Empty;

            try
            {
                json = await Newton_Workloader.GetServerStatusResponse();

                return Newton_Workloader.ServerStatusResponse.FromJson(json);
            }
            catch
            {
                // Silent fail - status panel just shows placeholders
            }

            return null;
        }

        public static async Task<Newton_Workloader.MostWantedResponse> MostWantedResponse(string realm)
        {
            string json = string.Empty;

            try
            {
                json = await Newton_Workloader.GetMostWantedResponse(realm);

                return Newton_Workloader.MostWantedResponse.FromJson(json);
            }
            catch
            {
                // Silent fail - wanted panel just shows placeholders
            }

            return null;
        }
    }
}
