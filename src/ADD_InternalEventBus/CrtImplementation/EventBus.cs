using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using ADD_InternalEventBus.AbsDomain;

namespace ADD_InternalEventBus.CrtImplementation
{
    public class EventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, WeakReference>> _subscribers = new ConcurrentDictionary<Type, ConcurrentDictionary<int, WeakReference>>();
        private bool _disposed;

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

            foreach (var (key, weakReference) in subscribersList)
            {
                if (weakReference.IsAlive)
                {
                    var subscriber = weakReference.Target;
                    if (subscriber != null)
                    {
                        _ = subscriber switch
                        {
                            Func<T, Task> asyncSubscriber =>
                                // Fire and forget with async subscriber
                                _ = asyncSubscriber(eventMessage),
                            Action<T> syncSubscriber =>
                                Task.Run(() =>
                                {
                                    syncSubscriber(eventMessage);
                                }),
                            _ => Task.CompletedTask
                        };
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

            foreach (var key in toRemove)
            {
                subscribersList.TryRemove(key, out _);
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
                throw new ObjectDisposedException(nameof(EventBus));
            }
        }
    }
}