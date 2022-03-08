using System.Collections.Generic;

namespace DMicroservices.RabbitMq.Model
{
    public class ExchangeContent
    {
        public string ExchangeName { get; set; }

        // örn: ExchangeType.Fanout
        public string ExchangeType { get; set; }

        public string RoutingKey { get; set; } = string.Empty;

        public Dictionary<string, object> Headers { get; set; }
    }
}
