using System;
using DMicroservices.DataAccess.Repository;

namespace DMicroservices.DataAccess.UnitOfWork
{
   public  interface IUnitOfWork : IDisposable
    {
        IRepository<T> GetRepository<T>() where T : class;
        IRepository<T> GetReadonlyRepository<T>() where T : class;
        int SaveChanges();
    }
}
