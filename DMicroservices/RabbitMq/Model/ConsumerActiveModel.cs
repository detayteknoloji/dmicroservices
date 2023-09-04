using DMicroservices.RabbitMq.Consumer;
using System;

namespace DMicroservices.RabbitMq.Model
{
    public class ConsumerActiveModel
    {
        public Type Type { get; set; }

        public IConsumer IConsumer { get; set; }

        public ushort ParallelismCount { get; set; }
    }
}
