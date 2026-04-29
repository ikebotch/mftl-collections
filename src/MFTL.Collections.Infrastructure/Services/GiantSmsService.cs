using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MFTL.Collections.Infrastructure.Services;

public class GiantSmsService : ISmsService
{
    private readonly HttpClient _httpClient;
    private readonly GiantSmsOptions _options;
    private readonly ILogger<GiantSmsService> _logger;
    private const string BaseUrl = "https://api.giantsms.com/api/v1/";

    public GiantSmsService(
        HttpClient httpClient,
        IOptions<GiantSmsOptions> options,
        ILogger<GiantSmsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        if (string.IsNullOrEmpty(_options.Username) || string.IsNullOrEmpty(_options.Password))
        {
            _logger.LogWarning("GiantSMS credentials are not fully configured. SMS functionality may fail.");
        }
    }

    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        if (string.IsNullOrEmpty(_options.Username)) return;

        try
        {
            var normalizedPhone = NormalizePhoneNumber(phoneNumber);

            var payload = new GiantSmsSendRequest
            {
                From = _options.SenderId,
                To = normalizedPhone,
                Msg = message
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}send");
            AddBasicAuth(request);
            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request);
            await HandleResponseAsync(response, "SendSms");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", phoneNumber);
        }
    }

    public async Task<decimal> GetBalanceAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}balance?username={_options.Username}&password={_options.Password}");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GiantSmsResponse<GiantSmsBalanceData>>();
                if (result?.Status == true && result.Data != null)
                {
                    return result.Data.Credit;
                }
            }
            
            await HandleResponseAsync(response, "GetBalance");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get GiantSMS balance");
        }

        return 0;
    }

    public async Task<string> GetStatusAsync(string messageId)
    {
        try
        {
            var payload = new { message_id = messageId };
            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}status");
            AddBasicAuth(request);
            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GiantSmsResponse<GiantSmsStatusData>>();
                if (result?.Status == true && result.Data != null)
                {
                    return result.Data.Status;
                }
            }

            await HandleResponseAsync(response, "GetStatus");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check SMS status for {MessageId}", messageId);
        }

        return "Unknown";
    }

    private void AddBasicAuth(HttpRequestMessage request)
    {
        var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        var normalized = phoneNumber.Trim().Replace(" ", "").Replace("-", "");
        if (normalized.StartsWith("0") && normalized.Length == 10)
        {
            return "233" + normalized.Substring(1);
        }
        if (normalized.StartsWith("+"))
        {
            return normalized.Substring(1);
        }
        return normalized;
    }

    private async Task HandleResponseAsync(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogError("GiantSMS {Operation} failed: {StatusCode} - {Content}", operation, response.StatusCode, content);
            return;
        }

        var result = await response.Content.ReadFromJsonAsync<GiantSmsBaseResponse>();
        if (result?.Status == false)
        {
            _logger.LogError("GiantSMS {Operation} returned error status: {Message}", operation, result.Message);
        }
    }

    private class GiantSmsSendRequest
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        [JsonPropertyName("msg")]
        public string Msg { get; set; } = string.Empty;
    }

    private class GiantSmsBaseResponse
    {
        [JsonPropertyName("status")]
        public bool Status { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    private class GiantSmsResponse<T> : GiantSmsBaseResponse
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    private class GiantSmsBalanceData
    {
        [JsonPropertyName("credit")]
        public decimal Credit { get; set; }
    }

    private class GiantSmsStatusData
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }
}
