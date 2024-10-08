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
    }
}
