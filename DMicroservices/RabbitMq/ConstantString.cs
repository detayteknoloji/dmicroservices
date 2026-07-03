using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DMicroservices.RabbitMq
{
    public static class ConstantString
    {
        public const string RABBITMQ_INDEX_FORMAT = "rabbit-serilog-{0:yyyy.MM}";
        public const string REDIS_LOG_INDEX_FORMAT = "redis-serilog-{0:yyyy.MM}";
    }
}
