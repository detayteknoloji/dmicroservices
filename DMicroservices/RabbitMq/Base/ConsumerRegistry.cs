using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DMicroservices.RabbitMq.Consumer;
using DMicroservices.RabbitMq.Producer;
using DMicroservices.Utils.Logger;

namespace DMicroservices.RabbitMq.Base
{
    public class ConsumerRegistry
    {
        private List<IConsumer> ConsumerList { get; set; }

        #region Singleton Section
        private static readonly Lazy<ConsumerRegistry> _instance = new Lazy<ConsumerRegistry>(() => new ConsumerRegistry());

        private ConsumerRegistry()
        {
            ConsumerList = new List<IConsumer>();
        }

        public static ConsumerRegistry Instance => _instance.Value;
        #endregion

        public void Register(Type consumer)
        {
            if (consumer.GetInterfaces().Length == 0 || consumer.GetInterfaces().Any(x => x.GetInterface("IConsumer") != null))
                throw new Exception("Consumer must be implement IConsumer.");

            if (ConsumerList.Any(x => x.GetType() == consumer))
                throw new Exception("Consumer already registered.");

            try
            {
                var consumerObject = (IConsumer)Activator.CreateInstance(consumer);
                ConsumerList.Add(consumerObject);
            }
            catch (Exception e)
            {
                ElasticLogger.Instance.Error(e, $"ConsumerRegistry throw an error : {e.Message}");
            }
        }

        public void UnRegister(Type consumer)
        {
            var consumerObject = ConsumerList.FirstOrDefault(x => x.GetType() == consumer);

            if (consumerObject == null)
                throw new Exception("Consumer not registered.");

            consumerObject.Dispose();
            ConsumerList.Remove(consumerObject);
        }

        public void ClearAllRegisters(params Type[] consumerIgnores)
        {
            if (consumerIgnores == null)
                throw new Exception("ConsumerIgnores cannot be null.");

            List<IConsumer> consumerList = ConsumerList.Where(x => !consumerIgnores.Any(m => x.GetType() == m)).ToList();

            consumerList.ForEach(x => Task.Run(() => { UnRegister(x.GetType()); }));
        }
    }
}
