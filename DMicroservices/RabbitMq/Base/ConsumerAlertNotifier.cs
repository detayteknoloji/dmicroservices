using System;

namespace DMicroservices.RabbitMq.Base
{
    public static class ConsumerAlertNotifier
    {
        // implemente edilen yerden mail gönderilecek method bağlanır
        public static Action<string, string> DrainTimeoutAlert { get; set; }
    }
}
