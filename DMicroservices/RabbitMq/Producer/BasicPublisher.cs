using System;
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
        public void Publish(string queueName, T message)
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
                    channel.BasicPublish(string.Empty, queueName, null, Encoding.UTF8.GetBytes(jsonData));
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, "RabbitMQPublisher");
            }
        }

        #endregion
    }
}
