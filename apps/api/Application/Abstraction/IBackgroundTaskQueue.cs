namespace BusinessOS.Application.Abstraction;

// In-process replacement for BullMQ - no Redis, no external broker. A lost item on
// process restart just leaves whatever DB row it would have updated in its prior
// status (e.g. still Pending) - acceptable per the "no queue infra" decision; see
// the phase-1 plan's Architecture section for the durability tradeoff discussion.
public interface IBackgroundTaskQueue
{
    ValueTask QueueAsync(Func<IServiceProvider, CancellationToken, ValueTask> workItem);

    ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken);
}
