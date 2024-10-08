using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using ADD_InternalEventBus.AbsDomain;
using Microsoft.Extensions.Logging;

namespace ADD_InternalEventBus.CrtImplementation
{
    public class WeakRefEventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, WeakReference>> _subscribers = new ConcurrentDictionary<Type, ConcurrentDictionary<int, WeakReference>>();
        private bool _disposed;
        private readonly ILogger _logger;
        private readonly bool _fireAndForget;

        public WeakRefEventBus(ILogger<EventBus> logger, EventBusOptions? options = null)
        {
            _logger = logger;
            _fireAndForget = options?.FireAndForget ?? true;
        }

        public void Subscribe<T>(Action<T> subscriber)
        {
            CheckDisposed();
            var subscribersList = _subscribers.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<int, WeakReference>());
            subscribersList[subscriber.GetHashCode()] = new WeakReference(subscriber);
        }

        public void Subscribe<T>(Func<T, Task> subscriber)
        {
            CheckDisposed();
            var subscribersList = _subscribers.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<int, WeakReference>());
            subscribersList[subscriber.GetHashCode()] = new WeakReference(subscriber);
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

        public async Task PublishAsync<T>(T eventMessage)
        {
            CheckDisposed();
            PublishInternal(eventMessage);
            // Awaiting a completed task to keep the method signature async
            await Task.CompletedTask;
        }

        public void Publish<T>(T eventMessage)
        {
            CheckDisposed();
            PublishInternal(eventMessage);
        }

        private void PublishInternal<T>(T eventMessage)
        {
            if (!_subscribers.TryGetValue(typeof(T), out var subscribersList)) return;

            var toRemove = new List<int>();

            if (_fireAndForget)
            {
                foreach (var (key, weakReference) in subscribersList)
                {
                    if (weakReference.IsAlive)
                    {
                        var subscriber = weakReference.Target;
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
                        else
                        {
                            toRemove.Add(key);
                        }
                    }
                    else
                    {
                        toRemove.Add(key);
                    }
                }
            }
            else
            {
                // Run each subscriber one by one, synchronously
                foreach (var (key, weakReference) in subscribersList)
                {
                    if (weakReference.IsAlive)
                    {
                        var subscriber = weakReference.Target;
                        if (subscriber != null)
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
                        else
                        {
                            toRemove.Add(key);
                        }
                    }
                    else
                    {
                        toRemove.Add(key);
                    }
                }
            }

            foreach (var key in toRemove)
            {
                subscribersList.TryRemove(key, out _);
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
        
        private void HandleException(Exception? exception)
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