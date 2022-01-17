using System;
using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Producer;

namespace DMicroservices.RabbitMq.Test
{
    class Program
    {
        static void Main(string[] args)
        {

            ConsumerRegistry.Instance.Register(typeof(ExampleConsumer));

            RabbitMqPublisher<ExampleModel>.Instance.Publish("ExampleQueue", new ExampleModel()
            {
                Message = "hello world."
            });

            Console.ReadLine();
        }
    }
}
