using System;
using System.Threading;
using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Producer;

namespace DMicroservices.RabbitMq.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsumerRegistry.Instance.Register(typeof(ExampleConsumer));

            ThreadPool.QueueUserWorkItem(delegate
            {
                for (int i = 0; i < 100; i++)
                {
                    RabbitMqPublisher<ExampleModel>.Instance.Publish("ExampleQueue", new ExampleModel()
                    {
                        Message = "hello world."
                    });

                    Thread.Sleep(500);
                }
            });

            //register

            Console.ReadLine();

            //unregister
            ConsumerRegistry.Instance.UnRegister(typeof(ExampleConsumer));

            Console.ReadLine();

        }
    }
}
