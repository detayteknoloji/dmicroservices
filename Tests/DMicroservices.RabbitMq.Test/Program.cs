using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Producer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace DMicroservices.RabbitMq.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsumerRegistry.Instance.Register(typeof(ExampleConsumer), 2);
            Console.ReadLine();
            //BasicPublishTest();
            //ExchangePublishTest();
        }

        static void BasicPublishTest()
        {
            Debug.WriteLine("all register");


            //Debug.WriteLine("all register");
            //ConsumerRegistry.Instance.Register(typeof(ExampleConsumer));
            //ConsumerRegistry.Instance.Register(typeof(ExampleConsumer2));
            //Console.WriteLine("ok");

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
            ConsumerRegistry.Instance.Register(typeof(ExchangeConsumer), 2);

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
