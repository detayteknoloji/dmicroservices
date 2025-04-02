using DMicroservices.DataAccess.Redis;
using DMicroservices.Utils.Logger;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DMicroservices.RabbitMq.Base
{
    public class RabbitMqHealthCheck : IHealthCheck
    {
        private readonly Action<string> _afterConnectionAction;

        public RabbitMqHealthCheck(Action<string> afterConnectionAction)
        {
            _afterConnectionAction = afterConnectionAction;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                ElasticLogger.Instance.Info("RabbitMQ health check cancelled → Degraded returned");
                return HealthCheckResult.Degraded("RabbitMQ health check timeout (under load)");
            }

            if (!RabbitMqConnection.Instance.IsConnected)
            {
                try
                {
                    var task = Task.Run(() =>
                    {
                        var conn = RabbitMqConnection.Instance.GetConnection();
                        return conn != null && conn.IsOpen;
                    }, cancellationToken);

                    var isOpen = task.Wait(TimeSpan.FromSeconds(1).Milliseconds, cancellationToken);
                    if (!isOpen)
                    {
                        return HealthCheckResult.Unhealthy("RabbitMQ Connection Lost");
                    }

                    _afterConnectionAction?.Invoke("RabbitMQ ready");
                }
                catch (OperationCanceledException)
                {
                    ElasticLogger.Instance.Info("RabbitMQ health check task cancelled");
                    return HealthCheckResult.Degraded("RabbitMQ health check cancelled");
                }
                catch (Exception ex)
                {
                    ElasticLogger.Instance.Error(ex, "RabbitMQ Connection Exception");
                    return HealthCheckResult.Unhealthy("RabbitMQ error");
                }
            }

            return HealthCheckResult.Healthy();
        }
    }
}
