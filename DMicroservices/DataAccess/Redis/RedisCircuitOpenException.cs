using System;

namespace DMicroservices.DataAccess.Redis
{
    public class RedisCircuitOpenException : Exception
    {
        public RedisCircuitOpenException(string message) : base(message) { }
    }
}
