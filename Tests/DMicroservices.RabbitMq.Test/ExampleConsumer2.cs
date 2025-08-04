using System;
using System.Threading;
using DMicroservices.DataAccess.Redis;
using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Consumer;
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
            try
            {
                var resultLockFact = RedLockManager.Instance.TryGetLockFactory(out var factory);
                using (var createdLock = factory.CreateLock("res", new TimeSpan(0, 0, 1)))
                {
                    var isAck = createdLock.IsAcquired;
                    if (isAck)
                    {

                    }
                }
                var result = RedisManagerV2.Instance.Set("testkey2", "test", isThrowEx: false);
            }
            catch (OperationCanceledException ex)
            {
            }

            //Send Ack.
            BasicAck(e.DeliveryTag, false);

        }

    }
}
