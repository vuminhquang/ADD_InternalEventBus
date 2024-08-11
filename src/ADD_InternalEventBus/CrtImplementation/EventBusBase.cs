using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using ADD_InternalEventBus.AbsDomain;
using ADD_InternalEventBus.CrtImplementation.BackgroundJobQueue;
using Microsoft.Extensions.Logging;

namespace ADD_InternalEventBus.CrtImplementation
{
    public abstract class EventBusBase : IEventBus
    {
        protected readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, object>> _subscribers;
        protected readonly ILogger _logger;
        protected readonly bool _fireAndForget;
        protected bool _disposed;

        protected EventBusBase(ILogger<EventBus> logger, EventBusOptions? options = null)
        {
            _logger = logger;
            _fireAndForget = options?.FireAndForget ?? true;
            _subscribers = new ConcurrentDictionary<Type, ConcurrentDictionary<int, object>>();
        }

        public virtual void Subscribe<T>(Action<T> subscriber)
        {
            CheckDisposed();
            var subscribersList = _subscribers.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<int, object>());
            subscribersList[subscriber.GetHashCode()] = subscriber;
        }

        public virtual void Subscribe<T>(Func<T, Task> subscriber)
        {
            CheckDisposed();
            var subscribersList = _subscribers.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<int, object>());
            subscribersList[subscriber.GetHashCode()] = subscriber;
        }

        public virtual void Unsubscribe<T>(Action<T> subscriber)
        {
            CheckDisposed();
            if (_subscribers.TryGetValue(typeof(T), out var subscribersList))
            {
                subscribersList.TryRemove(subscriber.GetHashCode(), out _);
            }
        }

        public virtual void Unsubscribe<T>(Func<T, Task> subscriber)
        {
            CheckDisposed();
            if (_subscribers.TryGetValue(typeof(T), out var subscribersList))
            {
                subscribersList.TryRemove(subscriber.GetHashCode(), out _);
            }
        }

        public Task PublishAsync<T>(T eventMessage)
        {
            //CheckDisposed();
            PublishInternal(eventMessage);
            return Task.CompletedTask;
        }

        public void Publish<T>(T eventMessage)
        {
            CheckDisposed();
            PublishInternal(eventMessage);
        }

        protected abstract void PublishInternal<T>(T eventMessage);

        protected Task HandleSubscriber(object subscriber, object eventMessage)
        {
            return subscriber switch
            {
                Func<object, Task> asyncSubscriber => HandleAsyncSubscriber(asyncSubscriber, eventMessage),
                Action<object> syncSubscriber => HandleSyncSubscriber(syncSubscriber, eventMessage),
                _ => Task.CompletedTask
            };
        }

        private Task HandleAsyncSubscriber(Func<object, Task> asyncSubscriber, object eventMessage)
        {
            BackgroundJobManager.EnqueueJob("", (provider, token) =>
            {
                try
                {
                    return asyncSubscriber(eventMessage);
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                    return Task.CompletedTask;
                }
            });
            return Task.CompletedTask;
        }

        private Task HandleSyncSubscriber(Action<object> syncSubscriber, object eventMessage)
        {
            BackgroundJobManager.EnqueueJob("", (provider, token) =>
            {
                try
                {
                    syncSubscriber(eventMessage);
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                    return Task.CompletedTask;
                }
            });
            return Task.CompletedTask;
        }

        protected void HandleException(Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogError(exception, "An exception occurred while processing an event.");
            }
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
                // Clear all subscribers
                foreach (var subscribersList in _subscribers.Values)
                {
                    subscribersList.Clear();
                }

                _subscribers.Clear();
            }

            _disposed = true;
        }

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("EventBus");
            }
        }
    }
}