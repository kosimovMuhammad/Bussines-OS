namespace BusinessOS.Application.Services;

public interface ITaskDueNotificationService
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
