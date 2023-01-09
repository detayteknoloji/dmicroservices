using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Debug.WriteLine("all register");
            //ConsumerRegistry.Instance.Register(typeof(ExampleConsumer));

            for (int j = 0; j < 100; j++)
            {

                List<string> exm = new List<string>();
                for (int i = 0; i < 100000; i++)
                {
                    exm.Add($"C:\\apache-maven-3.8.5\\lib\\jansi-native\\Windows\\x86_64C:\\apache-maven-3.8.5\\lib\\jansi-native\\Windows\\x86_64C:\\apache-maven-3.8.5\\lib\\jansi-native\\Windows\\x86_64C:\\apache-maven-3.8.5\\lib\\jansi-native\\Windows\\x86_64{i.ToString()}");
                }

                RabbitMqPublisher<ExampleModel>.Instance.Publish("ExampleQueu2e", exm);
            }
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
