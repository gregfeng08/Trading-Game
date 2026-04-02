using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.API
{
    public static class APIClient
    {
        private static readonly HttpClient client = new HttpClient();

        // Backing field so we can normalize once.
        private static string _baseUrl = "http://127.0.0.1:5000";

        public static string BaseUrl
        {
            get => _baseUrl;
            set => _baseUrl = NormalizeBaseUrl(value);
        }

        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("ApiClient.BaseUrl is empty.");

            baseUrl = baseUrl.Trim();

            // Allow user to omit scheme
            if (!baseUrl.StartsWith("http://") && !baseUrl.StartsWith("https://"))
                baseUrl = "http://" + baseUrl;

            // Remove trailing slash so BuildUri can handle consistently
            baseUrl = baseUrl.TrimEnd('/');

            // Validate
            if (!Uri.TryCreate(baseUrl + "/", UriKind.Absolute, out var _))
                throw new ArgumentException($"ApiClient.BaseUrl is not a valid URL: '{baseUrl}'");

            return baseUrl;
        }

        private static Uri BuildUri(string path)
        {
            // Ensure base ends with slash for Uri combining
            var baseUri = new Uri(_baseUrl + "/");

            path = (path ?? "").Trim();
            if (path.StartsWith("/")) path = path.Substring(1);

            return new Uri(baseUri, path);
        }

        public static async Task<TResponse> GetAsync<TResponse>(string path)
        {
            Uri uri = BuildUri(path);
            string url = uri.ToString();

            HttpResponseMessage response;
            string responseText;

            try
            {
                response = await client.GetAsync(uri);
                responseText = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ApiClient] GET failed: {url}\n{ex}");
                throw;
            }

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"[ApiClient] GET {(int)response.StatusCode} {response.ReasonPhrase}: {url}\nBody:\n{responseText}");
                throw new HttpRequestException($"GET {url} failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            try
            {
                return JsonUtility.FromJson<TResponse>(responseText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ApiClient] GET JSON parse failed: {url}\nBody:\n{responseText}\n{ex}");
                throw;
            }
        }

        public static async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body)
        {
            Uri uri = BuildUri(path);
            string url = uri.ToString();

            string jsonBody = JsonUtility.ToJson(body);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            string responseText;

            try
            {
                response = await client.PostAsync(uri, content);
                responseText = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ApiClient] POST failed: {url}\nRequest:\n{jsonBody}\n{ex}");
                throw;
            }

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError($"[ApiClient] POST {(int)response.StatusCode} {response.ReasonPhrase}: {url}\nRequest:\n{jsonBody}\nBody:\n{responseText}");
                throw new HttpRequestException($"POST {url} failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            try
            {
                return JsonUtility.FromJson<TResponse>(responseText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ApiClient] POST JSON parse failed: {url}\nRequest:\n{jsonBody}\nBody:\n{responseText}\n{ex}");
                throw;
            }
        }
    }
}
