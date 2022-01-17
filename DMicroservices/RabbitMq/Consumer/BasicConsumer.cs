using System;
using System.Text;
using System.Threading;
using DMicroservices.RabbitMq.Base;
using DMicroservices.Utils.Logger;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DMicroservices.RabbitMq.Consumer
{
    /// <summary>
    /// Consuming base
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class BasicConsumer<T> : IConsumer
    {
        public abstract string ListenQueueName { get; }

        public abstract bool AutoAck { get; }

        public virtual ushort PrefectCount { get; set; }

        public virtual byte MaxPriority { get; set; } = 0;

        public virtual Action<T, BasicDeliverEventArgs> DataReceivedAction { get; }

        /// <summary>
        /// Modeli dinlemek için kullanıclan event
        /// </summary>
        private EventingBasicConsumer _eventingBasicConsumer;

        private IModel _rabitMqChannel;

        protected BasicConsumer()
        {
            InitializeConsumer();
        }

        private void InitializeConsumer()
        {
            try
            {
                if (string.IsNullOrEmpty(ListenQueueName))
                {
                    ElasticLogger.Instance.Info("Consumer QueueName was null");
                }

                _rabitMqChannel = MaxPriority > 0
                    ? RabbitMqConnection.Instance.GetChannel(ListenQueueName, MaxPriority)
                    : RabbitMqConnection.Instance.GetChannel(ListenQueueName);

                if (PrefectCount != 0)
                    _rabitMqChannel.BasicQos(0, PrefectCount, false);

                _eventingBasicConsumer = new EventingBasicConsumer(_rabitMqChannel);
                _eventingBasicConsumer.Received += DocumentConsumerOnReceived;
                _rabitMqChannel.BasicConsume(ListenQueueName, AutoAck, _eventingBasicConsumer);
                _rabitMqChannel.ModelShutdown += (sender, args) =>
                {
                    ElasticLogger.Instance.Error(new Exception(args.ToString()), "RabbitMQ/Shutdown");
                    ThreadPool.QueueUserWorkItem(RabbitMqChannelShutdown);
                };
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, "RabbitMQ/RabbitmqConsumer");
            }
        }

        private void RabbitMqChannelShutdown(object? state)
        {
            _rabitMqChannel = null;
            Thread.Sleep(5000);
            InitializeConsumer();
        }

        private void DocumentConsumerOnReceived(object sender, BasicDeliverEventArgs e)
        {
            var jsonData = Encoding.UTF8.GetString(e.Body.ToArray());
            try
            {
                var parsedData = JsonConvert.DeserializeObject<T>(jsonData);
                DataReceivedAction(parsedData, e);
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, $"DocumentConsumer generic data received exception: {ex.Message}",
                    jsonData);
                _rabitMqChannel.BasicNack(e.DeliveryTag, false, false);
            }
        }

        protected void BasicAck(ulong deliveryTag, bool multiple)
        {
            _rabitMqChannel.BasicAck(deliveryTag, multiple);
        }

        protected EventingBasicConsumer GetCurrentConsumer()
        {
            return _eventingBasicConsumer;
        }
    }
}
