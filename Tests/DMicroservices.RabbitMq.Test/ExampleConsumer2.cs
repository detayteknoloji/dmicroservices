using System;
using System.Threading;
using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Consumer;
using DMicroservices.RabbitMq.Test.Services;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client.Events;

namespace DMicroservices.RabbitMq.Test
{
    [ListenQueue("ExampleQueue2")]
    class ExampleConsumer2 : BasicConsumer<ExampleModel>
    {
        public override bool AutoAck => false;

        public override Action<ExampleModel, BasicDeliverEventArgs> DataReceivedAction => DataReceived;

        private void DataReceived(ExampleModel model, BasicDeliverEventArgs e)
        {
            Console.WriteLine(model.Message);

            Console.WriteLine($"2-1 :{ServiceProvider.GetService<ExampleGuidService>().Guid}");

            var scopedService = ServiceProvider.GetService<ExampleScopedService>();
            int i = new Random().Next(1, 100);
            scopedService.UserId = i;
            Thread.Sleep(300);
            string message = $"Scoped set ({i}). Message : {scopedService.GetMessage()} Data : {model.Message}";
            Console.WriteLine(message);
            var singleService = ServiceProvider.GetService<ExampleSingleService>();
            int j = new Random().Next(1, 100);
            
            singleService.UserId = j;
            Thread.Sleep(300);
            string message1 = $"Single set ({j}). Message : {singleService.GetMessage()} Data : {model.Message}";
            Console.WriteLine(message1);

            //Send Ack.
            BasicAck(e.DeliveryTag, false);

        }

    }
}
