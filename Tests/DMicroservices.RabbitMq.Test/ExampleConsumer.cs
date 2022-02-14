using System;
using System.Threading;
using DMicroservices.RabbitMq.Consumer;
using RabbitMQ.Client.Events;

namespace DMicroservices.RabbitMq.Test
{
    class ExampleConsumer : BasicConsumer<ExampleModel>
    {
        public override string ListenQueueName => "ExampleQueue";

        public override bool AutoAck => false;

        public override Action<ExampleModel, BasicDeliverEventArgs> DataReceivedAction => DataReceived;

        private void DataReceived(ExampleModel model, BasicDeliverEventArgs e)
        {
            Console.WriteLine(model.Message);

            for (int i = 0; i < 10; i++)
            {
                Console.WriteLine(i);
                Thread.Sleep(300);
            }

            //Send Ack.
            BasicAck(e.DeliveryTag, false);

        }

    }
}
