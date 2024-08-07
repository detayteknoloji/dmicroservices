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
        public RabbitMqHealthCheck()
        {
        }
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (!RabbitMqConnection.Instance.IsConnected)
            {
                RabbitMQ.Client.IConnection rabbitConnection = null;
                var tryConnectionTask = new Task(() =>
                {
                    try
                    {
                        rabbitConnection = RabbitMqConnection.Instance.GetConnection();
                    }
                    catch
                    {
                        //ignored
                    }
                });
                tryConnectionTask.Start();
                tryConnectionTask.Wait(TimeSpan.FromSeconds(1));
                if (rabbitConnection == null || !rabbitConnection.IsOpen)
                {
                    ElasticLogger.Instance.Info($"RabbitMQ Connection Lost");
                    return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ Connection Lost"));
                }
                else
                {
                    if (_afterConnectionAction != null)
                        _afterConnectionAction.Invoke("Called /IsReady Method");
                }
            }

            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}
