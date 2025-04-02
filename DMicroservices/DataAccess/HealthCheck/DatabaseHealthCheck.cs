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
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly Type _dbContextType;

        public DatabaseHealthCheck(Type dbContextType)
        {
            _dbContextType = dbContextType;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                ElasticLogger.Instance.Info("MySQL health check cancelled → Degraded returned");
                return HealthCheckResult.Degraded("MySQL health check timeout (under load)");
            }

            try
            {
                var db = UnitOfWorkFactory.CreateUnitOfWork(_dbContextType).GetDbContext();
                await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                ElasticLogger.Instance.Error(ex, "MySQL health check timeout");
                return HealthCheckResult.Degraded("MySQL health check cancelled");
            }
            catch (Exception ex)
            {
                ElasticLogger.Instance.Error(ex, "Mysql Connection Lost");
                return HealthCheckResult.Unhealthy("Mysql Connection Lost");
            }

            return HealthCheckResult.Healthy();
        }
    }
}
