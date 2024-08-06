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
        Type _dbContextType = null;
        public DatabaseHealthCheck(Type dbContextType)
        {
            _dbContextType = dbContextType;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                UnitOfWorkFactory.CreateUnitOfWork(_dbContextType).GetDbContext().Database.ExecuteSqlRaw("SELECT 1");
            }
            catch (Exception e)
            {
                ElasticLogger.Instance.Error(e, $"Mysql Connection Lost");
                return Task.FromResult(HealthCheckResult.Unhealthy());

            }

            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}
