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
    }
}
