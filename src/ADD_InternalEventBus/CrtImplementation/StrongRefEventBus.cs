using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using ADD_InternalEventBus.AbsDomain;
using ADD_InternalEventBus.CrtImplementation.BackgroundJobQueue;
using Microsoft.Extensions.Logging;

namespace ADD_InternalEventBus.CrtImplementation
{
    public class StrongRefEventBus : EventBusBase
    {
        public StrongRefEventBus(ILogger<EventBus> logger, EventBusOptions? options = null) : base(logger, options)
        {
        }
        
        protected override void PublishInternal<T>(T eventMessage)
        {
            if (!_subscribers.TryGetValue(typeof(T), out var subscribersList)) return;

            var toRemove = new List<int>();

            if (_fireAndForget)
            {
                foreach (var (key, subscriber) in subscribersList)
                {
                    if (subscriber != null)
                    {
                        HandleSubscriber(subscriber, eventMessage);
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
                foreach (var (key, subscriber) in subscribersList)
                {
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
            }

            foreach (var key in toRemove)
            {
                subscribersList.TryRemove(key, out _);
            }
        }
    }
}