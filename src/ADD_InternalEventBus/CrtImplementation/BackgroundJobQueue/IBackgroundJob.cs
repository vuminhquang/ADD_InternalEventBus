using System.Threading;
using System.Threading.Tasks;

namespace ADD_InternalEventBus.CrtImplementation.BackgroundJobQueue
{
    public interface IBackgroundJob
    {
        // Execute the job
        Task ExecuteAsync(CancellationToken stoppingToken);

        delegate void OnStartDelegate(IBackgroundJob backgroundJob);

        OnStartDelegate? OnStart { get; set; }

        delegate void OnCompleteDelegate(IBackgroundJob backgroundJob);
    
        OnCompleteDelegate? OnComplete { get; set; }
        string JobName { get; set; }
    }
}