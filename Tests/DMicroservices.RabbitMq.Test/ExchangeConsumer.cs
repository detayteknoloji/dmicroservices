using DMicroservices.RabbitMq.Consumer;
using DMicroservices.RabbitMq.Model;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System;
using DMicroservices.RabbitMq.Base;

namespace DMicroservices.RabbitMq.Test
{
    [ListenQueue("ExampleQueue")]
    class ExchangeConsumer : BasicConsumer<ExampleModel>
    {

        public override ExchangeContent ExchangeContent => new ExchangeContent() { ExchangeName = "ExampleExchange", ExchangeType = ExchangeType.Fanout };

        public override bool AutoAck => false;

        public override Action<ExampleModel, BasicDeliverEventArgs> DataReceivedAction => DataReceived;

        private void DataReceived(ExampleModel model, BasicDeliverEventArgs e)
        {
            Console.WriteLine(model.Message);
            BasicAck(e.DeliveryTag, false);
        }
    }
}
