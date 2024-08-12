using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ADD_InternalEventBus.CrtImplementation.BackgroundJobQueue
{
    public static class BackgroundJobManager
    {
        private static BackgroundJobService _jobService;

        public static BackgroundJobService JobService
        {
            // get only, create job service if it doesn't exist
            get
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var logger = loggerFactory.CreateLogger<BackgroundJobService>();
                
                // create service provider
                var serviceProvider = new ServiceCollection()
                    .AddLogging()
                    .BuildServiceProvider();
                
                return _jobService ??= new BackgroundJobService(serviceProvider, logger);
            }
            set => _jobService = value;
        }

        public static int MaxRunningJobs { get; set; } = 128;

        /// <summary>
        /// If you don't want to wait for the job to complete, use this method
        /// </summary>
        public static void EnqueueJob(string jobName, Func<IServiceProvider, CancellationToken, Task> job)
        {
            JobService.EnqueueJob(jobName, job);
        }

        /// <summary>
        /// If you want to wait for the job to complete, use this method
        /// </summary>
        public static Task EnqueueJobAndGetResult(string jobName, Func<IServiceProvider, CancellationToken, Task> job)
        {
            return JobService.EnqueueJobAsync(jobName, job);
        }
    }
}