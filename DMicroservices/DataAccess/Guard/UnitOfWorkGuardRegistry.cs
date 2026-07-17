using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace DMicroservices.DataAccess.Guard
{
    /// <summary>
    /// Dmicroserviceyi kullanıpta UnitOfWorkGuardRegistry ile implement edilen <see cref="IEntitySaveGuard"/> implementasyonlarını tutar ve UnitOfWork.SaveChanges öncesinde dbye gidecek entityler üzerinde çalıştırır.
    /// </summary>
    public static class UnitOfWorkGuardRegistry
    {
        private static readonly ConcurrentBag<IEntitySaveGuard> Guards = new ConcurrentBag<IEntitySaveGuard>();

        public static bool HasGuards => !Guards.IsEmpty;

        public static void Register(IEntitySaveGuard guard)
        {
            if (guard == null)
                return;

            if (Guards.Any(p => p.GetType() == guard.GetType()))
                return;

            Guards.Add(guard);
        }

        public static void ValidateEntries(DbContext dbContext, string filterColumnName, object filterColumnValue, Type dbContextType)
        {
            if (Guards.IsEmpty || dbContext == null)
                return;

            foreach (var entry in dbContext.ChangeTracker.Entries()
                         .Where(p => p.State == EntityState.Added || p.State == EntityState.Modified))
            {
                if (entry.Entity == null)
                    continue;

                foreach (IEntitySaveGuard guard in Guards)
                {
                    guard.Validate(entry.Entity, entry.State, filterColumnName, filterColumnValue, dbContextType);
                }
            }
        }
    }
}
