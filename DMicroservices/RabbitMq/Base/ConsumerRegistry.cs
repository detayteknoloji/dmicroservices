using DMicroservices.RabbitMq.Consumer;
using DMicroservices.RabbitMq.Model;
using DMicroservices.Utils.Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DMicroservices.RabbitMq.Base
{
    public class ConsumerRegistry
    {
        private Dictionary<string, IConsumer> Consumers { get; set; }

        #region Singleton Section
        private static readonly Lazy<ConsumerRegistry> _instance = new Lazy<ConsumerRegistry>(() => new ConsumerRegistry());

        private ConsumerRegistry()
        {
            Consumers = new Dictionary<string, IConsumer>();
        }

        public static ConsumerRegistry Instance => _instance.Value;
        #endregion

        public void RegisterWithList(List<ConsumerActiveModel> consumerList)
        {
            foreach (var i in consumerList)
            {
                Register(i.Type, i.ParallelismCount);
            }
        }

        public void UnRegisterWithList(List<ConsumerActiveModel> consumerList, params string[] consumerIgnores)
        {
            List<string> unRegisterKeyList = new List<string>();
            List<string> nameList = new List<string>();
            foreach (var activeConsumer in consumerList)
            {
                for (int i = 0; i <= activeConsumer.ParallelismCount; i++)
                {
                    nameList.Add($"{activeConsumer.Type.FullName}_{i}");
                }
            }

            foreach (var consumer in Consumers.Keys.Where(x => !nameList.Contains(x) && !consumerIgnores.Contains(x)))
            {
                UnRegister(consumer);
            }

            foreach (var consumer in unRegisterKeyList)
            {
                UnRegister(consumer);
            }
        }

        public void Register(Type consumer, int parallelismCount = 0)
        {
            if (consumer.GetInterfaces().Length == 0 || consumer.GetInterfaces().Any(x => x.GetInterface("IConsumer") != null))
                throw new Exception("Consumer must be implement IConsumer.");
            Console.WriteLine($"Called to Register! {consumer.FullName}");
            for (int i = 0; i <= parallelismCount; i++)
            {
                try
                {
                    string consName = $"{consumer.FullName}_{i}";
                    Console.WriteLine($"Register has been named! {consName}");

                    if (Consumers.All(keyValue => keyValue.Key != consName))
                    {
                        var consumerObject = (IConsumer)Activator.CreateInstance(consumer);

                        lock (Consumers)
                        {
                            Console.WriteLine("Consumer register new request with: " + consName);
                            Consumers.Add(consName, consumerObject);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No condition! Key already exists! Consumer will be sent to start! ConsName : {consName} ConsumerList: {JsonConvert.SerializeObject(Consumers.Keys)} ");
                    }

                    Consumers[consName].StartConsume();
                }
                catch (Exception e)
                {
                    ElasticLogger.Instance.Error(e, $"ConsumerRegistry throw an error : {e.Message}");
                }
            }
        }

        public void UnRegister(string consumerKey)
        {
            lock (Consumers)
            {
                if (Consumers.Keys.Any(p => p == consumerKey))
                    Consumers[consumerKey].StopConsume();
            }
        }

        public void ClearAllRegisters(params string[] consumerIgnores)
        {
            lock (Consumers)
            {
                var consumerList = Consumers.Where(x => consumerIgnores.All(m => x.Key != null && !x.Key.Equals(m))).ToList();

                List<Task> stopConsumeTaskList = new List<Task>();
                foreach (var consumerItem in consumerList)
                {
                    stopConsumeTaskList.Add(consumerItem.Value.StopConsume());
                }

                Task.WaitAll(stopConsumeTaskList.ToArray());
            }
        }
    }
}
