using DMicroservices.DataAccess.Redis;
using DMicroservices.DataAccess.UnitOfWork;
using DMicroservices.Utils.Logger;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DMicroservices.RabbitMq.Base
{
    public class RedisHealthCheck : IHealthCheck
    {

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                RedisManager.Instance.Get("test");
            }
            catch (Exception e)
            {
                ElasticLogger.Instance.Error(e, $"Redis Connection Lost");
                return Task.FromResult(HealthCheckResult.Unhealthy("Redis Connection Lost"));

            }

            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}
