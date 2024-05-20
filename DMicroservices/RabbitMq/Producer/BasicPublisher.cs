using DMicroservices.RabbitMq.Base;
using DMicroservices.Utils.Logger;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace DMicroservices.RabbitMq.Producer
{
    /// <summary>
    /// Rabbitmq publisher
    /// </summary>
    public class RabbitMqPublisher<T>
    {
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        #region Singleton Section
        private static readonly Lazy<RabbitMqPublisher<T>> _instance = new Lazy<RabbitMqPublisher<T>>(() => new RabbitMqPublisher<T>());

        private RabbitMqPublisher()
        {
            _jsonSerializerSettings = new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            };
        }

        public static RabbitMqPublisher<T> Instance => _instance.Value;

        #endregion

        #region Member

        private byte DeliveryMode = 2;
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
                    ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    return 0;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName))
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = DeliveryMode;
                    channel.BasicPublish(string.Empty, queueName, properties, Encoding.UTF8.GetBytes(jsonData));
                    return channel.MessageCount(queueName);
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQPublisher Error! RabbitMQPublisher Error! Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
            }

            return 0;
        }

        public bool PublishWithStatus(string queueName, T message)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    return false;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName))
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = DeliveryMode;
                    channel.BasicPublish(string.Empty, queueName, properties, Encoding.UTF8.GetBytes(jsonData));
                    return true;
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQPublisher Error! RabbitMQPublisher Error! Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
            }

            return false;
        }

        public bool PublishWithStatus(string queueName, T message, byte priority, byte channelPriority = 10)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    return false;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName, channelPriority))
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = DeliveryMode;
                    properties.Priority = priority;
                    channel.BasicPublish(string.Empty, queueName, properties, Encoding.UTF8.GetBytes(jsonData));
                    return true;
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQPublisher Error! RabbitMQPublisher Error! Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
            }

            return false;
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        /// <param name="messages">mesaj</param>
        public uint Publish(string queueName, List<T> messages)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"Message: {JsonConvert.SerializeObject(messages, _jsonSerializerSettings)} QueueName: {queueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    return 0;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName))
                {
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = DeliveryMode;

                    var batchPublish = channel.CreateBasicPublishBatch();
                    foreach (var message in messages)
                    {
                        string jsonData = JsonConvert.SerializeObject(message);
                        batchPublish.Add(string.Empty, queueName, true, properties, Encoding.UTF8.GetBytes(jsonData));
                    }
                    batchPublish.Publish();

                    return channel.MessageCount(queueName);
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQPublisher Error! RabbitMQPublisher Error! Message: {JsonConvert.SerializeObject(messages, _jsonSerializerSettings)} QueueName: {queueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
            }

            return 0;
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        /// <param name="messages">mesaj</param>
        public uint Publish(string queueName, List<string> messages, Dictionary<string, object> headers)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"Message: {JsonConvert.SerializeObject(messages, _jsonSerializerSettings)} QueueName: {queueName} Headers: {headers}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    return 0;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName))
                {
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = DeliveryMode;
                    properties.Headers = headers;

                    var batchPublish = channel.CreateBasicPublishBatch();
                    foreach (var message in messages)
                    {
                        batchPublish.Add(string.Empty, queueName, true, properties, Encoding.UTF8.GetBytes(message));
                    }
                    batchPublish.Publish();

                    return channel.MessageCount(queueName);
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQPublisher Error! RabbitMQPublisher Error! Message: {JsonConvert.SerializeObject(messages, _jsonSerializerSettings)} QueueName: {queueName} Headers: {headers}", ConstantString.RABBITMQ_INDEX_FORMAT);
            }

            return 0;
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        /// <param name="messages">mesaj</param>
        public bool PublishWithStatus(string queueName, List<string> messages, Dictionary<string, object> headers)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.Error(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"Message: {JsonConvert.SerializeObject(messages, _jsonSerializerSettings)} QueueName: {queueName} Headers: {headers}");
                    return false;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName))
                {
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = DeliveryMode;
                    properties.Headers = headers;

                    var batchPublish = channel.CreateBasicPublishBatch();
                    foreach (var message in messages)
                    {
                        batchPublish.Add(string.Empty, queueName, true, properties, Encoding.UTF8.GetBytes(message));
                    }
                    batchPublish.Publish();

                    return true;
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, $"RabbitMQPublisher Error! RabbitMQPublisher Error! Message: {JsonConvert.SerializeObject(messages, _jsonSerializerSettings)} QueueName: {queueName} Headers: {headers}");
            }

            return false;
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
                    ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName} Headers: {headers}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    return;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName))
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.Headers = headers;
                    properties.DeliveryMode = DeliveryMode;
                    channel.BasicPublish(string.Empty, queueName, properties, Encoding.UTF8.GetBytes(jsonData));
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQPublisher Error! RabbitMQPublisher Error! Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName} Headers: {headers}", ConstantString.RABBITMQ_INDEX_FORMAT);
            }
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        /// <param name="message">mesaj</param>
        /// <param name="headers"></param>
        public bool PublishWithStatus(string queueName, T message, Dictionary<string, object> headers)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.Error(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName} Headers: {headers}");
                    return false;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName))
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.Headers = headers;
                    properties.DeliveryMode = DeliveryMode;
                    channel.BasicPublish(string.Empty, queueName, properties, Encoding.UTF8.GetBytes(jsonData));
                    return true;
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, $"RabbitMQPublisher Error! RabbitMQPublisher Error! Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName} Headers: {headers}");
            }
            return false;
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        /// <param name="message">mesaj</param>
        /// <param name="headers"></param>
        /// <param name="priority"></param>
        public bool PublishWithStatus(string queueName, T message, Dictionary<string, object> headers, byte priority, byte channelPriority = 10)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.Error(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName} Headers: {headers} Priority: {priority}");
                    return false;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName, channelPriority))
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.Headers = headers;
                    properties.Priority = priority;
                    properties.DeliveryMode = DeliveryMode;
                    channel.BasicPublish(string.Empty, queueName, properties, Encoding.UTF8.GetBytes(jsonData));

                    return true;
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, $"RabbitMQPublisher Error! Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName} Headers: {headers} Priority: {priority}");
            }

            return false;
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        /// <param name="message">mesaj</param>
        /// <param name="headers"></param>
        /// <param name="priority"></param>
        public bool Publish(string queueName, T message, Dictionary<string, object> headers, byte priority, byte channelPriority = 10)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName} Headers: {headers} Priority: {priority}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    return false;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName, channelPriority))
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.Headers = headers;
                    properties.Priority = priority;
                    properties.DeliveryMode = DeliveryMode;
                    channel.BasicPublish(string.Empty, queueName, properties, Encoding.UTF8.GetBytes(jsonData));
                    return true;
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQPublisher Error! Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName} Headers: {headers} Priority: {priority}", ConstantString.RABBITMQ_INDEX_FORMAT);
            }

            return false;
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        /// <param name="message">mesaj</param>
        /// <param name="headers"></param>
        /// <param name="priority"></param>
        public void Publish(string queueName, T message, byte priority, byte channelPriority = 10)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName} Priority: {priority}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    return;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName, channelPriority))
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.Priority = priority;
                    properties.DeliveryMode = DeliveryMode;
                    channel.BasicPublish(string.Empty, queueName, properties, Encoding.UTF8.GetBytes(jsonData));
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQPublisher Error! Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} QueueName: {queueName} Priority: {priority}", ConstantString.RABBITMQ_INDEX_FORMAT);
            }
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="exchangeName">Exchange adı</param>
        /// <param name="key">Exchange için anahtar kelime</param>
        /// <param name="message">mesaj</param>
        public void PublishExchange(string exchangeName, string key, T message)
        {
            try
            {
                if (string.IsNullOrEmpty(exchangeName) || key == null)
                {
                    ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception("RabbitMQPublisher Error! ExchangeName or key was not null!"), $"Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} Exchange: {exchangeName} Key: {key}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    return;
                }
                using (IModel channel = RabbitMqConnection.Instance.Connection.CreateModel())
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = DeliveryMode;
                    channel.BasicPublish(exchangeName, key, properties, Encoding.UTF8.GetBytes(jsonData));
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQPublisher Error! Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} Exchange: {exchangeName} Key: {key}", ConstantString.RABBITMQ_INDEX_FORMAT);
            }
        }

        /// <summary>
        /// Aldığı mesajı aldığı kuyruğa yazar
        /// </summary>
        /// <param name="exchangeName">Exchange adı</param>
        /// <param name="key">Exchange için anahtar kelime</param>
        /// <param name="message">mesaj</param>
        /// <param name="headers">Header exchange için key value nesnesi</param>
        public void PublishExchange(string exchangeName, string key, T message, Dictionary<string, object> headers)
        {
            try
            {
                if (string.IsNullOrEmpty(exchangeName) || key == null || headers == null)
                {
                    ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception("RabbitMQPublisher Error! ExchangeName or key was not null!"), $"Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} Exchange: {exchangeName} Key: {key}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    return;
                }
                using (IModel channel = RabbitMqConnection.Instance.Connection.CreateModel())
                {
                    string jsonData = JsonConvert.SerializeObject(message);
                    IBasicProperties properties = channel.CreateBasicProperties();
                    properties.Headers = headers;
                    properties.DeliveryMode = DeliveryMode;
                    channel.BasicPublish(exchangeName, key, properties, Encoding.UTF8.GetBytes(jsonData));
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQPublisher Error! Message: {JsonConvert.SerializeObject(message, _jsonSerializerSettings)} Exchange: {exchangeName} Key: {key}", ConstantString.RABBITMQ_INDEX_FORMAT);
            }
        }

        /// <summary>
        /// Kuyruğun içerisinde kaç adet mesaj olduğunu döner.
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        public uint MessageCount(string queueName, byte channelPriority)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"QueueName: {queueName} Priority: {channelPriority}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    return 0;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName, channelPriority))
                {
                    return channel.MessageCount(queueName);
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQPublisher Error! QueueName: {queueName} Priority: {channelPriority}", ConstantString.RABBITMQ_INDEX_FORMAT);
            }

            return 0;
        }

        /// <summary>
        /// Kuyruğun içerisinde kaç adet mesaj olduğunu döner.
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        public uint MessageCount(string queueName)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.ErrorSpecificIndexFormat(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"QueueName: {queueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
                    return 0;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName))
                {
                    return channel.MessageCount(queueName);
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.ErrorSpecificIndexFormat(ex, $"RabbitMQPublisher Error! QueueName: {queueName}", ConstantString.RABBITMQ_INDEX_FORMAT);
            }

            return 0;
        }

        /// <summary>
        /// Kuyruğu dinleyen kaç adet consumer olduğunu döner.
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        public uint ConsumerCount(string queueName)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.Error(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"QueueName: {queueName}");
                    return 0;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName))
                {
                    return channel.ConsumerCount(queueName);
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, $"RabbitMQPublisher Error! QueueName: {queueName}");
            }

            return 0;
        }

        /// <summary>
        /// Kuyruğu dinleyen kaç adet consumer olduğunu döner.
        /// </summary>
        /// <param name="queueName">kuyruk adı</param>
        public uint ConsumerCount(string queueName, byte channelPriority)
        {
            try
            {
                if (string.IsNullOrEmpty(queueName))
                {
                    ElasticLogger.Instance.Error(new Exception("RabbitMQPublisher Error! QueueName was not null!"), $"QueueName: {queueName}");
                    return 0;
                }
                using (IModel channel = RabbitMqConnection.Instance.GetChannel(queueName, channelPriority))
                {
                    return channel.ConsumerCount(queueName);
                }
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, $"RabbitMQPublisher Error! QueueName: {queueName}");
            }

            return 0;
        }

        #endregion
    }
}
