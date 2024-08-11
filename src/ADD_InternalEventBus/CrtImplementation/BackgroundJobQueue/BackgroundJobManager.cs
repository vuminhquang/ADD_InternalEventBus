using System;
using System.Threading;
using System.Threading.Tasks;

namespace ADD_InternalEventBus.CrtImplementation.BackgroundJobQueue
{
    public static class BackgroundJobManager
    {
        public static BackgroundJobService? JobService { get; set; }
        public static int MaxRunningJobs { get; set; } = 8;

        /// <summary>
        /// If you don't want to wait for the job to complete, use this method
        /// </summary>
        public static void EnqueueJob(string jobName, Func<IServiceProvider, CancellationToken, Task> job)
        {
            JobService?.EnqueueJob(jobName, job);
        }

        /// <summary>
        /// If you want to wait for the job to complete, use this method
        /// </summary>
        public static Task EnqueueJobAndGetResult(string jobName, Func<IServiceProvider, CancellationToken, Task> job)
        {
            return JobService == null ? Task.CompletedTask : JobService.EnqueueJobAsync(jobName, job);
        }
    }
}