using System.Text.Json;
using BusinessOS.Infrastructure.Instagram;

namespace BusinessOS.Tests.Unit;

public class InstagramOAuthParserTests
{
    [Fact]
    public void ParseShortLivedAccessToken_DataArrayShape_ReturnsToken() =>
        Assert.Equal("IGAA123", InstagramOAuthParser.ParseShortLivedAccessToken(
            """{"data":[{"access_token":"IGAA123","user_id":"1784","permissions":"instagram_business_basic"}]}"""));

    [Fact]
    public void ParseShortLivedAccessToken_FlatShape_ReturnsToken() =>
        Assert.Equal("IGAA123", InstagramOAuthParser.ParseShortLivedAccessToken("""{"access_token":"IGAA123","user_id":1784}"""));

    [Theory]
    [InlineData("""{"data":[]}""")]
    [InlineData("""{"data":[{"user_id":"1"}]}""")]
    [InlineData("""{"access_token":""}""")]
    [InlineData("""{"access_token":null}""")]
    [InlineData("[]")]
    public void ParseShortLivedAccessToken_NoToken_ThrowsJsonException(string body) =>
        Assert.Throws<JsonException>(() => InstagramOAuthParser.ParseShortLivedAccessToken(body));

    [Theory]
    [InlineData("AQBabc#_", "AQBabc")]
    [InlineData("  AQBabc#_ ", "AQBabc")]
    [InlineData("AQBabc", "AQBabc")]
    [InlineData("AQB#_abc", "AQB#_abc")]
    public void NormalizeCode_StripsTrailingFragmentMarker(string input, string expected) =>
        Assert.Equal(expected, InstagramOAuthParser.NormalizeCode(input));
}
