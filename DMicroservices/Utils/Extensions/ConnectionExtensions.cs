using System;

namespace DMicroservices.Utils.Extensions
{
    public static class ConnectionExtensions
    {
        public static string GetConnectionString()
        {
            return string.Format("server={0};port={1};user={2};password={3};database={4}",
                Environment.GetEnvironmentVariable("MYSQL_IP")
                , Environment.GetEnvironmentVariable("MYSQL_PORT")
                , Environment.GetEnvironmentVariable("MYSQL_USER")
                , Environment.GetEnvironmentVariable("MYSQL_PASWORD")
                , Environment.GetEnvironmentVariable("MYSQL_DB"));
        }
    }
}
