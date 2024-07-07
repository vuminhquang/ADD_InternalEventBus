using Xunit.Abstractions;

namespace ADD_InternalEventBus.CrtImplementation.Tests
{
    public class EventBusTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly EventBus _eventBus;

        public EventBusTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _eventBus = new EventBus();
        }

        [Fact]
        public async Task EventBus_Should_Handle_Synchronous_Subscriber()
        {
            // Arrange
            string receivedMessage = null;
            _eventBus.Subscribe<string>(message =>
            {
                _testOutputHelper.WriteLine($"Sync received message: {message}");
                receivedMessage = message;
            });
            
            // Act
            _testOutputHelper.WriteLine("Publishing sync event...");
            await _eventBus.PublishAsync("Hello, Sync EventBus!");
            
            await Task.Delay(1000);

            // Assert
            Assert.Equal("Hello, Sync EventBus!", receivedMessage);
        }

        [Fact]
        public async Task EventBus_Should_Handle_Asynchronous_Subscriber()
        {
            // Arrange
            string receivedMessage = null;
            _eventBus.Subscribe<string>(async message =>
            {
                _testOutputHelper.WriteLine($"Async received message: {message}");
                await Task.Delay(100); // Simulate async work
                receivedMessage = message;
            });
            
            // Act
            _testOutputHelper.WriteLine("Publishing async event...");
            await _eventBus.PublishAsync("Hello, Async EventBus!");
            
            await Task.Delay(1000);

            // Assert
            Assert.Equal("Hello, Async EventBus!", receivedMessage);
        }

        [Fact]
        public async Task EventBus_Should_Handle_Both_Synchronous_And_Asynchronous_Subscribers()
        {
            // Arrange
            string syncReceivedMessage = null;
            string asyncReceivedMessage = null;

            _eventBus.Subscribe<string>(message =>
            {
                _testOutputHelper.WriteLine($"Sync received message: {message}");
                syncReceivedMessage = message;
            });
            _eventBus.Subscribe<string>(async message =>
            {
                _testOutputHelper.WriteLine($"Async received message: {message}");
                await Task.Delay(100); // Simulate async work
                asyncReceivedMessage = message;
            });

            // Act
            _testOutputHelper.WriteLine("Publishing mixed event...");
            await _eventBus.PublishAsync("Hello, Mixed EventBus!");
            
            await Task.Delay(1000);
            
            // Assert
            Assert.Equal("Hello, Mixed EventBus!", syncReceivedMessage);
            Assert.Equal("Hello, Mixed EventBus!", asyncReceivedMessage);
        }
    }
}