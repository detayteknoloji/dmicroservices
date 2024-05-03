using DMicroservices.RabbitMq.Model;
using DMicroservices.Utils.Logger;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DMicroservices.RabbitMq.Base
{
    /// <summary>
    /// Rabbitmq bağlantı backend classı
    /// </summary>
    public class RabbitMqConnection
    {
        #region Singleton Section
        private static readonly Lazy<RabbitMqConnection> _instance = new Lazy<RabbitMqConnection>(() => new RabbitMqConnection());


        public static RabbitMqConnection Instance => _instance.Value;
        #endregion

        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        public RabbitMqConnection()
        {
        }

        #endregion

        #region Properties

        private static readonly object _lockObj = new object();

        public IConnection Connection { get; set; }

        private ConcurrentDictionary<ConnectionType, IConnection> ConnectionList { get; set; } = new ConcurrentDictionary<ConnectionType, IConnection>();

        public bool IsConnected => Connection is { IsOpen: true };

        #endregion

        #region Method

        /// <summary>
        /// Rabbitmq bağlantısı oluşturup döner
        /// </summary>
        /// <returns></returns>
        public IConnection GetConnection()
        {
            if (IsConnected)
                return Connection;
            try
            {
                lock (_lockObj)
                {
                    string hostName = Environment.GetEnvironmentVariable("HOSTNAME");
                    if (string.IsNullOrEmpty(hostName))
                    {
                        hostName = Environment.GetEnvironmentVariable("COMPUTERNAME");
                    }
                    if (IsConnected)
                        return Connection;
                    ConnectionFactory connectionFactory = new ConnectionFactory
                    {
                        Uri = new Uri(Environment.GetEnvironmentVariable("RABBITMQ_URI")),
                        AutomaticRecoveryEnabled = false,
                        ClientProvidedName = hostName
                    };
                    Connection = connectionFactory.CreateConnection();
                    Connection.ConnectionShutdown += (sender, args) =>
                    {
                        if (args.ReplyCode != 200)
                        {
                            ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception($"{args}"), "RabbitMQ/ConnectionShutdown", ConstantString.RABBITMQ_INDEX_FORMAT);
                        }
                    };
                    return Connection;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, "RabbitmqConnection", ConstantString.RABBITMQ_INDEX_FORMAT);
                return null;
            }
        }

        public IConnection GetConnection(ConnectionType connectionType)
        {
            if (ConnectionList.TryGetValue(connectionType, out IConnection connectionObject) && connectionObject.IsOpen)
                return connectionObject;
            try
            {
                lock (_lockObj)
                {
                    string hostName = Environment.GetEnvironmentVariable("HOSTNAME");
                    if (string.IsNullOrEmpty(hostName))
                    {
                        hostName = Environment.GetEnvironmentVariable("COMPUTERNAME");
                    }
                    if (ConnectionList.TryGetValue(connectionType, out IConnection connectionObjectInner) && connectionObjectInner.IsOpen)
                        return connectionObjectInner;

                    ConnectionFactory connectionFactory = new ConnectionFactory
                    {
                        Uri = new Uri(Environment.GetEnvironmentVariable("RABBITMQ_URI")),
                        AutomaticRecoveryEnabled = false,
                        ClientProvidedName = hostName
                    };
                    var connection = connectionFactory.CreateConnection();

                    ConnectionList.TryAdd(connectionType, connection);
                    connection.ConnectionShutdown += (sender, args) =>
                    {
                        if (args.ReplyCode != 200)
                        {
                            ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception($"{args}"), "RabbitMQ/ConnectionShutdown", ConstantString.RABBITMQ_INDEX_FORMAT);
                        }
                    };
                    return connection;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, "RabbitmqConnection", ConstantString.RABBITMQ_INDEX_FORMAT);
                return null;
            }
        }

        /// <summary>
        /// Channel oluşturup döner
        /// </summary>
        /// <returns></returns>
        public IModel GetChannel(string queueName)
        {
            IModel channel;

            try
            {
                channel = GetConnection().CreateModel();
                channel.QueueDeclarePassive(queueName);
            }
            catch (Exception e)
            {
                channel = GetConnection().CreateModel();
                channel.QueueDeclare(queueName, true, false, false, null);
            }

            return channel;
        }

        /// <summary>
        /// Channel oluşturup döner
        /// </summary>
        /// <returns></returns>
        public IModel GetChannel(string queueName, byte maxPriority)
        {
            IModel channel;

            try
            {
                channel = GetConnection().CreateModel();
                channel.QueueDeclarePassive(queueName);
            }
            catch (Exception e)
            {
                channel = GetConnection().CreateModel();
                channel.QueueDeclare(queueName, true, false, false, new Dictionary<string, object>()
                {
                    {"x-max-priority", maxPriority}
                });
            }

            return channel;
        }
        /// <summary>
        /// Exchange Channel oluşturup döner
        /// </summary>
        /// <returns></returns>
        public IModel GetExchangeChannel(ExchangeContent exchangeContent, string queueName)
        {
            IModel channel = GetConnection().CreateModel();
            channel.ExchangeDeclare(exchangeContent.ExchangeName, exchangeContent.ExchangeType);
            channel.QueueDeclare(queueName, true, false, false);
            channel.QueueBind(queueName, exchangeContent.ExchangeName, exchangeContent.RoutingKey, exchangeContent.Headers);
            return channel;
        }

        #endregion
    }
}
