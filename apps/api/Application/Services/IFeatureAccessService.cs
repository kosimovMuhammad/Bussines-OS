namespace BusinessOS.Application.Services;

public interface IFeatureAccessService
{
    // No subscription => every feature is treated as unavailable (fails closed - the
    // opposite of ILimitEnforcementService's numeric-limit fail-open behavior, since a
    // missing subscription here means "nothing has been paid for", not "a data gap").
    Task<bool> HasFeatureAsync(string companyId, string featureKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, bool>> GetFeatureFlagsAsync(string companyId, CancellationToken cancellationToken = default);
}
