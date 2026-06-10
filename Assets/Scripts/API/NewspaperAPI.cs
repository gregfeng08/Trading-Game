using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.API
{
    public static class NewspaperAPI
    {
        public static async Task PreGenerate(string date, int entityId = -1)
        {
            try
            {
                var qs = $"date={date}";
                if (entityId > 0) qs += $"&entityId={entityId}";
                var uri = new Uri(APIClient.BaseUrl + $"/newspaper/image?{qs}");
                var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                await client.GetAsync(uri);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NewspaperAPI] Pre-generation failed for {date}: {ex.Message}");
            }
        }

        public static async Task<Texture2D> GetNewspaperImage(string date = null, int entityId = -1)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(date)) parts.Add($"date={date}");
            if (entityId > 0) parts.Add($"entityId={entityId}");
            string path = parts.Count > 0
                ? $"/newspaper/image?{string.Join("&", parts)}"
                : "/newspaper/image";

            var uri = new Uri(APIClient.BaseUrl + path);
            var client = new HttpClient();

            var response = await client.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Debug.LogError($"[NewspaperAPI] GET {uri} failed: {(int)response.StatusCode}\n{errorBody}");
                return null;
            }

            byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            if (!texture.LoadImage(imageBytes))
            {
                Debug.LogError("[NewspaperAPI] Failed to decode newspaper PNG");
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            return texture;
        }
    }
}
