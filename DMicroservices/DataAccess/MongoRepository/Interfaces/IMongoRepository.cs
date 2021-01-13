using DMicroservices.DataAccess.DynamicQuery.Enum;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace DMicroservices.DataAccess.MongoRepository.Interfaces
{
    /// <summary>
    /// Model katmanımızda bulunan her T tipi için aşağıda tanımladığımız fonksiyonları gerçekleştirebilecek generic bir repository tanımlıyoruz.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IMongoRepository<T> where T : class
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
        /// Getirilen veri üzerinde veri gelmeden kolonları seç.
        /// </summary>
        /// <param name="where">Veri kısıtlamaları</param>
        /// <param name="select">Seçilecek kolonlar</param>
        /// <returns></returns>
        IQueryable<dynamic> SelectList(Expression<Func<T, bool>> @where, Expression<Func<T, dynamic>> @select);

        /// <summary>
        /// Verinin bir bölümünü getirir.
        /// </summary>
        ///  <param name="where">neye gore gelsin?</param>
        /// <param name="skipCount">Veri nerden baslasin?</param>
        /// <param name="takeCount">Kac veri gelsin</param>
        /// <param name="sort">Neye gore sortlansin</param>
        /// <param name="sortType">A|D (A->ascending | D->descending)</param>
        /// <returns></returns>
        IQueryable<T> GetDataPart(Expression<Func<T, bool>> where, Expression<Func<T, dynamic>> sort, SortTypeEnum sortType, int skipCount, int takeCount);

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
        bool Add(T entity);

        /// <summary>
        /// Verilen entity i güncelle.
        /// </summary>
        /// <param name="entity"></param>
        bool Update(T entity);

        /// <summary>
        /// predicate göre veriler düzenlenir.
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="entity"></param>
        bool Update(Expression<Func<T, bool>> predicate, T entity);

        /// <summary>
        /// Verilen entityi sil.
        /// </summary>
        /// <param name="entity"></param>
        bool Delete(T entity, bool forceDelete = false);

        /// <summary>
        /// predicate göre veriler silinir.
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="forceDelete"></param>
        bool Delete(Expression<Func<T, bool>> predicate, bool forceDelete = false);

        bool Delete<TField>(FieldDefinition<T, TField> field, TField date);

        /// <summary>
        /// Aynı kayıt eklememek için objeyi kontrol ederek true veya false dönderir.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate"></param>
        /// <returns></returns>
        bool Any(Expression<Func<T, bool>> predicate);


        bool Truncate();

        bool BulkInsert(List<T> entityList);

        bool BulkDelete(Expression<Func<T, bool>> predicate);

        bool AddAsync(T entity);

        bool UpdateAsync(Expression<Func<T, bool>> predicate, T entity);

        bool BulkInsertAsync(List<T> entityList);
    }
}
