using System;
using System.Threading.Tasks;

namespace DMicroservices.RabbitMq.Consumer
{
    public interface IConsumer 
    {

        bool AutoAck { get; }

        ushort PrefectCount { get; set; }

        Task StartConsume();

        Task StopConsume();

        void ChangePrefetchCount(ushort prefetchCount);
        string GetListenQueueName();
    }
}
