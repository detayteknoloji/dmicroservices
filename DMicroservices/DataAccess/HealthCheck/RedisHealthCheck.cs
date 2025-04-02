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
            if (cancellationToken.IsCancellationRequested)
            {
                ElasticLogger.Instance.Info("Redis health check cancelled → Degraded returned");
                return Task.FromResult(HealthCheckResult.Degraded("Redis health check timeout (under load)"));
            }

            try
            {
                var value = RedisManager.Instance.Get("test");
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, "Redis Connection Lost");
                return Task.FromResult(HealthCheckResult.Unhealthy("Redis Connection Lost"));
            }

            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}
