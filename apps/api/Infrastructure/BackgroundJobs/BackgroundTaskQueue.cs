using System.Threading.Channels;
using BusinessOS.Application.Abstraction;

namespace BusinessOS.Infrastructure.BackgroundJobs;

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, ValueTask>> _channel =
        Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, ValueTask>>();

    public ValueTask QueueAsync(Func<IServiceProvider, CancellationToken, ValueTask> workItem) =>
        _channel.Writer.WriteAsync(workItem);

    public ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
