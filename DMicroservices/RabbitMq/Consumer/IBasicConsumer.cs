using System;

namespace DMicroservices.RabbitMq.Consumer
{
    public interface IConsumer : IDisposable
    {
        string ListenQueueName { get;}

        bool AutoAck { get; }

        ushort PrefectCount { get; set; }


    }
}
