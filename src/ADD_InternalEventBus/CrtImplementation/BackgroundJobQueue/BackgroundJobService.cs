using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ADD_InternalEventBus.CrtImplementation.BackgroundJobQueue
{
    public class BackgroundJobService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundJobService> _logger;
        
        // Concurrent queue to enqueue the tuples of jobs with TaskCompletionSource to wait for running
        private readonly ConcurrentQueue<(IBackgroundJob, TaskCompletionSource<object>)> _jobs =
            new ConcurrentQueue<(IBackgroundJob, TaskCompletionSource<object>)>();
        
        // Concurrent HashSet  to record running jobs
        private readonly ConcurrentDictionary <IBackgroundJob, byte> _runningJobs = new ConcurrentDictionary <IBackgroundJob, byte>();

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        
        public BackgroundJobService(IServiceProvider serviceProvider, ILogger<BackgroundJobService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }
        
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // intentionally left blank
            return Task.CompletedTask;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            BackgroundJobManager.JobService = this;
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("BackgroundJobService - Exiting");
            Cancel();
            return Task.CompletedTask;
        }
        
        private Task EnqueueJob(IBackgroundJob backgroundJob)
        {
            var tcs = new TaskCompletionSource<object>();
            
            // enqueue the job to the queue
            _jobs.Enqueue((backgroundJob, tcs));
            
            // release the job to run
            RunJobs(_cancellationTokenSource.Token);
            
            return tcs.Task;
        }

        private void RunJobs(CancellationToken token)
        {
            // check if Cts is canceled
            if (token.IsCancellationRequested)
            {
                return;
            }
            
            // check if there is any job in the queue
            if (_jobs.IsEmpty)
            {
                return;
            }
            
            // check if the current running jobs is more than limit
            if (_runningJobs.Count >= BackgroundJobManager.MaxRunningJobs)
            {
                //Console.WriteLine("Running jobs is more than limit");
                return;
            }
            
            // dequeue the job from the queue
            if (!_jobs.TryDequeue(out var job))
            {
                return;
            }
            
            // add the job to the running jobs
            _runningJobs.TryAdd(job.Item1, 1);
            
            // execute the job
            ExecuteJob(job.Item1, job.Item2);
        }

        private void ExecuteJob(IBackgroundJob backgroundJob, TaskCompletionSource<object> tcs)
        {
            ThreadPool.QueueUserWorkItem(CallBack);
            return;

            async void CallBack(object? state)
            {
                try
                {
                    await backgroundJob.ExecuteAsync(_cancellationTokenSource.Token);
                    // _logger.LogInformation($"BackgroundJobService - Job {backgroundJob.JobName} completed");
                    // Console.WriteLine($"BackgroundJobService - Job {backgroundJob.JobName} completed");
                    tcs.SetResult(Task.CompletedTask); // Task completed successfully
                }
                catch (Exception ex)
                {
                    _logger.LogError($"BackgroundJobService - Job {backgroundJob.JobName} failed, exception: {ex.Message} - {ex.StackTrace}");
                    Console.WriteLine($"BackgroundJobService - Job {backgroundJob.JobName} failed, exception: {ex.Message} - {ex.StackTrace}");
                    tcs.SetException(ex); // Task completed with an exception
                }
                finally
                {
                    // dequeue the job from the running jobs
                    _runningJobs.TryRemove(backgroundJob, out _);
                    
                    // release the job to run
                    RunJobs(_cancellationTokenSource.Token);
                }
            }
        }

        /// <summary>
        /// If you don't want to wait for the job to complete, use this method
        /// </summary>
        /// <param name="jobName"></param>
        /// <param name="job"></param>
        public void EnqueueJob(string jobName, Func<IServiceProvider, CancellationToken, Task> job)
        {
            EnqueueJob(new GenericBackgroundJob(jobName, _serviceProvider, job));
        }
        
        /// <summary>
        /// If you want to wait for the job to complete, use this method
        /// </summary>
        /// <param name="jobName"></param>
        /// <param name="job"></param>
        /// <returns></returns>
        public Task EnqueueJobAsync(string jobName, Func<IServiceProvider, CancellationToken, Task> job)
        {
            return EnqueueJob(new GenericBackgroundJob(jobName, _serviceProvider, job));
        }
        
        private void Cancel()
        {
            // check if Cts is canceled
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }
            
            _logger.LogInformation("BackgroundJobService - Exiting");
            
            _cancellationTokenSource.Cancel();
        }
    }
}