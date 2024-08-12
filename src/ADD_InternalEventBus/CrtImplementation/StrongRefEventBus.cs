using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ADD_InternalEventBus.AbsDomain;
using ADD_InternalEventBus.CrtImplementation.BackgroundJobQueue;
using Microsoft.Extensions.Logging;

namespace ADD_InternalEventBus.CrtImplementation
{
    public class StrongRefEventBus : IEventBus, IDisposable
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, object>> _subscribers = new ConcurrentDictionary<Type, ConcurrentDictionary<int, object>>();
        private bool _disposed;
        private readonly ILogger _logger;
        private readonly bool _fireAndForget;

        public StrongRefEventBus(ILogger<EventBus> logger, EventBusOptions? options = null)
        {
            _logger = logger;
            _fireAndForget = options?.FireAndForget ?? true;
        }

        public void Subscribe<T>(Action<T> subscriber)
        {
            CheckDisposed();
            var subscribersList = _subscribers.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<int, object>());
            subscribersList[subscriber.GetHashCode()] = subscriber;
        }

        public void Subscribe<T>(Func<T, Task> subscriber)
        {
            CheckDisposed();
            var subscribersList = _subscribers.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<int, object>());
            subscribersList[subscriber.GetHashCode()] = subscriber;
        }

        public void Unsubscribe<T>(Action<T> subscriber)
        {
            CheckDisposed();
            if (_subscribers.TryGetValue(typeof(T), out var subscribersList))
            {
                subscribersList.TryRemove(subscriber.GetHashCode(), out _);
            }
        }

        public void Unsubscribe<T>(Func<T, Task> subscriber)
        {
            CheckDisposed();
            if (_subscribers.TryGetValue(typeof(T), out var subscribersList))
            {
                subscribersList.TryRemove(subscriber.GetHashCode(), out _);
            }
        }

        public Task PublishAsync<T>(T eventMessage)
        {
            CheckDisposed();
            PublishInternal(eventMessage);
            // Awaiting a completed task to keep the method signature async
            return Task.CompletedTask;
        }

        public void Publish<T>(T eventMessage)
        {
            CheckDisposed();
            PublishInternal(eventMessage);
        }

        private void PublishInternal<T>(T eventMessage)
        {
            if (!_subscribers.TryGetValue(typeof(T), out var subscribersList)) return;

            if (_fireAndForget)
            {
                foreach (var (key, subscriber) in subscribersList)
                {
                    if (subscriber != null)
                    {
                        switch (subscriber)
                        {
                            case Func<T, Task> asyncSubscriber:
                                HandleFunc(asyncSubscriber, eventMessage);
                                break;
                            case Action<T> syncSubscriber:
                                HandleAction(syncSubscriber, eventMessage);
                                break;
                            default:
                                _ = Task.CompletedTask;
                                break;
                        }
                    }
                }
            }
            else
            {
                // Run each subscriber one by one, synchronously
                foreach (var (key, subscriber) in subscribersList)
                {
                    try
                    {
                        switch (subscriber)
                        {
                            case Func<T, Task> asyncSubscriber:
                                asyncSubscriber(eventMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                                break;
                            case Action<T> syncSubscriber:
                                syncSubscriber(eventMessage);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        HandleException(ex);
                    }
                }
            }
        }

        private async void HandleAction<T>(Action<T> action, T eventMessage)
        {
            await CallBackAsync();
            return;

            Task CallBackAsync()
            {
                try
                {
                    action(eventMessage);
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }
                return Task.CompletedTask;
            }
        }
        
        private async void HandleFunc<T>(Func<T, Task> func, T eventMessage)
        {
            try
            {
                await func(eventMessage);
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }
        
        private void HandleException(Exception exception)
        {
            _logger.LogError(exception, "An exception occurred while processing an event.");
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