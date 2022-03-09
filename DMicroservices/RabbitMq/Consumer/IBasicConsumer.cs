using System;
using System.Threading.Tasks;

namespace DMicroservices.RabbitMq.Consumer
{
    public interface IConsumer 
    {
        string ListenQueueName { get;}

        bool AutoAck { get; }

        ushort PrefectCount { get; set; }

        Task StartConsume();

        Task StopConsume();
    }
}
