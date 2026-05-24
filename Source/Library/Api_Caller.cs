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
            string json = string.Empty;

            try
            {
                json = await Newton_Workloader.GetHomeSliderResponse();

                return Newton_Workloader.HomeSliderResponse.FromJson(json);
            }
            catch (Exception ex)
            {
                string message = $"[File '{Extensions.GetCurrentCallerFileName()}' - Method 'HomeSliderResponse']\r\nException error: {ex.Message}\r\nApi message: {json}";

                MessageBox.Show(message);
            }

            return null;
        }

        public static async Task<List<Newton_Workloader.GameFilesListResponse>> GameFilesListResponse()
        {
            string json = string.Empty;

            try
            {
                json = await Newton_Workloader.GetGameFilesListResponse();

                return Newton_Workloader.GameFilesListResponse.FromJson(json);
            }
            catch (Exception ex)
            {
                string message = $"[File '{Extensions.GetCurrentCallerFileName()}' - Method 'GameFilesListResponse']\r\nException error: {ex.Message}\r\nApi message: {json}";

                MessageBox.Show(message);
            }

            return null;
        }
    }
}
