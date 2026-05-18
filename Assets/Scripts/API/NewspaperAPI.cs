using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.API
{
    public static class NewspaperAPI
    {
        public static async Task<Texture2D> GetNewspaperImage(string date = null)
        {
            string path = string.IsNullOrEmpty(date)
                ? "/newspaper/image"
                : $"/newspaper/image?date={date}";

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
