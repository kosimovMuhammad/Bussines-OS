using System.Collections.Concurrent;

namespace BusinessOS.Application.Services;

// Single-instance only, by design for this pass: bounds a misconfigured workflow's blast
// radius on this process so it can't hammer WhatsApp/Email providers or the DB. A
// multi-instance deployment would need a distributed limiter (e.g. a Redis sliding
// window via Upstash) instead - not implemented here because no Redis client is wired
// into this codebase at all yet (confirmed by recon: "Redis via Upstash" is in the
// target stack description, but nothing in the code actually uses it today).
public class InMemoryWorkflowRateLimiter : IWorkflowRateLimiter
{
    private const int MaxExecutionsPerWindow = 60;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> executionTimestamps = new();

    public bool TryAcquire(string companyId)
    {
        var now = DateTime.UtcNow;
        var queue = executionTimestamps.GetOrAdd(companyId, _ => new ConcurrentQueue<DateTime>());

        while (queue.TryPeek(out var oldest) && now - oldest > Window)
        {
            queue.TryDequeue(out _);
        }

        if (queue.Count >= MaxExecutionsPerWindow)
        {
            return false;
        }

        queue.Enqueue(now);
        return true;
    }
}
