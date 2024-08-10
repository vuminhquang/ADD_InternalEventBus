using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace ADD_InternalEventBus.CrtImplementation.Tests
{
    public class WeakRefEventBusTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly WeakRefEventBus _weakRefEventBus;

        public WeakRefEventBusTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            var serviceProvider = new ServiceCollection()
                .AddLogging(configure => configure.AddConsole())
                .AddSingleton<WeakRefEventBus>()
                .BuildServiceProvider();

            _weakRefEventBus = serviceProvider.GetRequiredService<WeakRefEventBus>();
        }

        [Fact]
        public async Task EventBus_Should_Handle_Synchronous_Subscriber()
        {
            // Arrange
            string receivedMessage = null;
            _weakRefEventBus.Subscribe<string>(message =>
            {
                _testOutputHelper.WriteLine($"Sync received message: {message}");
                receivedMessage = message;
            });
            
            // Act
            _testOutputHelper.WriteLine("Publishing sync event...");
            await _weakRefEventBus.PublishAsync("Hello, Sync EventBus!");
            
            await Task.Delay(1000);

            // Assert
            Assert.Equal("Hello, Sync EventBus!", receivedMessage);
        }

        [Fact]
        public async Task EventBus_Should_Handle_Asynchronous_Subscriber()
        {
            // Arrange
            string receivedMessage = null;
            var tcs = new TaskCompletionSource<string>();

            _weakRefEventBus.Subscribe<string>(async message =>
            {
                _testOutputHelper.WriteLine($"Async received message: {message}");
                await Task.Delay(100); // Simulate async work
                receivedMessage = message;
                tcs.SetResult(message); // Signal that the message has been received
            });
    
            // Act
            _testOutputHelper.WriteLine("Publishing async event...");
            await _weakRefEventBus.PublishAsync("Hello, Async EventBus!");

            // Wait for the message to be received
            var received = await tcs.Task;

            // Assert
            Assert.Equal("Hello, Async EventBus!", received);
        }

        [Fact]
        public async Task EventBus_Should_Handle_Both_Synchronous_And_Asynchronous_Subscribers()
        {
            // Arrange
            string syncReceivedMessage = null;
            string asyncReceivedMessage = null;

            _weakRefEventBus.Subscribe<string>(message =>
            {
                _testOutputHelper.WriteLine($"Sync received message: {message}");
                syncReceivedMessage = message;
            });
            _weakRefEventBus.Subscribe<string>(async message =>
            {
                _testOutputHelper.WriteLine($"Async received message: {message}");
                await Task.Delay(100); // Simulate async work
                asyncReceivedMessage = message;
            });

            // Act
            _testOutputHelper.WriteLine("Publishing mixed event...");
            await _weakRefEventBus.PublishAsync("Hello, Mixed EventBus!");
            
            await Task.Delay(1000);
            
            // Assert
            Assert.Equal("Hello, Mixed EventBus!", syncReceivedMessage);
            Assert.Equal("Hello, Mixed EventBus!", asyncReceivedMessage);
        }
    }
}