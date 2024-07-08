using System;
using System.Threading.Tasks;

namespace ADD_InternalEventBus.AbsDomain
{
    public interface IEventBus : IDisposable
    {
        void Subscribe<T>(Action<T> subscriber);
        void Subscribe<T>(Func<T, Task> subscriber);
        void Unsubscribe<T>(Action<T> subscriber);
        void Unsubscribe<T>(Func<T, Task> subscriber);
        Task PublishAsync<T>(T eventMessage);
        void Publish<T>(T eventMessage);
    }
}