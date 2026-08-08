namespace BusinessOS.Domain.Interfaces;

// Implemented by log/history entities that must never be updated or deleted once
// written (AuditLog, LoginHistory). Enforced at the API level by only ever exposing
// GET endpoints for these tables, and defense-in-depth at the EF level by
// ImmutableRecordSaveChangesInterceptor, which rejects any Modified/Deleted entry.
public interface IImmutableRecord;
