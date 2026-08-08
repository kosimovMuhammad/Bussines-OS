namespace BusinessOS.Application.Constants;

public static class SearchTypes
{
    public const string Contacts = "contacts";
    public const string Deals = "deals";
    public const string Tasks = "tasks";
    public const string Users = "users";
    public const string Files = "files";
    public const string Emails = "emails";
    public const string Messages = "messages";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Contacts, Deals, Tasks, Users, Files, Emails, Messages,
    };

    // Which [RequireModule] gates the underlying data for each search type - used both to
    // filter the aggregate /search response down to what the caller's role can actually
    // read, and to pick the [RequireModule] for each /search/{type} route. Emails and
    // Messages both read the "communications" table (split by CommunicationChannel), so
    // both sit behind the same Communications module as /communications itself.
    public static readonly IReadOnlyDictionary<string, ModuleName> GoverningModule = new Dictionary<string, ModuleName>(StringComparer.OrdinalIgnoreCase)
    {
        [Contacts] = ModuleName.Contacts,
        [Deals] = ModuleName.Deals,
        [Tasks] = ModuleName.Tasks,
        [Users] = ModuleName.Users,
        [Files] = ModuleName.Files,
        [Emails] = ModuleName.Communications,
        [Messages] = ModuleName.Communications,
    };
}
