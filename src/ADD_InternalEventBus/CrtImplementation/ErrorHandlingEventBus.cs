using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using ADD_InternalEventBus.AbsDomain;

namespace ADD_InternalEventBus.CrtImplementation
{
    public class ErrorHandlingEventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, WeakReference>> _subscribers = new ConcurrentDictionary<Type, ConcurrentDictionary<int, WeakReference>>();
        private readonly Action<Exception>? _defaultLogException;
        private bool _disposed;

        public ErrorHandlingEventBus(Action<Exception>? defaultLogException = null)
        {
            _defaultLogException = defaultLogException;
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
            await PublishInternalAsync(eventMessage, _defaultLogException);
        }

        public async Task PublishAsync<T>(T eventMessage, Action<Exception>? logException)
        {
            CheckDisposed();
            await PublishInternalAsync(eventMessage, logException ?? _defaultLogException);
        }

        public void Publish<T>(T eventMessage)
        {
            CheckDisposed();
            PublishInternal(eventMessage, _defaultLogException);
        }

        public void Publish<T>(T eventMessage, Action<Exception>? logException)
        {
            CheckDisposed();
            PublishInternal(eventMessage, logException ?? _defaultLogException);
        }

        private async Task PublishInternalAsync<T>(T eventMessage, Action<Exception>? logException)
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
                        switch (subscriber)
                        {
                            case Func<T, Task> asyncSubscriber:
                                await Task.Run(async () =>
                                {
                                    try
                                    {
                                        await asyncSubscriber(eventMessage);
                                    }
                                    catch (Exception ex)
                                    {
                                        logException?.Invoke(ex);
                                    }
                                });
                                break;

                            case Action<T> syncSubscriber:
                                await Task.Run(() =>
                                {
                                    try
                                    {
                                        syncSubscriber(eventMessage);
                                    }
                                    catch (Exception ex)
                                    {
                                        logException?.Invoke(ex);
                                    }
                                });
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

            foreach (var key in toRemove)
            {
                subscribersList.TryRemove(key, out _);
            }
        }

        private void PublishInternal<T>(T eventMessage, Action<Exception>? logException)
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
                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        await asyncSubscriber(eventMessage);
                                    }
                                    catch (Exception ex)
                                    {
                                        logException?.Invoke(ex);
                                    }
                                }),

                            Action<T> syncSubscriber =>
                                Task.Run(() =>
                                {
                                    try
                                    {
                                        syncSubscriber(eventMessage);
                                    }
                                    catch (Exception ex)
                                    {
                                        logException?.Invoke(ex);
                                    }
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
                throw new ObjectDisposedException(nameof(ErrorHandlingEventBus));
            }
        }
    }
}