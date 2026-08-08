namespace BusinessOS.Domain.Interfaces;

// Implemented by every entity that supports Archive/Restore (Phase 6). Archiving is an
// explicit, visible feature (excluded from default list queries, but always reachable via
// ?includeArchived=true and GET .../:id) - NOT the silent soft-delete some other codebases
// use in place of a real DELETE. Hard delete (with its existing ConflictException guards)
// is untouched; archiving is a separate, reversible "hide from the default view" action.
public interface IArchivable : ITenantScoped
{
    bool IsArchived { get; set; }
    DateTime UpdatedAt { get; set; }
}
