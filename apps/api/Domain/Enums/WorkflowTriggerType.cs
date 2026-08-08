namespace BusinessOS.Domain.Enums;

public enum WorkflowTriggerType
{
    RecordCreated,
    RecordUpdated,
    RecordStatusChanged,
    TimeBased,
    FieldValueMatches,
}
