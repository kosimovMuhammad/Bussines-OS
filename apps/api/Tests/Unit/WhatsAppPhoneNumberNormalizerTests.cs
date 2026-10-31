using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;

namespace BusinessOS.Tests.Unit;

public class WhatsAppPhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("99200989144051", "992989144051")] // Tajik "00" carrier quirk after the 992 country code
    [InlineData("+992989144051", "992989144051")] // already E.164 with a leading "+"
    [InlineData("989144051", "992989144051")] // bare 9-digit local number, no country code
    [InlineData("992989144051", "992989144051")] // already clean
    [InlineData("+992 98 914 40 51", "992989144051")] // spaces/formatting characters stripped
    public void NormalizeForWhatsApp_ProducesGraphApiFormat(string raw, string expected)
    {
        Assert.Equal(expected, WhatsAppPhoneNumberNormalizer.NormalizeForWhatsApp(raw));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("")]
    [InlineData("abc")]
    public void NormalizeForWhatsApp_ThrowsBadRequestException_ForMalformedInput(string raw)
    {
        Assert.Throws<BadRequestException>(() => WhatsAppPhoneNumberNormalizer.NormalizeForWhatsApp(raw));
    }
}
