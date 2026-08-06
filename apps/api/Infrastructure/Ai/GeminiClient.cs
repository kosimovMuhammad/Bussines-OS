using System.Net.Http.Json;
using System.Text.Json;
using BusinessOS.Application.Abstraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;

namespace BusinessOS.Infrastructure.Ai;

public class GeminiClient(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IGeminiClient
{
    private const string Model = "gemini-flash-latest";

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

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Gemini API error: {response.StatusCode} {errorBody}");
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        return body
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }
}
