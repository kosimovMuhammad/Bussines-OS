namespace BusinessOS.Domain.Interfaces;

// Implemented by entities whose create/update/delete lifecycle should be recorded
// in the ActivityLog feed. ActivityLogSaveChangesInterceptor picks up any tracked
// entry implementing this - adding a new loggable entity is just implementing the
// interface, no interceptor changes needed.
public interface IActivityLoggable : ITenantScoped;
