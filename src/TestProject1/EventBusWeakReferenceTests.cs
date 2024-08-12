using System;
using System.Threading.Tasks;
using ADD_InternalEventBus.AbsDomain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace ADD_InternalEventBus.CrtImplementation.Tests
{
    public class EventBusWeakReferenceTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly IEventBus _weakRefEventBus;

        public EventBusWeakReferenceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            var serviceProvider = new ServiceCollection()
                .AddLogging(configure => configure.AddConsole())
                .AddSingleton<IEventBus, EventBus>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<EventBus>>();
                    return new EventBus(logger, new EventBusOptions
                    {
                        UseWeakReferences = true,
                        FireAndForget = true
                    });
                })
                .BuildServiceProvider();

            _weakRefEventBus = serviceProvider.GetRequiredService<IEventBus>();
        }

        [Fact]
        public async Task EventBus_Should_Handle_WeakReference_Subscriber()
        {
            // Arrange
            var receivedMessage = string.Empty;
            WeakReference weakReference;
            Subscriber? subscriber = null;

            // Create a weak reference to the subscriber within its own scope
            weakReference = CreateSubscriberAndSubscribe(out subscriber, message =>
                {
                    receivedMessage = message;
                }
            );

            // Act - Publish event while the subscriber is alive
            _testOutputHelper.WriteLine("Publishing event with active subscriber...");
            await _weakRefEventBus.PublishAsync("Hello, Weak Reference EventBus!");

            // sleep for a while to allow the subscriber to receive the message
            await Task.Delay(1000);
            
            // Verify that the subscriber received the message
            Assert.Equal("Hello, Weak Reference EventBus!", receivedMessage);

            // Explicitly nullify the subscriber variable
            subscriber = null;

            GC.Collect();

            // Act - Publish event after the subscriber should be collected
            receivedMessage = string.Empty; // Reset received message
            _testOutputHelper.WriteLine("Publishing event after subscriber should be collected...");
            await _weakRefEventBus.PublishAsync("This should not be received");

            // Assert that the weak reference target is null (indicating the object has been collected)
            Assert.Null(weakReference.Target);

            // Assert that no message was received (since the subscriber should be collected)
            Assert.Equal(string.Empty, receivedMessage);
        }
        
        [Fact]
        public async Task EventBus_Should_Handle_WeakReference_Subscriber_2()
        {
            // Arrange
            var receivedMessage = string.Empty;
            WeakReference weakReference;
            Subscriber? subscriber = null;

            // Create a weak reference to the subscriber within its own scope
            using (subscriber = new Subscriber(_testOutputHelper, message => receivedMessage = message))
            {
                weakReference = new WeakReference(subscriber);
                _weakRefEventBus.Subscribe<string>(subscriber.HandleEvent);
                // Act - Publish event while the subscriber is alive
                _testOutputHelper.WriteLine("Publishing event with active subscriber...");
                await _weakRefEventBus.PublishAsync("Hello, Weak Reference EventBus!");
                // sleep for a while to allow the subscriber to receive the message
                await Task.Delay(1000);
            }
            
            // Verify that the subscriber received the message
            Assert.Equal("Hello, Weak Reference EventBus!", receivedMessage);

            // Explicitly nullify the subscriber variable
            subscriber = null;

            GC.Collect();

            // Act - Publish event after the subscriber should be collected
            receivedMessage = string.Empty; // Reset received message
            _testOutputHelper.WriteLine("Publishing event after subscriber should be collected...");
            await _weakRefEventBus.PublishAsync("This should not be received");

            // Assert that the weak reference target is null (indicating the object has been collected)
            Assert.Null(weakReference.Target);

            // Assert that no message was received (since the subscriber should be collected)
            Assert.Equal(string.Empty, receivedMessage);
        }

        private WeakReference CreateSubscriberAndSubscribe(out Subscriber subscriber, Action<string> handleMessage)
        {
            subscriber = new Subscriber(_testOutputHelper, handleMessage);
            var weakReference = new WeakReference(subscriber);
            _weakRefEventBus.Subscribe<string>(subscriber.HandleEvent);
            return weakReference;
        }

        private class Subscriber : IDisposable
        {
            private readonly ITestOutputHelper _testOutputHelper;
            private readonly Action<string> _onMessage;
            private bool _disposed;

            public Subscriber(ITestOutputHelper testOutputHelper, Action<string> onMessage)
            {
                _testOutputHelper = testOutputHelper;
                _onMessage = onMessage;
            }

            public void HandleEvent(string message)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(Subscriber));
                _testOutputHelper.WriteLine($"Subscriber received message: {message}");
                _onMessage(message);
            }

            public void Dispose()
            {
                _disposed = true;
                _testOutputHelper.WriteLine("Subscriber disposed.");
            }

            ~Subscriber()
            {
                _testOutputHelper.WriteLine("Subscriber finalized.");
            }
        }
    }
}