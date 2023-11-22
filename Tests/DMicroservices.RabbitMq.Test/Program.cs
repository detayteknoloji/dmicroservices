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
            };

            //ThreadPool.QueueUserWorkItem(delegate
            //{
            //    while (true)
            //    {
            //        BasicPublishTest();
            //        Thread.Sleep(1);
            //    }
            //});
            while (true)
            {
                string consoleInput = Console.ReadLine();
                switch (consoleInput)
                {
                    case "e":
                        return;
                    case "ra":
                        ConsumerRegistry.Instance.RegisterWithList(regList);
                        break;
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
                    case "cq":
                        ConsumerRegistry.Instance.ChangePrefetch(typeof(ExampleConsumer),30);
                        break;
                }

            }

            //ExchangePublishTest();
        }

        static void BasicPublishTest()
        {
            RabbitMqPublisher<ExampleModel>.Instance.Publish("ExampleQueue", new ExampleModel()
            {
                Message = "hello world."
            });
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
