using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DMicroservices.DataAccess.Redis;
using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Producer;
using DMicroservices.RabbitMq.Test.Services;
using DMicroservices.Utils.Logger;
using MessagePack.Resolvers;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DMicroservices.RabbitMq.Test
{
    class Program
    {
        static void Main(string[] args)
        {

            CreateHostBuilder(args).Build().Run();

        }
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<ExampleSingleService>();
                    services.AddScoped<ExampleScopedService>();
                    services.AddScoped<ExampleGuidService>();

                    services.AddSingleton<ConsumerRegistry>();
                    services.AddHostedService<TestService>();
                });
        }

       
    }

    public class TestService(ConsumerRegistry consumerRegistry) : BackgroundService
    {
        private readonly ConsumerRegistry _consumerRegistry = consumerRegistry;

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            BasicPublishTest();

            return Task.CompletedTask;
        }

        void BasicPublishTest()
        {
            Debug.WriteLine("all register");
            _consumerRegistry.Register(typeof(ExampleConsumer));
            _consumerRegistry.Register(typeof(ExampleConsumer2));
            _consumerRegistry.Register(typeof(ExchangeConsumer));

            //Debug.WriteLine("all register");
            //ConsumerRegistry.Instance.Register(typeof(ExampleConsumer));
            //ConsumerRegistry.Instance.Register(typeof(ExampleConsumer2));
            //Console.WriteLine("ok");

            ElasticLogger.Instance.Info("test");

            ThreadPool.QueueUserWorkItem(delegate
            {
                for (int i = 0; i < 100; i++)
                {
                    RabbitMqPublisher<ExampleModel>.Instance.Publish("ExampleQueue", new ExampleModel()
                    {
                        Message = $"hello {i}"
                    });
                }
            });
            ThreadPool.QueueUserWorkItem(delegate
            {
                for (int i = 0; i < 100; i++)
                {
                    RabbitMqPublisher<ExampleModel>.Instance.Publish("ExampleQueue2", new ExampleModel()
                    {
                        Message = $"hello {i}"
                    });
                }
            });

            Console.ReadLine();
        }

        static void ExchangePublishTest()
        {
            //ConsumerRegistry.Instance.Register(typeof(ExchangeConsumer));

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
