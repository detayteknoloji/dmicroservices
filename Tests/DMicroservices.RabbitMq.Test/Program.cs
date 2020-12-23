using System;
using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Producer;

namespace DMicroservices.RabbitMq.Test
{
    class Program
    {
        static void Main(string[] args)
        {

            ConsumerRegistry.Instance.Register(typeof(ExampleModel));

            RabbitMqPublisher<ExampleModel>.Instance.Publish("Test",new ExampleModel()
            {
                Message = "hello world."
            });

            Console.ReadLine();
        }
    }
}
