namespace BusinessOS.Application.Constants;

// Self-reported persona collected on the registration form (e.g. "What's your role?").
// Purely for onboarding personalization - has no bearing on Domain.Enums.Role/RBAC.
public static class OnboardingRoles
{
    public const string Founder = "Founder";
    public const string Sales = "Sales";
    public const string Marketing = "Marketing";
    public const string Operations = "Operations";
    public const string Support = "Support";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = [Founder, Sales, Marketing, Operations, Support, Other];
}
