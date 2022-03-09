using System;
using System.Text;
using System.Threading;
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
        private EventingBasicConsumer _eventingBasicConsumer;

        /// <summary>
        /// bu consumer tekrar initialize edilebilir mi?
        /// </summary>
        private bool _cantBeReInitilaze = false;

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

                if (ExchangeContent != null)
                {
                    if (ExchangeContent.RoutingKey == null || string.IsNullOrEmpty(ExchangeContent.ExchangeName) || string.IsNullOrEmpty(ExchangeContent.ExchangeType))
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
                _rabitMqChannel.ModelShutdown += (sender, args) =>
                {
                    if (args.ReplyCode != 200)
                    {
                        ElasticLogger.Instance.Error(new Exception(args.ToString()), "RabbitMQ/Shutdown");
                        ThreadPool.QueueUserWorkItem(RabbitMqChannelShutdown);
                    }
                };
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, "RabbitMQ/RabbitmqConsumer");
            }
        }

        private void RabbitMqChannelShutdown(object? state)
        {
            try
            {
                _rabitMqChannel.Close();
            }
            catch
            {
                //ignored
            }
            _rabitMqChannel = null;
            if (_cantBeReInitilaze)
                return;

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

        /// <summary>
        /// basicconsumer.received removed on dispose and wait 30sec. then rmqchannel will be disposed.
        /// </summary>%
        public void Dispose()
        {
            _eventingBasicConsumer.Received -= DocumentConsumerOnReceived;
            Thread.Sleep(TimeSpan.FromSeconds(15));
            _cantBeReInitilaze = true;
            _rabitMqChannel?.Dispose();
            _rabitMqChannel = null;
        }

        public void Dispose(bool cantBeReInitilaze)
        {
            _eventingBasicConsumer.Received -= DocumentConsumerOnReceived;
            Thread.Sleep(TimeSpan.FromSeconds(15));
            _cantBeReInitilaze = cantBeReInitilaze;
            _rabitMqChannel?.Dispose();
            _rabitMqChannel = null;
        }

    }
}
