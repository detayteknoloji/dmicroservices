using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DMicroservices.RabbitMq.Consumer;
using DMicroservices.RabbitMq.Producer;
using DMicroservices.Utils.Logger;

namespace DMicroservices.RabbitMq.Base
{
    public class ConsumerRegistry
    {
        private Dictionary<Type, IConsumer> Consumers { get; set; }

        #region Singleton Section
        private static readonly Lazy<ConsumerRegistry> _instance = new Lazy<ConsumerRegistry>(() => new ConsumerRegistry());

        private ConsumerRegistry()
        {
            Consumers = new Dictionary<Type, IConsumer>();
        }

        public static ConsumerRegistry Instance => _instance.Value;
        #endregion

        public void Register(Type consumer)
        {
            if (consumer.GetInterfaces().Length == 0 || consumer.GetInterfaces().Any(x => x.GetInterface("IConsumer") != null))
                throw new Exception("Consumer must be implement IConsumer.");

            try
            {
                if (Consumers.All(keyValue => keyValue.Key != consumer))
                {
                    //register
                    var consumerObject = (IConsumer)Activator.CreateInstance(consumer);

                    lock (Consumers)
                    {
                        Consumers.Add(consumer, consumerObject);
                    }
                }
                Consumers[consumer].StartConsume();

            }
            catch (Exception e)
            {
                ElasticLogger.Instance.Error(e, $"ConsumerRegistry throw an error : {e.Message}");
            }
        }

        public void UnRegister(Type consumer)
        {
            lock (Consumers)
            {
                Consumers[consumer].StopConsume();
            }
        }

        public void ClearAllRegisters(params Type[] consumerIgnores)
        {
            lock (Consumers)
            {
                var consumerList = Consumers
                    .Where(x => consumerIgnores.All(m => x.Key.FullName != null && !x.Key.FullName.Equals(m.FullName))).ToList();

                foreach (var consumerItem in consumerList)
                {
                    consumerItem.Value.StopConsume();
                }
            }
        }
    }
}
