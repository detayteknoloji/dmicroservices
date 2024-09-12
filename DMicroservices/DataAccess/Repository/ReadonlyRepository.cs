﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using DMicroservices.DataAccess.History;
using DMicroservices.Utils.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DMicroservices.DataAccess.Repository
{
    /// <summary>
    /// EntityFramework için hazırlıyor olduğumuz bu repositoriyi daha önceden tasarladığımız generic repositorimiz olan IRepository arayüzünü implemente ederek tasarladık.
    /// Bu şekilde tasarlamamızın ana sebebi ise veritabanına independent(bağımsız) bir durumda kalabilmek.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ReadonlyRepository<T> : IRepository<T> where T : class
    {
        private readonly DbContext DbContext;
        private readonly DbSet<T> DbSet;

        /// <summary>
        /// Repository instance ı başlatırç
        /// </summary>
        /// <param name="dbContext">Veritabanı bağlantı nesnesi</param>
        public ReadonlyRepository(DbContext dbContext)
        {
            DbContext = dbContext;
            DbSet = dbContext.Set<T>();
        }

        public void Add(T entity)
        {
            throw new NotImplementedException("This repository is read-only.");
        }

        public void BulkInsert(List<T> entityList)
        {
            throw new NotImplementedException("This repository is read-only.");
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

        public void Delete(T entity, bool forceDelete = false)
        {
            throw new NotImplementedException("This repository is read-only.");
        }

        public void Delete(Expression<Func<T, bool>> predicate, bool forceDelete = false)
        {
            throw new NotImplementedException("This repository is read-only.");
        }
        public void BulkDelete(List<T> entityList)
        {
            throw new NotImplementedException("This repository is read-only.");
        }

        public T Get(Expression<Func<T, bool>> predicate)
        {
            IQueryable<T> iQueryable = DbSet
                .Where(predicate);
            return iQueryable.ToList().FirstOrDefault();
        }

        public TResult Get<TResult>(Expression<Func<T, bool>> predicate, Expression<Func<T, TResult>> @select)
        {
            IQueryable<T> iQueryable = DbSet
                .Where(predicate);
            return iQueryable.Select(@select).FirstOrDefault();
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

        public IQueryable<TResult> SelectList<TResult>(Expression<Func<T, bool>> @where, Expression<Func<T, TResult>> @select)
        {
            IQueryable<TResult> iQueryable = DbSet
                .Where(@where).Select(@select);
            return iQueryable;
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
            throw new NotImplementedException("This repository is read-only.");
        }

        public void UpdateProperties(T entity, params string[] changeProperties)
        {
            throw new NotImplementedException("This repository is read-only.");
        }

        public void Update(Expression<Func<T, bool>> predicate, T entity)
        {
            throw new NotImplementedException("This repository is read-only.");
        }

        public int SendSqlScalar(string sqlQuery)
        {
            throw new NotImplementedException("This repository is read-only.");
        }

    }
}
