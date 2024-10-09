using System;

namespace DMicroservices.RabbitMq.Base
{

    public class ListenQueueAttribute : Attribute
    {
        public string ListenQueue { get; set; }
        public ListenQueueAttribute(string listenQueue)
        {
            ListenQueue = listenQueue;
        }

        /// <summary>
        /// add HOSTNAME after queuename
        /// </summary>
        /// <param name="listenQueue"></param>
        /// <param name="addHostnameEnvironment"></param>
        public ListenQueueAttribute(string listenQueue, bool addHostnameEnvironment)
        {
            if (addHostnameEnvironment)
            {
                ListenQueue = listenQueue + Environment.GetEnvironmentVariable("HOSTNAME");
            }
            else
            {
                ListenQueue = listenQueue;
            }
        }

        /// <summary>
        /// get from static property.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="property"></param>
        public ListenQueueAttribute(Type type, string propertyName)
        {

            foreach (var property in type.GetProperties())
            {
                if (property.Name.Equals(propertyName))
                {
                    ListenQueue = property.GetValue(null)?.ToString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(ListenQueue))
            {
                throw new Exception($"{propertyName} not found in type : {type.FullName}");
            }




        }
    }
}
