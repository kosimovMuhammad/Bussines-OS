using System.Collections.Immutable;

namespace BusinessOS.Application.Services;

// Ambient chain of WorkflowIds currently executing in this logical call chain. Flows
// correctly through direct nested awaits (RunWorkflowAsync -> action -> SaveChangesAsync
// -> interceptor, all on one call stack) but NOT across the IBackgroundTaskQueue boundary
// (a dequeue-and-invoke cycle is a fresh top-level async operation, not a continuation of
// the enqueuing call). That's why EntityChangedEvent explicitly carries the chain across
// that boundary (see ActivityLogSaveChangesInterceptor / WorkflowExecutionService), and
// PushAll restores it on the other side.
public static class WorkflowExecutionContext
{
    private static readonly AsyncLocal<ImmutableHashSet<string>?> Holder = new();

    public static ImmutableHashSet<string> Current => Holder.Value ?? ImmutableHashSet<string>.Empty;

    public static IDisposable Push(string workflowId)
    {
        var previous = Holder.Value ?? ImmutableHashSet<string>.Empty;
        Holder.Value = previous.Add(workflowId);
        return new Popper(previous);
    }

    public static IDisposable PushAll(IEnumerable<string> workflowIds)
    {
        var previous = Holder.Value ?? ImmutableHashSet<string>.Empty;
        Holder.Value = previous.Union(workflowIds);
        return new Popper(previous);
    }

    private sealed class Popper(ImmutableHashSet<string> previous) : IDisposable
    {
        public void Dispose() => Holder.Value = previous;
    }
}
