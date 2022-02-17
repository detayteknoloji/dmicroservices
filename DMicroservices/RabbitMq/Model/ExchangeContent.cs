using System.Collections.Generic;

namespace DMicroservices.RabbitMq.Model
{
    public class ExchangeContent
    {
        public string Name { get; set; }

        public string Type { get; set; }

        public string Key { get; set; } = string.Empty;

        public Dictionary<string, object> Headers { get; set; }
    }
}
