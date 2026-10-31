using System.Net.Http.Json;
using System.Text.Json;
using BusinessOS.Application.Abstraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Ai;

public class GeminiClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GeminiClient> logger) : IGeminiClient
{
    private const string Model = "gemini-3.6-flash";

    public async Task<string> GenerateAsync(string prompt, bool jsonMode = false, int maxOutputTokens = 2048, CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["GEMINI_API_KEY"];
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("GEMINI_API_KEY is not configured");
        }

        var client = httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={apiKey}",
            new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new
                {
                    maxOutputTokens,
                    responseMimeType = jsonMode ? "application/json" : "text/plain",
                },
            },
            cancellationToken);

        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogDebug("Gemini raw response ({StatusCode}): {Body}", (int)response.StatusCode, rawBody);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Gemini API error: {response.StatusCode} {rawBody}");
        }

        var body = JsonSerializer.Deserialize<JsonElement>(rawBody);

        if (!body.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            var blockReason = body.TryGetProperty("promptFeedback", out var feedback) && feedback.TryGetProperty("blockReason", out var reason)
                ? reason.GetString()
                : "no candidates returned";
            throw new InvalidOperationException($"Gemini returned no usable response (reason: {blockReason})");
        }

        var candidate = candidates[0];
        var finishReason = candidate.TryGetProperty("finishReason", out var fr) ? fr.GetString() : null;
        if (!candidate.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"Gemini returned no content (finishReason: {finishReason ?? "unknown"})");
        }

        return parts[0].GetProperty("text").GetString() ?? string.Empty;
    }
}
