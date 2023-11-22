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
        private Dictionary<string, int> ConsumerParallelismCount { get; set; }

        #region Singleton Section
        private static readonly Lazy<ConsumerRegistry> _instance = new Lazy<ConsumerRegistry>(() => new ConsumerRegistry());

        private ConsumerRegistry()
        {
            Consumers = new Dictionary<string, IConsumer>();
            ConsumerParallelismCount = new Dictionary<string, int>();
        }

        public static ConsumerRegistry Instance => _instance.Value;
        #endregion


        public void RegisterWithList(List<Type> consumerList)
        {
            foreach (var consumer in consumerList.Where(x => !Consumers.Values.Select(y => y.GetConsumerKey()).Contains($"{x.FullName}_0")))
            {
                Register(consumer);
            }
        }

        public void RegisterWithList(List<ConsumerActiveModel> consumerList)
        {
            foreach (var i in consumerList)
            {
                Register(i.Type, i.ParallelismCount);
            }
        }

        public void UnRegisterWithList(List<Type> consumerList, params Type[] consumerIgnores)
        {
            foreach (var consumer in Consumers.Keys.Where(x => consumerList.Select(y => $"{y.FullName}_0").Contains(x) && !consumerIgnores.Select(y => $"{y.FullName}_0").Contains(x)))
            {
                UnRegister(consumer);
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

            foreach (var consumer in Consumers.Keys.Where(x => nameList.Contains(x) && !consumerIgnores.Contains(x)))
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

            ElasticLogger.Instance.Info($"Called to Register! {consumer.FullName}");
            ConsumerParallelismCount.Add(consumer.FullName, parallelismCount);
            for (int i = 0; i <= parallelismCount; i++)
            {
                try
                {
                    string consumerKey = $"{consumer.FullName}_{i}";
                    RegisterConsumer(consumerKey, consumer);
                }
                catch (Exception e)
                {
                    ElasticLogger.Instance.Error(e, $"ConsumerRegistry throw an error : {e.Message}");
                }
            }
        }

        private void RegisterConsumer(string consumerKey, Type consumer)
        {
            ElasticLogger.Instance.Info($"Register has been named! {consumerKey}");

            if (Consumers.All(keyValue => keyValue.Key != consumerKey))
            {
                var consumerObject = (IConsumer)Activator.CreateInstance(consumer);

                lock (Consumers)
                {
                    ElasticLogger.Instance.Info("Consumer register new request with: " + consumerKey);
                    Consumers.Add(consumerKey, consumerObject);
                }
            }

            Consumers[consumerKey].StartConsume();
        }

        public void UnRegister(string consumerKey)
        {
            lock (Consumers)
            {
                if (Consumers.Keys.Any(p => p == consumerKey))
                    Consumers[consumerKey].StopConsume();
            }
        }

        public void UnRegister(Type consumer)
        {
            lock (Consumers)
            {
                Consumers[$"{consumer.FullName}_0"].StopConsume();
            }
        }

        public void ClearAllRegisters(params Type[] consumerIgnores)
        {
            lock (Consumers)
            {
                var consumerList = Consumers
                    .Where(x => consumerIgnores.All(m => x.Value.GetConsumerKey() != null && !x.Value.GetConsumerKey().Equals(m.FullName))).ToList();

                List<Task> stopConsumeTaskList = new List<Task>();
                foreach (var consumerItem in consumerList)
                {
                    stopConsumeTaskList.Add(consumerItem.Value.StopConsume());
                }

                Task.WaitAll(stopConsumeTaskList.ToArray());
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

        public void IncreaseParallelism(Type consumer)
        {
            int parallelismCount = ConsumerParallelismCount[consumer.FullName]++;
            string consumerKey = $"{consumer.FullName}_{parallelismCount + 1}";
            RegisterConsumer(consumerKey, consumer);
        }

        public void DecreaseParallelism(Type consumer)
        {
            int parallelismCount = ConsumerParallelismCount[consumer.FullName];
            if (parallelismCount == 0)
            {
                UnRegister(consumer);
            }
            else
            {
                string consumerKey = $"{consumer.FullName}_{parallelismCount}";
                UnRegister(consumerKey);
            }

            ConsumerParallelismCount[consumer.FullName]--;
        }

        public Dictionary<string, int> GetParallelismCount()
        {
            return ConsumerParallelismCount;
        }
    }

    public static class ConsumerExtensions
    {
        public static string GetConsumerKey(this IConsumer consumer)
        {
            return consumer.GetType().FullName;
        }
    }
}
