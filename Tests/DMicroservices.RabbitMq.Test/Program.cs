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
            var regList = new List<Type>()
            {
                typeof(ExampleConsumer),
                typeof(ExampleConsumer2),


            };
            ConsumerRegistry.Instance.RegisterWithList(regList);

            while (true)
            {
                string consoleInput = Console.ReadLine();
                switch (consoleInput)
                {
                    case "e":
                        return;
                    case "i":
                        ConsumerRegistry.Instance.IncreaseParallelism(typeof(ExampleConsumer));
                        break;
                    case "d":
                        ConsumerRegistry.Instance.DecreaseParallelism(typeof(ExampleConsumer));
                        break;
                    case "i2":
                        ConsumerRegistry.Instance.IncreaseParallelism(typeof(ExampleConsumer2));
                        break;
                    case "d2":
                        ConsumerRegistry.Instance.DecreaseParallelism(typeof(ExampleConsumer2));
                        break;
                    case "un":
                        ConsumerRegistry.Instance.UnRegisterWithList(regList);
                        break;
                }

            }
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
