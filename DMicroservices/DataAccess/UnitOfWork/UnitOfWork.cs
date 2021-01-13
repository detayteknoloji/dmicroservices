using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Transactions;
using DMicroservices.DataAccess.Repository;
using DMicroservices.Utils.Logger;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace DMicroservices.DataAccess.UnitOfWork
{
    public class UnitOfWork<T> : IDisposable, IUnitOfWork
        where T : DbContext
    {
        #region Members

        private DbContext dbContext;
        private bool disposed = false;
        private bool filteredRepository = false;
        public string FilterColumnName { get; set; }
        public object FilterColumnValue { get; set; }
        /// <summary>
        /// İşlemlerde hata oluşusa bu liste doldurulur.
        /// </summary>
        public readonly List<string> ErrorMessageList = new List<string>();

        #endregion

        #region Properties

        /// <summary>
        /// Açılan veri bağlantısı.
        /// </summary>
        private DbContext DbContext
        {
            get
            {
                if (dbContext == null)
                {
                    dbContext = (DbContext)Activator.CreateInstance(typeof(T));
                }
                return dbContext;
            }
            set { dbContext = value; }
        }

        #endregion

        #region Constructre

        /// <summary>
        /// UnitOfWork başlangıcı 
        /// </summary>
        public UnitOfWork()
        {

        }

        /// <summary>
        /// UnitOfWork başlangıcı 
        /// </summary>
        public UnitOfWork(string filterPropertyName, object filterPropertyValue)
        {
            filteredRepository = true;
            FilterColumnName = filterPropertyName;
            FilterColumnValue = filterPropertyValue;
        }

        #endregion

        #region IUnitOfWork Members

        /// <summary>
        /// Repository instance'ı başlatmak için kullanılır.
        /// </summary>
        /// <typeparam name="T">Veri Tabanı Tür Nesnesi</typeparam>
        /// <returns>Tür nesnesi ile ilgili Repository</returns>
        public IRepository<T> GetRepository<T>() where T : class
        {
            if (filteredRepository)
                return new FilteredRepository<T>(DbContext, FilterColumnName, FilterColumnValue);

            return new Repository<T>(DbContext);
        }

        /// <summary>
        /// Değişiklikleri kaydet.
        /// </summary>
        /// <returns></returns>
        public int SaveChanges()
        {
            int result = -1;
            try
            {
                using (TransactionScope tScope = new TransactionScope())
                {
                    result = DbContext.SaveChanges();
                    tScope.Complete();
                }
            }
            catch (ValidationException ex)
            {
                string errorString = ex.Message;
                ErrorMessageList.Add(errorString);
            }
            catch (DbUpdateException ex)
            {
                string errorString = ex.Message;
                if (ex.InnerException != null)
                {
                    errorString += ex.InnerException.Message;
                    if (ex.InnerException.InnerException != null)
                    {
                        errorString += ex.InnerException.InnerException.Message;
                    }
                }

                ErrorMessageList.Add(errorString);
            }
            catch (Exception ex)
            {
                ErrorMessageList.Add(ex.Message);
            }
            finally
            {
                if (result == -1)
                {
                    ElasticLogger.Instance.Info(
                        $"UnitOfWork Save Error. Type : {typeof(T).Name} Error Messages : {JsonConvert.SerializeObject(ErrorMessageList)}");
                }
            }
            return result;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            DbContext = null;
        }
        #endregion
    }
}
