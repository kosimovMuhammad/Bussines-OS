namespace BusinessOS.Domain.Interfaces;

// Implemented by entities that directly carry a BranchId column (as opposed to deriving
// branch through a Contact navigation - see IContactBranchScoped). Null BranchId means
// "not assigned to a branch yet" and stays visible to every branch, not just Owner.
public interface IBranchScoped : ITenantScoped
{
    string? BranchId { get; set; }
}
