using System;

namespace DMicroservices.DataAccess.UnitOfWork
{
    public interface ICustomDbContext
    {
        public string MYSQL_URI { get; set; }
    }
}
