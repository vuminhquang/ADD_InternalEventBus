using System;
using System.Threading;
using System.Threading.Tasks;

namespace ADD_InternalEventBus.CrtImplementation.BackgroundJobQueue
{
    public class GenericBackgroundJob : AbstractBackgroundJob
    {
        private readonly IServiceProvider _serviceProvider;

        // The generic job receive a function to execute
        public GenericBackgroundJob(string jobName, IServiceProvider serviceProvider, Func<IServiceProvider, CancellationToken, Task> job) : base(jobName)
        {
            _serviceProvider = serviceProvider;
            Job = job;
        }

        private Func<IServiceProvider, CancellationToken,Task> Job { get; }

        protected override Task ExecuteJobAsync(CancellationToken stoppingToken)
        {
            return Job(_serviceProvider, stoppingToken);
        }
    }
}