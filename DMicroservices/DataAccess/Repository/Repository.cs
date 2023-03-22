using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DMicroservices.Base.Attributes;
using DMicroservices.DataAccess.History;
using DMicroservices.Utils.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace DMicroservices.DataAccess.Repository
{
    /// <summary>
    /// EntityFramework için hazırlıyor olduğumuz bu repositoriyi daha önceden tasarladığımız generic repositorimiz olan IRepository arayüzünü implemente ederek tasarladık.
    /// Bu şekilde tasarlamamızın ana sebebi ise veritabanına independent(bağımsız) bir durumda kalabilmek.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly DbContext DbContext;
        private readonly DbSet<T> DbSet;

        /// <summary>
        /// Repository instance ı başlatırç
        /// </summary>
        /// <param name="dbContext">Veritabanı bağlantı nesnesi</param>
        public Repository(DbContext dbContext)
        {
            DbContext = dbContext;
            DbSet = dbContext.Set<T>();
        }

        public void Add(T entity)
        {
            DbSet.Add(entity);
        }

        public void BulkInsert(List<T> entityList)
        {
            foreach (var entity in entityList)
            {
                DbSet.Add(entity);
            }
        }

        /// <summary>
        /// Aynı kayıt eklememek için objeyi kontrol ederek true veya false dönderir.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public bool Any(Expression<Func<T, bool>> predicate)
        {
            return DbSet.Any(predicate);
        }

        /// <summary>
        /// Aynı kayıt eklememek için objeyi varsa alt ilişkisiyle birlikte kontrol ederek true veya false dönderir.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate">Sorgu</param>
        /// <param name="includePaths">Join nesnelerinin classı</param>
        /// <returns></returns>
        public bool Any(Expression<Func<T, bool>> predicate, List<string> includePaths)
        {
            return DbSet.Include(includePaths).Any(predicate);
        }

        public DbContext GetDbContext()
        {
            return DbContext;
        }

        public List<string> GetIncludePaths()
        {
            return DbContext.GetIncludePaths(typeof(T)).ToList();
        }

        public int Count()
        {
            return Count(arg => true);
        }

        public int Count(Expression<Func<T, bool>> predicate)
        {
            IQueryable<T> iQueryable = DbSet
            .Where(predicate);
            return iQueryable.Count();
        }

        public int Count(Expression<Func<T, bool>> predicate, List<string> includePaths)
        {
            IQueryable<T> iQueryable = DbSet.Include(includePaths)
            .Where(predicate);
            return iQueryable.Count();
        }

        public void Delete(T entity, bool forceDelete = false)
        {
            // Önce entity'nin state'ini kontrol etmeliyiz.
            EntityEntry<T> dbEntityEntry = DbContext.Entry(entity);

            if (dbEntityEntry.State != EntityState.Deleted)
            {
                dbEntityEntry.State = EntityState.Deleted;
            }
            else
            {
                DbSet.Attach(entity);
                DbSet.Remove(entity);
            }
        }

        public void Delete(Expression<Func<T, bool>> predicate, bool forceDelete = false)
        {
            T model = DbSet.FirstOrDefault(predicate);

            if (model != null)
                Delete(model, forceDelete);
        }
        public void BulkDelete(List<T> entityList)
        {
            foreach (var entity in entityList)
            {
                DbSet.Remove(entity);
            }
        }

        public T Get(Expression<Func<T, bool>> predicate)
        {
            IQueryable<T> iQueryable = DbSet
                .Where(predicate);
            return iQueryable.ToList().FirstOrDefault();
        }

        public T Get(Expression<Func<T, bool>> predicate, List<string> includePaths)
        {
            IQueryable<T> iQueryable = DbSet
                .Where(predicate).Include(includePaths);
            return iQueryable.ToList().FirstOrDefault();
        }

        public IQueryable<T> GetAll()
        {
            IQueryable<T> iQueryable = DbSet.Where(x => x != null);
            return iQueryable;
        }

        public IQueryable<T> GetAll(System.Linq.Expressions.Expression<Func<T, bool>> predicate)
        {
            IQueryable<T> iQueryable = DbSet
                .Where(predicate);
            return iQueryable;
        }

        public IQueryable<T> GetAll(System.Linq.Expressions.Expression<Func<T, bool>> predicate, List<string> includePaths)
        {
            IQueryable<T> iQueryable = DbSet
                .Where(predicate).Include(includePaths);
            return iQueryable;
        }

        public IQueryable<dynamic> SelectList(Expression<Func<T, bool>> where, Expression<Func<T, dynamic>> select)
        {
            throw new NotImplementedException();
        }

        public List<T> SendSql(string sqlQuery)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Verilen veriyi context üzerinde günceller.
        /// </summary>
        /// <param name="entity">Güncellenecek entity</param>
        public void Update(T entity)
        {
            DbSet.Attach(entity);
            DbContext.Entry(entity).State = EntityState.Modified;

            foreach (var propertyEntry in DbContext.Entry(entity).Properties)
            {
                foreach (var customAttribute in propertyEntry.Metadata.PropertyInfo.GetCustomAttributes())
                {
                    if (customAttribute.TypeId.Equals(typeof(DisableChangeTrackAttribute)))
                    {
                        propertyEntry.IsModified = false;
                    }
                }
            }
        }

        public void UpdateProperties(T entity, params string[] changeProperties)
        {
            DbSet.Attach(entity);
            DbContext.Entry(entity).State = EntityState.Modified;

            foreach (var propertyEntry in DbContext.Entry(entity).Properties)
            {
                if (!changeProperties.Contains(propertyEntry.Metadata.PropertyInfo.Name))
                    propertyEntry.IsModified = false;

                foreach (var customAttribute in propertyEntry.Metadata.PropertyInfo.GetCustomAttributes())
                {
                    if (customAttribute.TypeId.Equals(typeof(DisableChangeTrackAttribute)))
                    {
                        propertyEntry.IsModified = false;
                    }
                }
            }
        }

        public void Update(Expression<Func<T, bool>> predicate, T entity)
        {
            throw new NotImplementedException();
        }

    }
}
