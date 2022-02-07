using System;
using System.Collections.Generic;
using System.Text;
using DMicroservices.RabbitMq.Base;
using DMicroservices.Utils.Logger;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace DMicroservices.RabbitMq.Producer
{
    /// <summary>
    /// Rabbitmq publisher
    /// </summary>
    public class RabbitMqPublisher<T>
    {
        #region Singleton Section
        private static readonly Lazy<RabbitMqPublisher<T>> _instance = new Lazy<RabbitMqPublisher<T>>(() => new RabbitMqPublisher<T>());

        private RabbitMqPublisher()
        {

        }

        public static RabbitMqPublisher<T> Instance => _instance.Value;

        #endregion

        #region Member

        #endregion

        #region Property

        #endregion

        #region Methods

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        /// <param name="message">mesaj</param>
        public uint Publish(string queueName, T message)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.Info("QueueName was null");
                    return 0;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName))
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    channel.BasicPublish(string.Empty, queueName, null, Encoding.UTF8.GetBytes(jsonData));
                    return channel.MessageCount(queueName);
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, "RabbitMQPublisher");
            }

            return 0;
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        /// <param name="message">mesaj</param>
        /// <param name="headers"></param>
        public void Publish(string queueName, T message, Dictionary<string, object> headers)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.Info("QueueName was null");
                    return;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName))
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.Headers = headers;
                    channel.BasicPublish(string.Empty, queueName, properties, Encoding.UTF8.GetBytes(jsonData));
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, "RabbitMQPublisher");
            }
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        /// <param name="message">mesaj</param>
        /// <param name="headers"></param>
        /// <param name="priority"></param>
        public void Publish(string queueName, T message, Dictionary<string, object> headers, byte priority)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.Info("QueueName was null");
                    return;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName, 255))
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.Headers = headers;
                    properties.Priority = priority;
                    channel.BasicPublish(string.Empty, queueName, properties, Encoding.UTF8.GetBytes(jsonData));
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, "RabbitMQPublisher");
            }
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        /// <param name="message">mesaj</param>
        /// <param name="headers"></param>
        /// <param name="priority"></param>
        public void Publish(string queueName, T message, byte priority)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.Info("QueueName was null");
                    return;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName, 255))
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.Priority = priority;
                    channel.BasicPublish(string.Empty, queueName, properties, Encoding.UTF8.GetBytes(jsonData));
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, "RabbitMQPublisher");
            }
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        public uint MessageCount(string queueName)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.Info("QueueName was null");
                    return 0;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName))
                {
                    return channel.MessageCount(queueName);
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, "RabbitMQPublisher");
            }

            return 0;
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        public uint MessageCount(string queueName, byte priority)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.Info("QueueName was null");
                    return 0;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName, priority))
                {
                    return channel.MessageCount(queueName);
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, "RabbitMQPublisher");
            }

            return 0;
        }

        #endregion
    }
}
