namespace VantaiViet.CoreApi.Services.Interfaces;
public interface INotificationQueueService
{
    Task ProcessNextAsync(CancellationToken cancellationToken);
}
