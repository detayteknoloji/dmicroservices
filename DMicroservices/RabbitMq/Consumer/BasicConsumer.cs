using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Model;
using DMicroservices.Utils.Logger;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DMicroservices.RabbitMq.Consumer
{
    /// <summary>
    /// Consuming base
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class BasicConsumer<T> : IConsumer
    {
        private string _listenQueueName;

        public abstract bool AutoAck { get; }

        public virtual ushort PrefectCount { get; set; }
        public ushort DynamicPrefectCount { get; set; } = 0;

        public virtual byte MaxPriority { get; set; } = 0;
        public virtual bool Durable { get; set; } = true;

        public virtual bool AutoDelete { get; set; } = false;

        public virtual bool WaitSignalCleanup { get; set; } = false;

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
        private bool _isShutdowning = false;

        private IModel _rabbitMqChannel;

        private readonly object _stateChangeLockObject = new object();
        private readonly object _shutdownChangeLockObject = new object();
        private readonly ManualResetEventSlim _shutdownWaitHandle = new ManualResetEventSlim(false);

        protected BasicConsumer()
        {
            var listenQueueAttribute = GetType().GetCustomAttribute<ListenQueueAttribute>();
            if (listenQueueAttribute == null || string.IsNullOrEmpty(listenQueueAttribute.ListenQueue))
            {
                throw new Exception($"{GetType().FullName} sınıfı için ListenQueue attibute zorunludur");
            }

            _listenQueueName = listenQueueAttribute.ListenQueue;
        }


        private void RabbitMqChannelShutdown()
        {
            if (_isShutdowning)
                return;
            lock (_shutdownChangeLockObject)
            {
                _isShutdowning = true;
                ElasticLogger.Instance.InfoSpecificIndexFormat($"Only RabbitMqChannelShutdown Signal", ConstantString.RABBITMQ_INDEX_FORMAT);

                // IS_RABBIT_SHUTDOWN_CALL_CANCEL parametresi açıksa ve kuyruk üzerindeki WaitSignalCleanup property'si true ise stepExecution üzerindeki işlem bitmeden veya 120 saniye geçmeden kuyruk öldüğünde, tekrar dinlemeye başlamaz, ya StepBase'nin finally bloğu çalışmalı yada 120 saniye geçmiş olmalıdır.
                bool invokeCancellationToken =
                     !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IS_RABBIT_SHUTDOWN_CALL_CANCEL"))
                        ? bool.Parse(Environment.GetEnvironmentVariable("IS_RABBIT_SHUTDOWN_CALL_CANCEL"))
                        : false;

                if (invokeCancellationToken && WaitSignalCleanup)
                    OnShutdownTriggered();

                try
                {
                    if (_eventingBasicConsumer is { IsRunning: true })
                        _eventingBasicConsumer.OnCancel(_eventingBasicConsumer.ConsumerTags);
                    _rabbitMqChannel?.Dispose();
                    _rabbitMqChannel = null;
                }
                catch (Exception e)
                {
                    ElasticLogger.Instance.ErrorSpecificIndexFormat(e, $"RabbitMqChannelShutdown Signal Error", ConstantString.RABBITMQ_INDEX_FORMAT);

                    //ignored
                }

                Thread.Sleep(TimeSpan.FromSeconds(10));

                if (invokeCancellationToken && WaitSignalCleanup)
                {
                    int waitTimeoutSeconds = 120; // 5 dakika sinyal bekleme süresi
                    bool isSignalReceived = _shutdownWaitHandle.Wait(TimeSpan.FromSeconds(waitTimeoutSeconds));

                    if (!isSignalReceived)
                    {
                        ElasticLogger.Instance.InfoSpecificIndexFormat(
                            $"RabbitMqChannelShutdown: Signal beklenirken timeout oluştu! {waitTimeoutSeconds} saniye geçti.",
                            ConstantString.RABBITMQ_INDEX_FORMAT);

                    }

                    // Sinyal alındıysa, resetleyelim çünkü tekrar kullanılacak.
                    _shutdownWaitHandle.Reset();
                }

                ConsumerListening = false;
                if (_dontReinitialize)
                    return;

                StartConsume();
                _isShutdowning = false;
            }
        }

        /// <summary>
        /// Shutdown işlemini bekleten sinyali serbest bırakır.
        /// </summary>
        protected void SignalShutdownContinue()
        {
            ElasticLogger.Instance.InfoSpecificIndexFormat(
                        $"RabbitMqCleanup ReceivedSignal: Signal Alındı kilit açıldı.",
                        ConstantString.RABBITMQ_INDEX_FORMAT);
            _shutdownWaitHandle.Set();
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
                if (_rabbitMqChannel != null)
                    _rabbitMqChannel.BasicNack(e.DeliveryTag, false, false);
            }
        }

        protected void BasicAck(ulong deliveryTag, bool multiple)
        {
            if (_rabbitMqChannel != null)
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
                Debug.WriteLine($"Consumer {_listenQueueName} start requested. Status: New");
                lock (_stateChangeLockObject)
                {
                    Debug.WriteLine($"Consumer {_listenQueueName} start process started. Status: Pending");
                    if (ConsumerListening)
                    {
                        Debug.WriteLine($"Consumer {_listenQueueName} start process started. Status: Already Listening");
                        return;
                    }

                    try
                    {
                        if (string.IsNullOrEmpty(_listenQueueName))
                        {
                            ElasticLogger.Instance.InfoSpecificIndexFormat("Consumer QueueName was null",
                                ConstantString.RABBITMQ_INDEX_FORMAT);
                        }

                        if (ExchangeContent != null)
                        {
                            if (ExchangeContent.RoutingKey == null ||
                                string.IsNullOrEmpty(ExchangeContent.ExchangeName) ||
                                string.IsNullOrEmpty(ExchangeContent.ExchangeType))
                                throw new Exception("ExchangeContent contains null object(s)!");
                            _rabbitMqChannel =
                                RabbitMqConnection.Instance.GetExchangeChannel(ExchangeContent, _listenQueueName, Durable, AutoDelete);
                        }
                        else
                        {
                            _rabbitMqChannel = MaxPriority > 0
                                ? RabbitMqConnection.Instance.GetChannel(_listenQueueName, MaxPriority, Durable, AutoDelete)
                                : RabbitMqConnection.Instance.GetChannel(_listenQueueName, Durable, AutoDelete);
                        }

                        if (PrefectCount != 0 && DynamicPrefectCount == 0)
                            _rabbitMqChannel.BasicQos(0, PrefectCount, false);
                        else if (DynamicPrefectCount != 0)
                            _rabbitMqChannel.BasicQos(0, DynamicPrefectCount, false);

                        _eventingBasicConsumer = new EventingBasicConsumer(_rabbitMqChannel);
                        _eventingBasicConsumer.Received += DocumentConsumerOnReceived;

                        _eventingBasicConsumer.ConsumerCancelled += (sender, args) =>
                        {
                            ElasticLogger.Instance.ErrorSpecificIndexFormat(
                                   new Exception($"{args} Queue: {_listenQueueName}"), "RabbitMQ/ConsumerCancelled",
                                   ConstantString.RABBITMQ_INDEX_FORMAT);
                            Task.Run(() => { RabbitMqChannelShutdown(); });
                        };

                        _rabbitMqChannel.BasicConsume(_listenQueueName, AutoAck, _eventingBasicConsumer);
                        _rabbitMqChannel.ModelShutdown += (sender, args) =>
                        {
                            if (args.ReplyCode != 200)
                            {
                                ElasticLogger.Instance.ErrorSpecificIndexFormat(
                                    new Exception($"{args} Queue: {_listenQueueName}"), "RabbitMQ/ModelShutdown",
                                    ConstantString.RABBITMQ_INDEX_FORMAT);
                                Task.Run(() => { RabbitMqChannelShutdown(); });
                            }
                        };

                        ConsumerListening = true;
                    }
                    catch (RabbitMQClientException connectionException)
                    {
                        if (!ConsumerListening)
                        {
                            Task.Run(() => { RabbitMqChannelShutdown(); });
                        }
                        ElasticLogger.Instance.ErrorSpecificIndexFormat(connectionException, $"RabbitMQ Connection Exception! Queue: {_listenQueueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    }
                    catch (Exception ex)
                    {
                        //try connect if not connected.
                        if (!ConsumerListening)
                        {
                            Task.Run(() => { RabbitMqChannelShutdown(); });
                        }
                        ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQ/RabbitmqConsumer Error! Queue: {_listenQueueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    }
                }
                Debug.WriteLine($"Consumer {_listenQueueName} start completed. Status: Success");
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
                Debug.WriteLine($"Consumer {_listenQueueName} stop requested.");
                lock (_stateChangeLockObject)
                {
                    Debug.WriteLine($"Consumer {_listenQueueName} stop process started.");
                    if (!ConsumerListening)
                        return;

                    _dontReinitialize = true;

                    _eventingBasicConsumer.Received -= DocumentConsumerOnReceived;
                    Thread.Sleep(TimeSpan.FromSeconds(15));
                    _eventingBasicConsumer.OnCancel(_eventingBasicConsumer.ConsumerTags);
                    _rabbitMqChannel?.Dispose();
                    _rabbitMqChannel = null;
                    ConsumerListening = false;
                    Debug.WriteLine($"Consumer {_listenQueueName} stop completed.");
                }
            });
        }

        public string GetListenQueueName()
        {
            return _listenQueueName;
        }

        protected void OnShutdownTriggered()
        {
            try
            {
                var currentType = this.GetType();

                var tokenSourceField = currentType.GetField("CancellationTokenSource", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (tokenSourceField != null)
                {
                    var tokenSourceObj = tokenSourceField.GetValue(this) as CancellationTokenSource;
                    tokenSourceObj?.Cancel();
                    ElasticLogger.Instance.InfoSpecificIndexFormat($"CancellationTokenSource canceled (field) in {currentType.Name}", ConstantString.RABBITMQ_INDEX_FORMAT);
                }
                else
                {
                    var tokenSourceProperty = currentType.GetProperty("CancellationTokenSource", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (tokenSourceProperty != null)
                    {
                        var tokenSourceObj = tokenSourceProperty.GetValue(this) as CancellationTokenSource;
                        tokenSourceObj?.Cancel();
                        ElasticLogger.Instance.InfoSpecificIndexFormat($"CancellationTokenSource canceled (property) in {currentType.Name}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    }
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, "OnShutdownTriggered method error!");
            }
        }
    }
}
