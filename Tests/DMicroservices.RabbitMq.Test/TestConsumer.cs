using System;
using System.Collections.Generic;
using System.Text;
using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Consumer;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client.Events;

namespace DMicroservices.RabbitMq.Test
{
    [ListenQueue("Test")]
    class TestConsumer : BasicConsumer<ExampleModel>
    {
        public override bool AutoAck => false;
        public override Action<ExampleModel, BasicDeliverEventArgs> DataReceivedAction => DataReceived;

        private void DataReceived(ExampleModel arg1, BasicDeliverEventArgs arg2)
        {

        }

    }
}
