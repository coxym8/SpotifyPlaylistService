using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpotifyPlaylistService.Services
{
    /// <summary>
    /// Implementation of Spotify OAuth 2.0 authentication
    /// </summary>
    public class SpotifyAuthService : ISpotifyAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly string _redirectUri = "http://127.0.0.1:8888/callback";
        private string? _clientId;
        private string? _clientSecret;

        public string? AccessToken { get; private set; }
        public string? RefreshToken { get; private set; }

        public SpotifyAuthService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<bool> AuthenticateAsync(string clientId, string clientSecret)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;

            var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:8888/");
            listener.Start();

            var scope = "user-library-read user-library-modify playlist-read-private playlist-read-collaborative playlist-modify-public playlist-modify-private";
            var authUrl = $"https://accounts.spotify.com/authorize?" +
                         $"client_id={_clientId}" +
                         $"&response_type=code" +
                         $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
                         $"&scope={Uri.EscapeDataString(scope)}";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Browser failed to open, URL should be displayed to user
            }

            var context = await listener.GetContextAsync();
            var code = context.Request.QueryString.Get("code");

            var response = context.Response;
            string responseString = "<html><body><h1>Success!</h1><p>You can close this window now.</p></body></html>";
            var buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
            listener.Stop();

            if (string.IsNullOrEmpty(code))
                return false;

            return await ExchangeCodeForTokenAsync(code);
        }

        private async Task<bool> ExchangeCodeForTokenAsync(string code)
        {
            if (_clientId == null || _clientSecret == null)
                return false;

            var tokenUrl = "https://accounts.spotify.com/api/token";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", _redirectUri)
            });

            var authHeader = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authHeader);

            var response = await _httpClient.PostAsync(tokenUrl, content);

            if (!response.IsSuccessStatusCode)
                return false;

            var responseContent = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseContent);

            AccessToken = doc.RootElement.GetProperty("access_token").GetString();
            RefreshToken = doc.RootElement.GetProperty("refresh_token").GetString();

            return true;
        }

        public async Task<bool> RefreshAccessTokenAsync()
        {
            if (string.IsNullOrEmpty(RefreshToken) || _clientId == null || _clientSecret == null)
                return false;

            var tokenUrl = "https://accounts.spotify.com/api/token";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", RefreshToken)
            });

            var authHeader = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", authHeader);

            var response = await _httpClient.PostAsync(tokenUrl, content);

            if (!response.IsSuccessStatusCode)
                return false;

            var responseContent = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseContent);

            AccessToken = doc.RootElement.GetProperty("access_token").GetString();

            return true;
        }
    }
}
