using BusinessOS.Infrastructure.Search;

namespace BusinessOS.Tests.Unit;

public class SearchServiceEscapingTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("50%off", "50\\%off")]
    [InlineData("under_score", "under\\_score")]
    [InlineData("back\\slash", "back\\\\slash")]
    [InlineData("100%_done\\now", "100\\%\\_done\\\\now")]
    public void EscapeLikePattern_EscapesLikeMetacharacters(string input, string expected)
    {
        Assert.Equal(expected, SearchService.EscapeLikePattern(input));
    }
}
