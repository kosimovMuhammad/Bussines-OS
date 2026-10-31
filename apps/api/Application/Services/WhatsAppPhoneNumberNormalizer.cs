using BusinessOS.Application.Configurations.Exceptions;

namespace BusinessOS.Application.Services;

// Normalizes a phone number into the exact "to" format Meta's WhatsApp Graph API expects:
// country code + local number, digits only, no "+", no leading "00". Tajik MegaFon (and
// possibly other Tajik carrier) numbers are commonly entered/stored with a literal "00"
// right after the "992" country code - a local dialing convention that Meta's API rejects
// or mishandles - so that has to be stripped here, once, for every WhatsApp send path (see
// WhatsAppService.SendAsync / SendPlatformTextMessageAsync / SendPlatformTemplateMessageAsync).
public static class WhatsAppPhoneNumberNormalizer
{
    private const string TajikCountryCode = "992";
    private const int TajikLocalNumberLength = 9;
    private const int MinLength = 11;
    private const int MaxLength = 15;

    // Lenient, non-throwing cleanup used for cross-channel phone matching (e.g.
    // TelegramService.HandleSharedContactAsync matching a shared Telegram contact against
    // existing Contacts by phone) - unlike NormalizeForWhatsApp below, this has no opinion on
    // country code or length, so it works for phone numbers from any country.
    public static string Normalize(string phone)
    {
        var digits = phone.Replace(" ", string.Empty).Replace("-", string.Empty);

        if (digits.StartsWith('+'))
        {
            digits = digits[1..];
        }
        else if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        return digits;
    }

    public static string NormalizeForWhatsApp(string rawPhone)
    {
        var digits = new string((rawPhone ?? string.Empty).Where(char.IsDigit).ToArray());

        if (digits.StartsWith(TajikCountryCode + "00", StringComparison.Ordinal))
        {
            digits = TajikCountryCode + digits[(TajikCountryCode.Length + 2)..];
        }
        else if (!digits.StartsWith(TajikCountryCode, StringComparison.Ordinal) && digits.Length == TajikLocalNumberLength)
        {
            digits = TajikCountryCode + digits;
        }

        if (digits.Length < MinLength || digits.Length > MaxLength || !digits.All(char.IsDigit))
        {
            throw new BadRequestException($"Invalid WhatsApp phone number: raw='{rawPhone}' normalized='{digits}' (expected {MinLength}-{MaxLength} digits)");
        }

        return digits;
    }
}
