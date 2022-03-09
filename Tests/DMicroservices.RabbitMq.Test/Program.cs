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
            BasicPublishTest();
            //ExchangePublishTest();
        }

        static void BasicPublishTest()
        {
            ConsumerRegistry.Instance.Register(typeof(ExampleConsumer));
            Console.ReadLine();
            //ConsumerRegistry.Instance.Register(typeof(ExampleConsumer2));
            ConsumerRegistry.Instance.ClearAllRegisters();


            //ThreadPool.QueueUserWorkItem(delegate
            //{
            //    RabbitMqPublisher<ExampleModel>.Instance.Publish("ExampleQueue", new ExampleModel()
            //    {
            //        Message = "hello world."
            //    });
            //});

            Console.ReadLine();
        }

        static void ExchangePublishTest()
        {
            ConsumerRegistry.Instance.Register(typeof(ExchangeConsumer));

            ThreadPool.QueueUserWorkItem(delegate
            {
                RabbitMqPublisher<ExampleModel>.Instance.PublishExchange("ExampleExchange", "", new ExampleModel()
                {
                    Message = "hello world."
                });
            });

            Console.ReadLine();
        }
    }
}
