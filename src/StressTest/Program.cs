using System.Diagnostics;
using ADD_InternalEventBus.AbsDomain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ADD_InternalEventBus.CrtImplementation.Tests
{
    public class Program
    {
        private static IEventBus _eventBus;
        private static readonly int _numThreads = Environment.ProcessorCount * 2; // Adjust based on the number of logical processors
        private static readonly int _numIterations = 1000000;
        // private static readonly int _numIterations = 10;

        static Program()
        {
            var serviceProvider = new ServiceCollection()
                .AddLogging(configure => configure.AddConsole())
                .AddSingleton<EventBus>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<EventBus>>();
                    return new EventBus(logger, new EventBusOptions
                    {
                        UseWeakReferences = false,
                        FireAndForget = true
                    });
                })
                .AddSingleton<WeakRefEventBus>()
                .AddSingleton<StrongRefEventBus>()
                // .AddSingleton<EventBus2>( serviceProvider =>
                // {
                //     var logger = serviceProvider.GetRequiredService<ILogger<EventBus2>>();
                //     return new EventBus2(logger, 8);
                // })
                .BuildServiceProvider();

            _eventBus = serviceProvider.GetRequiredService<EventBus>();
        }
        
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting stress test...");

            var stopwatch = Stopwatch.StartNew();

            // Parallel subscribing/unsubscribing tasks
            Parallel.For(0, _numThreads, i => SubscribeUnsubscribeTest(i));

            // Parallel publishing tasks
            Parallel.For(0, _numThreads, async i => await PublishTest(i));

            stopwatch.Stop();

            Console.WriteLine($"Stress test completed in {stopwatch.ElapsedMilliseconds} ms.");
        }

        private static void SubscribeUnsubscribeTest(int threadId)
        {
            for (int i = 0; i < _numIterations; i++)
            {
                Action<string> subscriber = message =>
                {
                    // Console.WriteLine($"Thread {threadId} received: {message}");
                    // throw new Exception("This should not be thrown.");
                };
                _eventBus.Subscribe(subscriber);
                // _eventBus.Unsubscribe(subscriber);

                Func<string, Task> asyncSubscriber = async message =>
                {
                    // await Task.Delay(10);
                    // Console.WriteLine($"Thread {threadId} received async: {message}");
                    // throw new Exception("This should not be thrown.");
                };
                _eventBus.Subscribe(asyncSubscriber);
                // _eventBus.Unsubscribe(asyncSubscriber);
            }
        }

        private static async Task PublishTest(int threadId)
        {
            for (int i = 0; i < _numIterations; i++)
            {
                await _eventBus.PublishAsync($"Message from thread {threadId} - {i}");
            }
        }
    }
}