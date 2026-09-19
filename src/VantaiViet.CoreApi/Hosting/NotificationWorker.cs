using VantaiViet.CoreApi.Services.Interfaces;
namespace VantaiViet.CoreApi.Hosting;
internal sealed class NotificationWorker(IServiceScopeFactory scopes,ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested){
            try{await using var scope=scopes.CreateAsyncScope();await scope.ServiceProvider.GetRequiredService<INotificationQueueService>().ProcessNextAsync(stoppingToken);}
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            // Do not include SMTP exception text: it can contain recipients or credentials.
            catch(Exception){logger.LogWarning("Notification queue iteration failed; will retry.");}
            await Task.Delay(TimeSpan.FromSeconds(2),stoppingToken);
        }
    }
}
