using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Telegram;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Telegram;

public class TelegramApiClient(IHttpClientFactory httpClientFactory, ILogger<TelegramApiClient> logger) : ITelegramApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<TelegramUser> GetMeAsync(string botToken, CancellationToken cancellationToken = default) =>
        await SendAsync<TelegramUser>(botToken, "getMe", new { }, cancellationToken)
        ?? throw new ExternalServiceException("Telegram API returned an empty getMe result");

    public async Task<long?> SendMessageAsync(string botToken, long chatId, string text, CancellationToken cancellationToken = default)
    {
        var sent = await SendAsync<TelegramMessage>(botToken, "sendMessage", new { chat_id = chatId, text }, cancellationToken);
        return sent?.MessageId;
    }

    public Task SendMessageWithContactRequestAsync(string botToken, long chatId, string text, string buttonText, CancellationToken cancellationToken = default) =>
        SendAsync<JsonElement>(botToken, "sendMessage", new
        {
            chat_id = chatId,
            text,
            reply_markup = new
            {
                keyboard = new[] { new[] { new { text = buttonText, request_contact = true } } },
                resize_keyboard = true,
                one_time_keyboard = true,
            },
        }, cancellationToken);

    public Task SetWebhookAsync(string botToken, string webhookUrl, string secretToken, CancellationToken cancellationToken = default) =>
        SendAsync<JsonElement>(botToken, "setWebhook", new { url = webhookUrl, secret_token = secretToken }, cancellationToken);

    // Single place that actually calls the Bot API - every method funnels through here so
    // there is exactly one HTTP-call/error-parsing implementation (same reasoning as
    // MetaWhatsAppClient.SendAsync).
    private async Task<T?> SendAsync<T>(string botToken, string method, object payload, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.PostAsJsonAsync($"https://api.telegram.org/bot{botToken}/{method}", payload, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        // Distinguish "our own timeout/network failure" from the caller actually cancelling
        // the request - only the former should degrade to a 502 instead of propagating as
        // cancellation (mirrors the Redis-unreachable fallback pattern).
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Telegram API request '{Method}' failed to complete", method);
            throw new ExternalServiceException($"Telegram API request '{method}' timed out or is unreachable") { IsTimeout = true };
        }

        TelegramApiResponse<T>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<TelegramApiResponse<T>>(body, JsonOptions);
        }
        catch (JsonException)
        {
            envelope = null;
        }

        if (envelope is null || !envelope.Ok)
        {
            // Never log botToken - it's embedded in the request URL, not the response body.
            logger.LogWarning(
                "Telegram API rejected {Method}: status={StatusCode} description={Description}",
                method, (int)response.StatusCode, envelope?.Description);

            throw new ExternalServiceException(envelope?.Description ?? $"Telegram API request '{method}' failed: {response.StatusCode}")
            {
                ProviderStatusCode = envelope?.ErrorCode ?? (int)response.StatusCode,
                RetryAfterSeconds = envelope?.Parameters?.RetryAfter ?? (response.Headers.RetryAfter?.Delta is { } delta ? (int)delta.TotalSeconds : null),
            };
        }

        return envelope.Result;
    }

    private class TelegramApiResponse<T>
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public T? Result { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("error_code")]
        public int? ErrorCode { get; set; }

        [JsonPropertyName("parameters")]
        public TelegramResponseParameters? Parameters { get; set; }
    }

    private class TelegramResponseParameters
    {
        [JsonPropertyName("retry_after")]
        public int? RetryAfter { get; set; }
    }
}
