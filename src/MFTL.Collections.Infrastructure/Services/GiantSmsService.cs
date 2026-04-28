using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MFTL.Collections.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MFTL.Collections.Infrastructure.Services;

public class GiantSmsService : ISmsService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _senderId;
    private readonly ILogger<GiantSmsService> _logger;

    public GiantSmsService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GiantSmsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["GiantSms:ApiKey"] ?? string.Empty;
        _senderId = configuration["GiantSms:SenderId"] ?? "MFTL";

        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("GiantSMS API Key is not configured. SMS sending will fail.");
        }
    }

    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogError("Cannot send SMS: GiantSMS API Key is missing.");
            return;
        }

        try
        {
            // Normalize phone number (ensure it starts with 233 for Ghana if it starts with 0)
            var normalizedPhone = phoneNumber.Trim();
            if (normalizedPhone.StartsWith("0"))
            {
                normalizedPhone = "233" + normalizedPhone.Substring(1);
            }

            var request = new GiantSmsRequest
            {
                From = _senderId,
                To = normalizedPhone,
                Msg = message
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.giantsms.com/api/v3/send");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            httpRequest.Content = JsonContent.Create(request);

            var response = await _httpClient.SendAsync(httpRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("GiantSMS API Error: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
            }
            else
            {
                _logger.LogInformation("SMS sent successfully via GiantSMS to {PhoneNumber}", phoneNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS via GiantSMS to {PhoneNumber}", phoneNumber);
        }
    }

    private class GiantSmsRequest
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        [JsonPropertyName("msg")]
        public string Msg { get; set; } = string.Empty;
    }
}
