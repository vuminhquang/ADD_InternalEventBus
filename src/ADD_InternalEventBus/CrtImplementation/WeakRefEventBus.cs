using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using ADD_InternalEventBus.AbsDomain;
using ADD_InternalEventBus.CrtImplementation.BackgroundJobQueue;
using Microsoft.Extensions.Logging;

namespace ADD_InternalEventBus.CrtImplementation
{
    public class WeakRefEventBus : EventBusBase
{
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, WeakReference>> _weakSubscribers;

    public WeakRefEventBus(ILogger<EventBus> logger, EventBusOptions? options = null) 
        : base(logger, options)
    {
        _weakSubscribers = new ConcurrentDictionary<Type, ConcurrentDictionary<int, WeakReference>>();
    }

    protected override void PublishInternal<T>(T eventMessage)
    {
        if (!_weakSubscribers.TryGetValue(typeof(T), out var subscribersList)) return;

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
                        HandleSubscriber(subscriber, eventMessage);
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

    public override void Subscribe<T>(Action<T> subscriber)
    {
        base.Subscribe<T>(subscriber);
        var weakSubscribersList = _weakSubscribers.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<int, WeakReference>());
        weakSubscribersList[subscriber.GetHashCode()] = new WeakReference(subscriber);
    }

    public override void Subscribe<T>(Func<T, Task> subscriber)
    {
        base.Subscribe<T>(subscriber);
        var weakSubscribersList = _weakSubscribers.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<int, WeakReference>());
        weakSubscribersList[subscriber.GetHashCode()] = new WeakReference(subscriber);
    }

    public override void Unsubscribe<T>(Action<T> subscriber)
    {
        base.Unsubscribe<T>(subscriber);
        if (_weakSubscribers.TryGetValue(typeof(T), out var weakSubscribersList))
        {
            weakSubscribersList.TryRemove(subscriber.GetHashCode(), out _);
        }
    }

    public override void Unsubscribe<T>(Func<T, Task> subscriber)
    {
        base.Unsubscribe<T>(subscriber);
        if (_weakSubscribers.TryGetValue(typeof(T), out var weakSubscribersList))
        {
            weakSubscribersList.TryRemove(subscriber.GetHashCode(), out _);
        }
    }
}
}