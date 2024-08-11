using System;
using System.Threading.Tasks;
using ADD_InternalEventBus.AbsDomain;
using ADD_InternalEventBus.CrtImplementation.BackgroundJobQueue;
using Microsoft.Extensions.Logging;

namespace ADD_InternalEventBus.CrtImplementation
{
    public class EventBus : IEventBus
    {
        private readonly IEventBus _eventBus;
        private bool _disposed;

        public EventBus(ILogger<EventBus> logger, EventBusOptions? options = null)
        {
            _eventBus = options is { UseWeakReferences: true }
                ? (IEventBus)new WeakRefEventBus(logger, options)
                : new StrongRefEventBus(logger, options);
        }

        public void Subscribe<T>(Action<T> subscriber)
        {
            CheckDisposed();
            _eventBus.Subscribe(subscriber);
        }

        public void Subscribe<T>(Func<T, Task> subscriber)
        {
            CheckDisposed();
            _eventBus.Subscribe(subscriber);
        }

        public void Unsubscribe<T>(Action<T> subscriber)
        {
            CheckDisposed();
            _eventBus.Unsubscribe(subscriber);
        }

        public void Unsubscribe<T>(Func<T, Task> subscriber)
        {
            CheckDisposed();
            _eventBus.Unsubscribe(subscriber);
        }

        public async Task PublishAsync<T>(T eventMessage)
        {
            CheckDisposed();
            await _eventBus.PublishAsync(eventMessage);
        }

        public void Publish<T>(T eventMessage)
        {
            CheckDisposed();
            _eventBus.Publish(eventMessage);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _eventBus.Dispose();
            }

            _disposed = true;
        }

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(EventBus));
            }
        }
    }

    public class EventBusOptions
    {
        public bool UseWeakReferences { get; set; }
        public bool FireAndForget { get; set; }
    }
}