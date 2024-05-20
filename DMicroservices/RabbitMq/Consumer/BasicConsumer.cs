﻿using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Model;
using DMicroservices.Utils.Logger;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nest;

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
        public ushort DynamicPrefectCount { get; set; } = 0;

        public virtual byte MaxPriority { get; set; } = 0;

        public virtual ExchangeContent ExchangeContent { get; set; }

        public virtual Action<T, BasicDeliverEventArgs> DataReceivedAction { get; }

        private bool _consumerListening = false;

        public bool ConsumerListening
        {
            get => _consumerListening;
            set
            {
                _consumerListening = value;
            }
        }

        /// <summary>
        /// Modeli dinlemek için kullanıclan event
        /// </summary>
        private EventingBasicConsumer _eventingBasicConsumer;

        /// <summary>
        /// bu consumer tekrar initialize edilebilir mi?
        /// </summary>
        private bool _dontReinitialize = false;

        private IModel _rabbitMqChannel;

        private readonly object _stateChangeLockObject = new object();


        protected BasicConsumer()
        {

        }


        private void RabbitMqChannelShutdown()
        {
            ElasticLogger.Instance.InfoSpecificIndexFormat($"Only RabbitMqChannelShutdown Signal", ConstantString.RABBITMQ_INDEX_FORMAT);

            try
            {
                _eventingBasicConsumer.OnCancel(_eventingBasicConsumer.ConsumerTags);
                _rabbitMqChannel?.Dispose();
                _rabbitMqChannel = null;
            }
            catch (Exception e)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(e, $"RabbitMqChannelShutdown Signal Error", ConstantString.RABBITMQ_INDEX_FORMAT);

                //ignored
            }

            Thread.Sleep(3000);

            ConsumerListening = false;
            if (_dontReinitialize)
                return;

            StartConsume();
        }

        private void DocumentConsumerOnReceived(object sender, BasicDeliverEventArgs e)
        {
            string jsonData = null;
            try
            {
                jsonData = Encoding.UTF8.GetString(e.Body.ToArray());
                var parsedData = JsonConvert.DeserializeObject<T>(jsonData);
                DataReceivedAction(parsedData, e);
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"DocumentConsumer generic data received exception: {ex.Message}, ConsumerTag {e?.ConsumerTag}", ConstantString.RABBITMQ_INDEX_FORMAT, new System.Collections.Generic.Dictionary<string, object>() { { "Data:", jsonData } });
                _rabbitMqChannel.BasicNack(e.DeliveryTag, false, false);
            }
        }

        protected void BasicAck(ulong deliveryTag, bool multiple)
        {
            _rabbitMqChannel.BasicAck(deliveryTag, multiple);
        }

        protected EventingBasicConsumer GetCurrentConsumer()
        {
            return _eventingBasicConsumer;
        }

        public Task StartConsume()
        {
            return Task.Run(() =>
            {
                Debug.WriteLine($"Consumer {ListenQueueName} start requested. Status: New");
                lock (_stateChangeLockObject)
                {
                    Debug.WriteLine($"Consumer {ListenQueueName} start process started. Status: Pending");
                    if (ConsumerListening)
                    {
                        Debug.WriteLine($"Consumer {ListenQueueName} start process started. Status: Already Listening");
                        return;
                    }

                    try
                    {
                        if (string.IsNullOrEmpty(ListenQueueName))
                        {
                            ElasticLogger.Instance.InfoSpecificIndexFormat("Consumer QueueName was null", ConstantString.RABBITMQ_INDEX_FORMAT);
                        }

                        if (ExchangeContent != null)
                        {
                            if (ExchangeContent.RoutingKey == null ||
                                string.IsNullOrEmpty(ExchangeContent.ExchangeName) ||
                                string.IsNullOrEmpty(ExchangeContent.ExchangeType))
                                throw new Exception("ExchangeContent contains null object(s)!");
                            _rabbitMqChannel =
                                RabbitMqConnection.Instance.GetExchangeChannel(ExchangeContent, ListenQueueName);
                        }
                        else
                        {
                            _rabbitMqChannel = MaxPriority > 0
                                ? RabbitMqConnection.Instance.GetChannel(ListenQueueName, MaxPriority)
                                : RabbitMqConnection.Instance.GetChannel(ListenQueueName);
                        }

                        if (PrefectCount != 0 && DynamicPrefectCount == 0)
                            _rabbitMqChannel.BasicQos(0, PrefectCount, false);
                        else if(DynamicPrefectCount != 0)
                            _rabbitMqChannel.BasicQos(0, DynamicPrefectCount, false);

                        _eventingBasicConsumer = new EventingBasicConsumer(_rabbitMqChannel);
                        _eventingBasicConsumer.Received += DocumentConsumerOnReceived;
                        _rabbitMqChannel.BasicConsume(ListenQueueName, AutoAck, _eventingBasicConsumer);
                        ConsumerListening = true;
                        _rabbitMqChannel.ModelShutdown += (sender, args) =>
                        {
                            if (args.ReplyCode != 200)
                            {
                                ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception($"{args} Queue: {ListenQueueName}"), "RabbitMQ/ModelShutdown", ConstantString.RABBITMQ_INDEX_FORMAT);
                                Task.Run(RabbitMqChannelShutdown);
                            }
                        };
                    }
                    catch (Exception ex)
                    {
                        ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQ/RabbitmqConsumer Error! Queue: {ListenQueueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    }
                }
                Debug.WriteLine($"Consumer {ListenQueueName} start completed. Status: Success");
            });
        }

        public void ChangePrefetchCount(ushort prefetchCount)
        {
            DynamicPrefectCount = prefetchCount;

            StopConsume().Wait();
            StartConsume().Wait();
        }

        public Task StopConsume()
        {
            return Task.Run(() =>
             {
                 Debug.WriteLine($"Consumer {ListenQueueName} stop requested.");
                 lock (_stateChangeLockObject)
                 {
                     Debug.WriteLine($"Consumer {ListenQueueName} stop process started.");
                     if (!ConsumerListening)
                         return;

                     _dontReinitialize = true;

                     _eventingBasicConsumer.Received -= DocumentConsumerOnReceived;
                     Thread.Sleep(TimeSpan.FromSeconds(15));
                     _eventingBasicConsumer.OnCancel(_eventingBasicConsumer.ConsumerTags);
                     _rabbitMqChannel?.Dispose();
                     _rabbitMqChannel = null;
                     ConsumerListening = false;
                     Debug.WriteLine($"Consumer {ListenQueueName} stop completed.");
                 }
             });
        }

    }
}
