using Warudo.Core.Attributes;
using Warudo.Core.Plugins;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Warudo.Plugins.DonationAlerts
{
    [PluginType(
        Id = "ntExist.DonationAlerts",
        Name = "DonationAlerts",
        Description = "DonationAlerts is one of the largest stream monetization services.",
        Author = "ntExist",
        Version = "1.0.2",
        NodeTypes = new[] {
            typeof(DonationAlertsNode)
        },
        Icon = Icon)]
    public class DonationAlerts : Plugin
    {
        public static DonationAlerts Instance { get; private set; }
        public const string Icon = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"37\" height=\"43\" viewBox=\"0 0 37 43\"><defs><linearGradient id=\"a\" x1=\"86.328%\" x2=\"8.51%\" y1=\"11.463%\" y2=\"100%\"><stop offset=\"0%\" stop-color=\"#F59C07\"/><stop offset=\"100%\" stop-color=\"#F57507\"/></linearGradient></defs><path fill=\"url(#a)\" fill-rule=\"nonzero\" d=\"M18.692 25.041h-2.906a.63.63 0 0 1-.445-.175.502.502 0 0 1-.152-.415l.257-2.626c.025-.28.285-.495.596-.494h2.907c.17 0 .33.063.445.176.113.112.169.263.152.414l-.257 2.627c-.025.28-.285.494-.597.493zm.466-5.143h-2.96a.582.582 0 0 1-.593-.571l.806-8.875a.585.585 0 0 1 .592-.503h2.96c.327 0 .593.256.593.571l-.83 8.88a.585.585 0 0 1-.568.498zM36.566 9.549L28.898.63A1.81 1.81 0 0 0 27.525 0H4.56a1.803 1.803 0 0 0-1.8 1.616L.006 32.896c-.044.503.126 1 .468 1.373a1.81 1.81 0 0 0 1.332.582h4.51L5.63 43l8.869-8.143h10.074c.47 0 .922-.18 1.26-.507l9.462-9.155c.312-.302.504-.705.541-1.137l1.157-13.184a1.794 1.794 0 0 0-.427-1.325zm-7.013 11.994a1.796 1.796 0 0 1-.541 1.142l-5.478 5.197a1.81 1.81 0 0 1-1.249.496h-13.4a1.831 1.831 0 0 1-1.324-.59 1.816 1.816 0 0 1-.476-1.365L8.707 8.11a1.803 1.803 0 0 1 1.8-1.616h13.628c.522 0 1.02.226 1.362.62l4.326 4.976c.326.358.494.832.465 1.314l-.735 8.138z\"/></svg>\n";

        [Markdown(primary: false)]
        public string Info = "";

        [DataInput]
        [Label("Authorization Code")]
        [DisabledIf(nameof(_authState), If.Equal, 1)]
        public string Code;

        private const string ClientId = "14335";
        private const string ClientSecret = "LLrNifO1ShQBPlEiROISafbD4qEYmVKWoHtEgDi5";
        private const string RedirectUri = "https://ntexistgit.github.io/DonationAlertsCode/";

        [DataInput]
        [Hidden]
        public int _authState = 0;

        [DataInput]
        [Hidden]
        public string _accessToken;

        [DataInput]
        [Hidden]
        public string _refreshToken;

        [Markdown(primary: false)]
        public string Error = "";

        private static readonly HttpClient _httpClient = new HttpClient();
        private DateTime _lastRequestTime = DateTime.MinValue;

        private void UpdateInfo(string value)
        {
            if (Info != value)
            {
                Info = value;
                BroadcastDataInput(nameof(Info));
            }
        }

        private void UpdateError(string value)
        {
            if (Error != value)
            {
                Error = value;
                BroadcastDataInput(nameof(Error));
            }
        }

        [Trigger]
        [DisabledIf(nameof(_authState), If.Equal, 1)]
        public void OpenLogin()
        {
            try
            {
                string loginUrl = $"https://www.donationalerts.com/oauth/authorize?client_id={ClientId}&redirect_uri={RedirectUri}&response_type=code&scope=oauth-user-show oauth-donation-index";
                Application.OpenURL(loginUrl);
                UpdateInfo(null);
                UpdateError("Please enter the authorization code manually after logging in.");
            }
            catch (Exception ex)
            {
                UpdateInfo(null);
                UpdateError($"Error opening authorization page: {ex.Message}");
                _authState = 0;
            }
        }

        [Trigger]
        [DisabledIf(nameof(_authState), If.Equal, 1)]
        public async Task SubmitCode()
        {
            if (string.IsNullOrEmpty(Code))
            {
                UpdateInfo(null);
                UpdateError("Authorization code is missing");
                _authState = 0;
                return;
            }

            await ExchangeCodeForTokenAsync();
        }

        [Trigger]
        [DisabledIf(nameof(_authState), If.Equal, 0)]
        public void ResetAuth()
        {
            _authState = 0;
            _accessToken = null;
            _refreshToken = null;
            Code = null;
            UpdateInfo(null);
            UpdateError(null);
            BroadcastDataInput(nameof(Code));
        }

        private async Task ThrottleRequests()
        {
            var timeSinceLastRequest = DateTime.Now - _lastRequestTime;
            if (timeSinceLastRequest.TotalMilliseconds < 1000)
            {
                await Task.Delay(1000 - (int)timeSinceLastRequest.TotalMilliseconds);
            }

            _lastRequestTime = DateTime.Now;
        }

        private async Task ExchangeCodeForTokenAsync()
        {
            await ThrottleRequests();

            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", ClientSecret),
                new KeyValuePair<string, string>("redirect_uri", RedirectUri),
                new KeyValuePair<string, string>("code", Code)
            });

            HttpResponseMessage response = await _httpClient.PostAsync("https://www.donationalerts.com/oauth/token", requestBody);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                UpdateInfo(null);
                UpdateError($"Error getting token: {response.StatusCode}");
                _authState = 0;
                return;
            }

            TokenResponse tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseBody);

            if (!string.IsNullOrEmpty(tokenResponse?.error))
            {
                UpdateInfo(null);
                UpdateError($"Authorization error: {tokenResponse.error_description ?? tokenResponse.error}");
                _authState = 0;
                return;
            }

            _accessToken = tokenResponse.access_token;
            _refreshToken = tokenResponse.refresh_token;

            if (string.IsNullOrEmpty(_accessToken))
            {
                UpdateInfo(null);
                UpdateError("Received empty access token");
                _authState = 0;
                return;
            }

            _authState = 1;
            UpdateError(null);

            await FetchUserInfoAsync();
        }

        private async Task FetchUserInfoAsync()
        {

            await ThrottleRequests();

            if (string.IsNullOrEmpty(_accessToken))
            {
                UpdateInfo(null);
                UpdateError("Access token is missing");
                _authState = 0;
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            HttpResponseMessage response = await _httpClient.GetAsync("https://www.donationalerts.com/api/v1/user/oauth");
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(_refreshToken))
                {
                    await RefreshTokenAsync();
                    return;
                }

                UpdateInfo(null);
                UpdateError($"API error: {response.StatusCode}");
                _accessToken = null;
                _authState = 0;
                return;
            }

            UserResponse userResponse = JsonConvert.DeserializeObject<UserResponse>(responseBody);

            if (userResponse == null || userResponse.data == null)
            {
                UpdateInfo(null);
                UpdateError("Failed to deserialize user info");
                _authState = 0;
                return;
            }

            string avatar = userResponse.data.avatar.ToString();
            string userName = userResponse.data.name;
            string userId = userResponse.data.id.ToString();

            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userId))
            {
                UpdateInfo(null);
                UpdateError("Failed to get user data");
                _authState = 0;
                return;
            }

            UpdateError(null);
            UpdateInfo($"<div style=\"display: flex; align-items: center; gap: 8px; height: 48px;\"><img src=\"{avatar}\" style=\"height: 32px; width: 32px; border-radius: 50%;\"/><span style=\"display: flex; align-items: center; line-height: 24px;\">Logged in as <span style=\"font-weight: bold;\">&nbsp;{userName}&nbsp;</span> (ID: {userId})</span></div>");
            _authState = 1;
        }

        private async Task RefreshTokenAsync()
        {
            await ThrottleRequests();

            if (string.IsNullOrEmpty(_refreshToken))
            {
                UpdateInfo(null);
                UpdateError("Refresh token is missing");
                _authState = 0;
                return;
            }

            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", ClientSecret),
                new KeyValuePair<string, string>("refresh_token", _refreshToken)
            });

            HttpResponseMessage response = await _httpClient.PostAsync("https://www.donationalerts.com/oauth/token", requestBody);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                UpdateInfo(null);
                UpdateError($"Error refreshing token: {response.StatusCode}");
                _authState = 0;
                return;
            }

            TokenResponse tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseBody);

            if (!string.IsNullOrEmpty(tokenResponse?.error))
            {
                UpdateInfo(null);
                UpdateError($"Token refresh error: {tokenResponse.error_description ?? tokenResponse.error}");
                _authState = 0;
                return;
            }

            _accessToken = tokenResponse.access_token;
            _refreshToken = tokenResponse.refresh_token;

            if (string.IsNullOrEmpty(_accessToken))
            {
                UpdateInfo(null);
                UpdateError("Received empty access token");
                _authState = 0;
                return;
            }

            _authState = 1;
            await FetchUserInfoAsync();
        }

        protected override async void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            await Task.Delay(1000);

            if (!string.IsNullOrEmpty(_accessToken))
            {
                _ = FetchUserInfoAsync();
            }
            else if (!string.IsNullOrEmpty(Code))
            {
                _ = ExchangeCodeForTokenAsync();
            }
            else
            {
                ResetAuth();
            }
        }

        [Serializable]
        private class TokenResponse
        {
            public string access_token;
            public string refresh_token;
            public string error;
            public string error_description;
        }

        [Serializable]
        private class UserResponse
        {
            public UserData data;
        }

        [Serializable]
        private class UserData
        {
            public int id;
            public string code;
            public string name;
            public int is_active;
            public string avatar;
            public string email;
            public string language;
            public string socket_connection_token;
        }
    }
}
