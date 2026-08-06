namespace BusinessOS.Application.Abstraction;

public interface IGeminiClient
{
    Task<string> GenerateAsync(string prompt, bool jsonMode = false, int maxOutputTokens = 2048, CancellationToken cancellationToken = default);
}
