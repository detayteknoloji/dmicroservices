using System;
using System.Text;
using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Model;
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

        public virtual ExchangeContent ExchangeContent { get; set; }

        public virtual Action<T, BasicDeliverEventArgs> DataReceivedAction { get; }

        /// <summary>
        /// Modeli dinlemek için kullanıclan event
        /// </summary>
        private readonly EventingBasicConsumer _eventingBasicConsumer;

        private readonly IModel _rabitMqChannel;

        protected BasicConsumer()
        {
            try
            {
                if (string.IsNullOrEmpty(ListenQueueName))
                {
                    ElasticLogger.Instance.Info("Consumer QueueName was null");
                }

                if (ExchangeContent != null)
                {
                    if (ExchangeContent.Key == null || string.IsNullOrEmpty(ExchangeContent.Name) || string.IsNullOrEmpty(ExchangeContent.Type))
                        throw new Exception("ExchangeContent contains null object(s)!");
                    _rabitMqChannel = RabbitMqConnection.Instance.GetExchangeChannel(ExchangeContent, ListenQueueName);
                }
                else
                {
                    _rabitMqChannel = MaxPriority > 0
                      ? RabbitMqConnection.Instance.GetChannel(ListenQueueName, MaxPriority)
                      : RabbitMqConnection.Instance.GetChannel(ListenQueueName);
                }

                if (PrefectCount != 0)
                    _rabitMqChannel.BasicQos(0, PrefectCount, false);

                _eventingBasicConsumer = new EventingBasicConsumer(_rabitMqChannel);
                _eventingBasicConsumer.Received += DocumentConsumerOnReceived;
                _rabitMqChannel.BasicConsume(ListenQueueName, AutoAck, _eventingBasicConsumer);
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, "RabbitMQ/RabbitmqConsumer");
            }


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
