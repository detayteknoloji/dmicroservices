using DMicroservices.RabbitMq.Base;
using DMicroservices.RabbitMq.Model;
using DMicroservices.Utils.Logger;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
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

        /// <summary>
        // her mesaj geldiğinde mesajı işleyecek sınıf ıcın yeni bir referans türetip türetmeyeceğimize bakarız ki, tek consumerin üzerindeki referansı ezemedığımız(rabbitmq channel kapanıp açıldıgında başka bir mesajın companynosu dataaccessorsleri ezerse halen işlemekte olan step datasının companynosu kayar) datayı izole edelım dıye dinleyen sınıfdan yeni referansla işlem yaparız.
        /// </summary>
        protected virtual bool IsolatedExecution => false;

        /// <summary>
        /// eğerki izolatedexecution açıksa şuanki izolated kim ise onu kapanış anında cancel eder
        /// </summary>
        private volatile BasicConsumer<T> _currentExecutor;

        /// <summary>
        /// eğerki izolatedexecution açıksa şuanki izolated kim ise onu kapanış anında cancel eder
        /// </summary>
        private BasicConsumer<T> _listener;

        /// <summary>
        /// şuan kaç mesaj işleniyorsa o mesajın sayısını tutar işi bittiğinde 0 olması beklenir böylece işi bitmemiş oldugunu anlayıp shutdown/up durumunda bekletme sağlatırız
        /// </summary>
        private int _inFlightCount;

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

                // mesaj datareceivedde bitmeden kuyruk öldüyse tekrar dinlemeye o ölü olanın işi bitmeden tekrar başlamaz!!! her 15 saniyede bir mail atalım
                int drainTimeoutSeconds = 120;
                string drainTimeoutEnv = Environment.GetEnvironmentVariable("RABBIT_DRAIN_TIMEOUT_SECONDS");
                if (!string.IsNullOrEmpty(drainTimeoutEnv) && int.TryParse(drainTimeoutEnv, out int drainTimeoutParsed) && drainTimeoutParsed > 0)
                    drainTimeoutSeconds = drainTimeoutParsed;

                int drainWaitedSeconds = 0;
                while (Volatile.Read(ref _inFlightCount) > 0)
                {
                    // kuyrugun işi bitene kadar bekletelim. eğerki finally de                 Interlocked.Decrement(ref _inFlightCount); 0 olduysa başarılıdır yada stepbase de SignalShutdownContinue cagırılırsa bekleme erken bitirilir ve kuyruk tekrar dınlenir
                    _shutdownWaitHandle.Wait(TimeSpan.FromSeconds(1));
                    drainWaitedSeconds++;

                    if (drainWaitedSeconds % drainTimeoutSeconds == 0 && Volatile.Read(ref _inFlightCount) > 0)
                    {
                        string drainAlertMessage =
                            $"RabbitMqChannelShutdown: Drain timeout! {drainWaitedSeconds} saniye geçti, in-flight delivery hala bitmedi. Delivery tamamlanmadan kuyruk yeniden dinlenmeyecek! Queue: {_listenQueueName}";

                        ElasticLogger.Instance.InfoSpecificIndexFormat(drainAlertMessage, ConstantString.RABBITMQ_INDEX_FORMAT);

                        try
                        {
                            ConsumerAlertNotifier.DrainTimeoutAlert?.Invoke(_listenQueueName, drainAlertMessage);
                        }
                        catch (Exception alertEx)
                        {
                            ElasticLogger.Instance.ErrorSpecificIndexFormat(alertEx, $"RabbitMqChannelShutdown: Drain timeout alarmı iletilemedi! Queue: {_listenQueueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
                        }
                    }
                }
                _shutdownWaitHandle.Reset();

                ConsumerListening = false;
                if (_dontReinitialize)
                    return;

                StartConsume();
                _isShutdowning = false;
            }
        }

        /// <summary>
        /// Shutdown işlemini bekleten sinyali serbest bırakır.
        /// Executor üzerinden çağrıldığında sinyal, kanalın sahibi olan listener'a iletilir.
        /// </summary>
        protected void SignalShutdownContinue()
        {
            ElasticLogger.Instance.InfoSpecificIndexFormat(
                        $"RabbitMqCleanup ReceivedSignal: Signal Alındı kilit açıldı.",
                        ConstantString.RABBITMQ_INDEX_FORMAT);
            (_listener ?? this)._shutdownWaitHandle.Set();
        }

        /// <summary>
        /// Consumer, ConsumerRegistrye kayıt edildiğinde yani activator ile yeni instance yarattıgımız anda bir kez çalıştıracağımız methoddur. bunu dinleyen Cron/iş emri kaydı gibi "process başına bir kez" olan işler ctor yerine burada yapılır; IsolatedExecution true olan consumerlerin ctor her yeni mesaj için newleme yapıldığından artık ctorda job activate edilen kuyruklar için burası çalıştırılır, diğer yeni newleme artık burda çalışmaz.
        /// </summary>
        public virtual void OnConsumerRegistered()
        {
        }

        private void DocumentConsumerOnReceived(object sender, BasicDeliverEventArgs e)
        {
            string jsonData = null;
            Interlocked.Increment(ref _inFlightCount);
            try
            {
                jsonData = Encoding.UTF8.GetString(e.Body.ToArray());
                var parsedData = JsonConvert.DeserializeObject<T>(jsonData);

                if (IsolatedExecution)
                {
                    // her mesaj için yeni instance türetelimki mesajlar arası companyuno kayması tamamen referansın içinde kalsın.
                    var executor = (BasicConsumer<T>)Activator.CreateInstance(GetType());
                    executor.AttachDelivery(this, sender as EventingBasicConsumer);

                    _currentExecutor = executor;
                    try
                    {
                        executor.DataReceivedAction(parsedData, e);
                    }
                    finally
                    {
                        _currentExecutor = null;
                    }
                }
                else
                {
                    DataReceivedAction(parsedData, e);
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"DocumentConsumer generic data received exception: {ex.Message}, ConsumerTag {e?.ConsumerTag}", ConstantString.RABBITMQ_INDEX_FORMAT, new System.Collections.Generic.Dictionary<string, object>() { { "Data:", jsonData } });

                IModel deliveryChannel = (sender as EventingBasicConsumer)?.Model ?? _rabbitMqChannel;
                if (deliveryChannel is { IsOpen: true })
                    deliveryChannel.BasicNack(e.DeliveryTag, false, false);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlightCount);
            }
        }

        private void AttachDelivery(BasicConsumer<T> listener, EventingBasicConsumer deliveryConsumer)
        {
            _listener = listener;
            _listenQueueName = listener._listenQueueName;
            _eventingBasicConsumer = deliveryConsumer ?? listener._eventingBasicConsumer;
            _rabbitMqChannel = _eventingBasicConsumer?.Model;
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
                // IsolatedExecution açıkken gelen her mesaj, executor instancesi üzerinde işlendiğinden;  iptal edilecek CancellationTokenSource da onun üzerindedir eğerki izolated açık degilse thisdir.
                object target = _currentExecutor ?? this;
                var currentType = target.GetType();

                var tokenSourceField = currentType.GetField("CancellationTokenSource", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (tokenSourceField != null)
                {
                    var tokenSourceObj = tokenSourceField.GetValue(target) as CancellationTokenSource;
                    tokenSourceObj?.Cancel();
                    ElasticLogger.Instance.InfoSpecificIndexFormat($"CancellationTokenSource canceled (field) in {currentType.Name}", ConstantString.RABBITMQ_INDEX_FORMAT);
                }
                else
                {
                    var tokenSourceProperty = currentType.GetProperty("CancellationTokenSource", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (tokenSourceProperty != null)
                    {
                        var tokenSourceObj = tokenSourceProperty.GetValue(target) as CancellationTokenSource;
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
