using Warudo.Core.Attributes;
using Warudo.Core.Graphs;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Warudo.Plugins.DonationAlerts;
using UnityEngine;

[NodeType(
    Id = "ntExist-464c1fe2-88ea-406c-95cd-bba570f8e8ba",
    Title = "DonationAlerts Node",
    Category = "DonationAlerts",
    Width = 1.5f)]
public class DonationAlertsNode : Node
{
    [DataInput]
    [Markdown]
    public string UpdateTimeInfo = "Use only one node!<br/>If you need two or more, you must not exceed the update limit from all nodes more often than one per one second.";

    [DataInput]
    [Label("Update Time in Seconds")]
    [FloatSlider(1.0f, 60.0f, 0.1f)]
    public float UpdateTime = 1.0f;

    [FlowOutput]
    [Label("Exit")]
    public Continuation OnNewDonation;

    private int _donationId;
    private string _username;
    private float _amount;
    private string _currency;
    private string _message;
    private DateTime _createdAt;
    private bool _isRequestSuccessful;
    private string _messageType;

    [DataOutput]
    [Label("Successful")]
    public bool GetIsRequestSuccessful() => _isRequestSuccessful;

    [DataOutput]
    [Label("ID")]
    public int GetDonationId() => _donationId;

    [DataOutput]
    [Label("Message Type")]
    public string MessageType() => _messageType;

    [DataOutput]
    [Label("Username")]
    public string GetUsername() => _username;

    [DataOutput]
    [Label("Message")]
    public string GetMessage() => _message;

    [DataOutput]
    [Label("Amount")]
    public float GetAmount() => _amount;

    [DataOutput]
    [Label("Currency")]
    public string GetCurrency() => _currency;

    [DataOutput]
    [Label("Created At")]
    public DateTime GetCreatedAt() => _createdAt;

    private int _lastDonationId = 0;
    private DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly HttpClient _httpClient = new HttpClient();
    private int PollingInterval => (int)(UpdateTime * 1000);

    protected override void OnCreate()
    {
        _ = PollDonationsAsync();
    }

    private async Task PollDonationsAsync()
    {
        while (true)
        {
            try
            {
                await ThrottleRequests();

                var accessToken = DonationAlerts.Instance?._accessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    UpdateRequestStatus(false);
                    continue;
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.GetAsync(
                    "https://www.donationalerts.com/api/v1/alerts/donations");

                if (!response.IsSuccessStatusCode)
                {
                    UpdateRequestStatus(false);
                    continue;
                }

                var responseBody = await response.Content.ReadAsStringAsync();

                try
                {
                    var donationsResponse = JsonConvert.DeserializeObject<DonationsResponse>(responseBody);
                    if (donationsResponse?.Data == null)
                    {
                        UpdateRequestStatus(false);
                        continue;
                    }

                    if (donationsResponse.Data.Count == 0)
                    {
                        UpdateRequestStatus(true);
                        continue;
                    }

                    UpdateRequestStatus(true);

                    var latestDonation = donationsResponse.Data[0];
                    if (latestDonation != null && latestDonation.Id != _lastDonationId)
                    {
                        UpdateDonationData(latestDonation);
                        _lastDonationId = latestDonation.Id;
                        InvokeFlow(nameof(OnNewDonation));
                    }
                }
                catch (JsonException)
                {
                    UpdateRequestStatus(false);
                }
            }
            catch (Exception)
            {
                UpdateRequestStatus(false);
            }

            await Task.Delay(PollingInterval);
        }
    }

    private void UpdateRequestStatus(bool isSuccessful)
    {
        if (_isRequestSuccessful != isSuccessful)
        {
            _isRequestSuccessful = isSuccessful;
            BroadcastDataInput(nameof(GetIsRequestSuccessful));
        }
    }

    private async Task ThrottleRequests()
    {
        var timeSinceLastRequest = DateTime.Now - _lastRequestTime;
        if (timeSinceLastRequest.TotalMilliseconds < PollingInterval)
        {
            await Task.Delay(PollingInterval - (int)timeSinceLastRequest.TotalMilliseconds);
        }
        _lastRequestTime = DateTime.Now;
    }

    private void UpdateDonationData(Donation donation)
    {
        if (donation == null) return;

        _donationId = donation.Id;
        _username = donation.Username ?? "";
        _amount = donation.Amount;
        _currency = donation.Currency ?? "";
        _message = donation.Message ?? "";
        _createdAt = donation.CreatedAt;
        _messageType = donation.MessageType;

        BroadcastDataInput(nameof(GetDonationId));
        BroadcastDataInput(nameof(GetUsername));
        BroadcastDataInput(nameof(GetAmount));
        BroadcastDataInput(nameof(GetCurrency));
        BroadcastDataInput(nameof(GetMessage));
        BroadcastDataInput(nameof(GetCreatedAt));
        BroadcastDataInput(nameof(MessageType));
    }

    [Serializable]
    private class DonationsResponse
    {
        [JsonProperty("data")]
        public List<Donation> Data { get; set; } = new List<Donation>();
    }

    [Serializable]
    private class Donation
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; } = "";

        [JsonProperty("amount")]
        public float Amount { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; } = "";

        [JsonProperty("message")]
        public string Message { get; set; } = "";

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("message_type")]
        public string MessageType { get; set; } = "";
    }
}
