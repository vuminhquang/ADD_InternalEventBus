using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ADD_InternalEventBus.CrtImplementation
{
    public class EventBus
    {
        private readonly Dictionary<Type, List<WeakReference>> _subscribers = new Dictionary<Type, List<WeakReference>>();

        public void Subscribe<T>(Action<T> subscriber)
        {
            if (!_subscribers.ContainsKey(typeof(T)))
            {
                _subscribers[typeof(T)] = new List<WeakReference>();
            }

            _subscribers[typeof(T)].Add(new WeakReference(subscriber));
        }

        public void Subscribe<T>(Func<T, Task> subscriber)
        {
            if (!_subscribers.ContainsKey(typeof(T)))
            {
                _subscribers[typeof(T)] = new List<WeakReference>();
            }

            _subscribers[typeof(T)].Add(new WeakReference(subscriber));
        }

        public void Unsubscribe<T>(Action<T> subscriber)
        {
            if (_subscribers.ContainsKey(typeof(T)))
            {
                _subscribers[typeof(T)].RemoveAll(wr => wr.Target is Action<T> target && target == subscriber);
            }
        }

        public void Unsubscribe<T>(Func<T, Task> subscriber)
        {
            if (_subscribers.ContainsKey(typeof(T)))
            {
                _subscribers[typeof(T)].RemoveAll(wr => wr.Target is Func<T, Task> target && target == subscriber);
            }
        }

        public async Task PublishAsync<T>(T eventMessage)
        {
            if (_subscribers.ContainsKey(typeof(T)))
            {
                var tasks = new List<Task>();
                var toRemove = new List<WeakReference>();

                foreach (var weakReference in _subscribers[typeof(T)])
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
                                        tasks.Add(asyncSubscriber(eventMessage));
                                        break;
                                    case Action<T> syncSubscriber:
                                        syncSubscriber(eventMessage);
                                        break;
                                }
                            }
                            catch (ObjectDisposedException)
                            {
                                // Handle or log the ObjectDisposedException if necessary
                                toRemove.Add(weakReference);
                            }
                        }
                        else
                        {
                            toRemove.Add(weakReference);
                        }
                    }
                    else
                    {
                        toRemove.Add(weakReference);
                    }
                }

                // Remove dead weak references
                foreach (var weakReference in toRemove)
                {
                    _subscribers[typeof(T)].Remove(weakReference);
                }

                await Task.WhenAll(tasks);
            }
        }
    }
}