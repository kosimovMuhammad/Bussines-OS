using System.Net.Http.Json;
using BusinessOS.Application.Abstraction;
using Microsoft.Extensions.Http;

namespace BusinessOS.Infrastructure.WhatsApp;

public class MetaWhatsAppClient(IHttpClientFactory httpClientFactory) : IWhatsAppClient
{
    public async Task SendTextMessageAsync(string phoneNumberId, string accessToken, string to, string message, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync(
            $"https://graph.facebook.com/v20.0/{phoneNumberId}/messages",
            new
            {
                messaging_product = "whatsapp",
                to,
                type = "text",
                text = new { body = message },
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"WhatsApp API rejected the message: {response.StatusCode} {body}");
        }
    }
}
