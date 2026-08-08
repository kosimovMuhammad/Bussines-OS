using BusinessOS.Application.Services;

namespace BusinessOS.Tests.Unit;

public class TemplateResolverTests
{
    [Fact]
    public void Resolve_SingleToken_Substitutes()
    {
        var values = new Dictionary<string, string?> { ["contact.name"] = "Bob" };
        Assert.Equal("Hello Bob!", TemplateResolver.Resolve("Hello {{contact.name}}!", values));
    }

    [Fact]
    public void Resolve_MultipleTokens_SubstitutesAll()
    {
        var values = new Dictionary<string, string?> { ["deal.title"] = "Big Deal", ["deal.value"] = "50000" };
        Assert.Equal("Big Deal is worth 50000", TemplateResolver.Resolve("{{deal.title}} is worth {{deal.value}}", values));
    }

    [Fact]
    public void Resolve_UnknownToken_LeftAsIs()
    {
        var values = new Dictionary<string, string?>();
        Assert.Equal("Hello {{unknown.token}}", TemplateResolver.Resolve("Hello {{unknown.token}}", values));
    }

    [Fact]
    public void Resolve_TokenWithNullValue_SubstitutesEmptyString()
    {
        var values = new Dictionary<string, string?> { ["contact.name"] = null };
        Assert.Equal("Hello !", TemplateResolver.Resolve("Hello {{contact.name}}!", values));
    }

    [Fact]
    public void Resolve_NoTokens_ReturnsUnchanged()
    {
        Assert.Equal("Plain text", TemplateResolver.Resolve("Plain text", new Dictionary<string, string?>()));
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["Deal.Value"] = "100" };
        Assert.Equal("Value: 100", TemplateResolver.Resolve("Value: {{deal.value}}", values));
    }
}
