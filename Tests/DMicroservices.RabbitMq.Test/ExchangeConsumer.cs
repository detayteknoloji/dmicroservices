using DMicroservices.RabbitMq.Consumer;
using DMicroservices.RabbitMq.Model;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System;

namespace DMicroservices.RabbitMq.Test
{
    class ExchangeConsumer : BasicConsumer<ExampleModel>
    {
        public override string ListenQueueName => "ExampleQueue";

        public override ExchangeContent ExchangeContent => new ExchangeContent() { Name = "ExampleExchange", Type = ExchangeType.Fanout };

        public override bool AutoAck => false;

        public override Action<ExampleModel, BasicDeliverEventArgs> DataReceivedAction => DataReceived;

        private void DataReceived(ExampleModel model, BasicDeliverEventArgs e)
        {
            Console.WriteLine(model.Message);
            BasicAck(e.DeliveryTag, false);
        }
    }
}
