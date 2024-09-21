using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using ADD_InternalEventBus.AbsDomain;
using Microsoft.Extensions.Logging;

namespace ADD_InternalEventBus.Switchable
{
    public class InMemoryEventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, Delegate>> _subscribers =
            new ConcurrentDictionary<Type, ConcurrentDictionary<int, Delegate>>();

        private readonly ILogger _logger;
        private bool _disposed;

        public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Subscribe<T>(Action<T> subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            CheckDisposed();
            var subscribersList = _subscribers.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<int, Delegate>());
            subscribersList[subscriber.GetHashCode()] = subscriber;
        }

        public void Subscribe<T>(Func<T, Task> subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            CheckDisposed();
            var subscribersList = _subscribers.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<int, Delegate>());
            subscribersList[subscriber.GetHashCode()] = subscriber;
        }

        public void Unsubscribe<T>(Action<T> subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            CheckDisposed();
            if (_subscribers.TryGetValue(typeof(T), out var subscribersList))
            {
                subscribersList.TryRemove(subscriber.GetHashCode(), out _);
            }
        }

        public void Unsubscribe<T>(Func<T, Task> subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
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
            return Task.CompletedTask;
        }

        public void Publish<T>(T eventMessage)
        {
            CheckDisposed();
            PublishInternal(eventMessage);
        }

        private void PublishInternal<T>(T eventMessage)
        {
            if (_subscribers.TryGetValue(typeof(T), out var subscribersList))
            {
                foreach (var subscriber in subscribersList.Values)
                {
                    switch (subscriber)
                    {
                        case Action<T> action:
                            var actionTask = HandleAction(action, eventMessage);
                            if (!actionTask.IsCompleted)
                            {
                                actionTask.ContinueWith(t =>
                                {
                                    if (t.Exception != null)
                                    {
                                        HandleException(t.Exception, typeof(T));
                                    }
                                }, TaskContinuationOptions.OnlyOnFaulted);
                            }

                            break;
                        case Func<T, Task> func:
                            var funcTask = HandleFunc(func, eventMessage);
                            if (!funcTask.IsCompleted)
                            {
                                funcTask.ContinueWith(t =>
                                {
                                    if (t.Exception != null)
                                    {
                                        HandleException(t.Exception, typeof(T));
                                    }
                                }, TaskContinuationOptions.OnlyOnFaulted);
                            }

                            break;
                    }
                }
            }
        }

        private Task HandleAction<T>(Action<T> action, T eventMessage)
        {
            try
            {
                action(eventMessage);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                HandleException(ex, typeof(T));
                return Task.CompletedTask;
            }
        }

        private Task HandleFunc<T>(Func<T, Task> func, T eventMessage)
        {
            try
            {
                Task task = func(eventMessage);

                if (task.IsCompletedSuccessfully)
                {
                    // Task completed synchronously, no need to do more
                    return Task.CompletedTask;
                }

                // If the task did not complete synchronously, attach a continuation
                // to handle any exceptions
                return task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        // Handle exceptions from the subscriber task
                        HandleException(t.Exception, typeof(T));
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
            catch (Exception ex)
            {
                // Handle exceptions thrown during the invocation of func
                HandleException(ex, typeof(T));
                return Task.CompletedTask;
            }
        }

        private void HandleException(Exception exception, Type eventType)
        {
            _logger.LogError(exception, $"An exception occurred while processing event of type {eventType.Name}.");
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
                throw new ObjectDisposedException(nameof(InMemoryEventBus));
        }
    }
}