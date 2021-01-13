using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace DMicroservices.DataAccess.Repository
{
    public interface IRepository<T> where T : class
    {
        /// <summary>
        /// Tüm veriyi getir.
        /// </summary>
        /// <returns></returns>
        IQueryable<T> GetAll();

        /// <summary>
        /// Veriyi Where metodu ile getir.
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        IQueryable<T> GetAll(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Veriyi Where metodu ile getir.
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="includePaths"></param>
        /// <returns></returns>
        IQueryable<T> GetAll(Expression<Func<T, bool>> predicate, List<string> includePaths);

        /// <summary>
        /// Verilen sorguya göre tablodaki sayıyı gönderir.
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        int Count();
        /// <summary>
        /// Verilen sorguya göre tablodaki sayıyı gönderir.
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        int Count(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// İstenilen veriyi single olarak getirir.
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        T Get(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// İstenilen veriyi single olarak getirir.
        /// </summary>
        /// <returns></returns>
        T Get(Expression<Func<T, bool>> predicate, List<string> includePaths);

        /// <summary>
        /// Getirilen veri üzerinde veri gelmeden kolonları seç.
        /// </summary>
        /// <param name="where">Veri kısıtlamaları</param>
        /// <param name="select">Seçilecek kolonlar</param>
        /// <returns></returns>
        IQueryable<dynamic> SelectList(Expression<Func<T, bool>> @where, Expression<Func<T, dynamic>> @select);

        /// <summary>
        /// Entity ile sql sorgusu göndermek için kullanılır.
        /// </summary>
        /// <param name="sqlQuery">Gönderilecek sql</param>
        /// <returns></returns>
        List<T> SendSql(string sqlQuery);

        /// <summary>
        /// Verilen entityi ekle.
        /// </summary>
        /// <param name="entity"></param>
        void Add(T entity);

        /// <summary>
        /// Verilen entityi ekle.
        /// </summary>
        /// <param name="entityList"></param>
        void BulkInsert(List<T> entityList);

        /// <summary>
        /// Verilen entity i güncelle.
        /// </summary>
        /// <param name="entity"></param>
        void Update(T entity);

        /// <summary>
        /// predicate göre veriler düzenlenir.
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="entity"></param>
        void Update(Expression<Func<T, bool>> predicate, T entity);

        /// <summary>
        /// Verilen entityi sil.
        /// </summary>
        /// <param name="entity"></param>
        void Delete(T entity, bool forceDelete = false);

        /// <summary>
        /// predicate göre veriler silinir.
        /// </summary>
        /// <param name="predicate"></param>
        void Delete(Expression<Func<T, bool>> predicate, bool forceDelete = false);


        /// <summary>
        /// Aynı kayıt eklememek için objeyi kontrol ederek true veya false dönderir.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate"></param>
        /// <returns></returns>
        bool Any(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// DbContext i verir.
        /// </summary> 
        /// <returns></returns>
        DbContext GetDbContext();

        List<string> GetIncludePaths();



    }
}


