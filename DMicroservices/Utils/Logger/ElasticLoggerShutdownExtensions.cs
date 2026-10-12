using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DMicroservices.Utils.Logger
{
    public static class ElasticLoggerShutdownExtensions
    {
        public static IApplicationBuilder UseElasticLoggerShutdown(this IApplicationBuilder app)
        {
            app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped
                .Register(() => ElasticLogger.CloseAndFlushIfCreated());
            return app;
        }
    }
}
