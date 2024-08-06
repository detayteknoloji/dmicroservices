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
        private Action<string> _afterConnectionAction;
        public RabbitMqHealthCheck(Action<string> afterConnectionAction)
        {
            _afterConnectionAction = afterConnectionAction;
        }
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (!RabbitMqConnection.Instance.IsConnected)
            {
                var rabbitConnection = RabbitMqConnection.Instance.GetConnection();
                if (rabbitConnection == null || !rabbitConnection.IsOpen)
                {
                    ElasticLogger.Instance.Info($"RabbitMQ Connection Lost");
                    return Task.FromResult(HealthCheckResult.Unhealthy());
                }
                else
                {
                    if(_afterConnectionAction != null)
                        _afterConnectionAction.Invoke("Called /IsReady Method");
                }
            }

            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}
