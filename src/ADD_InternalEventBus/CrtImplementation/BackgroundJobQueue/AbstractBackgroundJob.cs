using System.Threading;
using System.Threading.Tasks;

namespace ADD_InternalEventBus.CrtImplementation.BackgroundJobQueue
{
    public abstract class AbstractBackgroundJob : IBackgroundJob
    {
        protected AbstractBackgroundJob(string jobName)
        {
            JobName = jobName;
        }

        public IBackgroundJob.OnStartDelegate? OnStart { get; set; }

        public IBackgroundJob.OnCompleteDelegate? OnComplete { get; set; }
        public string JobName { get; set; }

        public virtual async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            OnStart?.Invoke(this);
            await ExecuteJobAsync(stoppingToken);
            OnComplete?.Invoke(this);
        }

        protected abstract Task ExecuteJobAsync(CancellationToken stoppingToken);
    }
}