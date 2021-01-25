using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Transactions;
using DMicroservices.DataAccess.History;
using DMicroservices.DataAccess.MongoRepository;
using DMicroservices.DataAccess.Repository;
using DMicroservices.Utils.Extensions;
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

        private readonly UnitOfWorkSettings _unitOfWorkSettings = new UnitOfWorkSettings()
        {
            ChangeDataCapture = false,
            ChangedUserPropertyName = "ChangedBy",
            IdPropertyName = "Id"
        };

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

        public UnitOfWork(UnitOfWorkSettings unitOfWorkSettings)
        {
            _unitOfWorkSettings = unitOfWorkSettings;
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


        /// <summary>
        /// UnitOfWork başlangıcı 
        /// </summary>
        public UnitOfWork(string filterPropertyName, object filterPropertyValue, UnitOfWorkSettings unitOfWorkSettings)
        {
            filteredRepository = true;
            FilterColumnName = filterPropertyName;
            FilterColumnValue = filterPropertyValue;
            _unitOfWorkSettings = unitOfWorkSettings;
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
            bool throwAnError =
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("THROW_UNIT_OF_WORK_ERROR"))
                    ? bool.Parse(Environment.GetEnvironmentVariable("THROW_UNIT_OF_WORK_ERROR"))
                    : false;
            int result = -1;
            try
            {
                using (TransactionScope tScope = new TransactionScope())
                {
                    List<HistoryCollectionModel> history = null;
                    if (_unitOfWorkSettings.ChangeDataCapture)
                        history = ChangeDataCapture();

                    result = DbContext.SaveChanges();
                    tScope.Complete();

                    if (history != null && history.Count > 0)
                        using (var mongoRepo = MongoRepositoryFactory.CreateMongoRepository<HistoryCollectionModel>())
                        {
                            mongoRepo.BulkInsert(history);
                        }
                }
            }
            catch (ValidationException ex)
            {
                string errorString = ex.Message;
                ErrorMessageList.Add(errorString);
                if (throwAnError)
                    throw;
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
                if (throwAnError)
                    throw;
            }
            catch (Exception ex)
            {
                ErrorMessageList.Add(ex.Message);
                if (throwAnError)
                    throw;
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

        private List<HistoryCollectionModel> ChangeDataCapture()
        {
            List<HistoryCollectionModel> historyCollection = new List<HistoryCollectionModel>();
            var changeTrack = DbContext.ChangeTracker.Entries()
                .Where(p => p.State == EntityState.Deleted || p.State == EntityState.Modified);
            foreach (var entry in changeTrack)
            {
                if (entry.Entity != null)
                {
                    HistoryCollectionModel historyModel = new HistoryCollectionModel();
                    historyModel.DatabaseName = DbContext.Database.GetDbConnection().Database;
                    historyModel.ObjectName = DbContext.Entry(entry.Entity).Metadata.Name;
                    historyModel.ChangeType = entry.State.ToString();
                    historyModel.DateTime = DateTime.Now;
                    historyModel.Columns = new List<HistoryTableColumnsModel>();

                    foreach (var prop in entry.OriginalValues.Properties)
                    {
                        if (string.Equals(prop.Name, _unitOfWorkSettings.ChangedUserPropertyName, StringComparison.OrdinalIgnoreCase))
                        {
                            historyModel.ChangedUser = entry.OriginalValues[prop] != null
                                ? entry.OriginalValues[prop].ToString()
                                : "UNKNOWN";
                        }

                        if (string.Equals(prop.Name, _unitOfWorkSettings.IdPropertyName, StringComparison.OrdinalIgnoreCase))
                        {
                            historyModel.RowId = entry.OriginalValues[prop] != null
                                ? entry.OriginalValues[prop].ToString()
                                : "UNKNOWN";
                        }
                    }

                    historyModel.Hash = CryptoExtensions.Md5Encrypt(JsonConvert.SerializeObject(entry.Entity));

                    switch (entry.State)
                    {
                        case EntityState.Modified:

                            foreach (var prop in entry.OriginalValues.Properties)
                            {
                                object currentValue = entry.CurrentValues[prop];
                                object originalValue = entry.OriginalValues[prop];
                                if (!object.Equals(currentValue, originalValue))
                                {
                                    historyModel.Columns.Add(new HistoryTableColumnsModel()
                                    {
                                        Name = prop.Name,
                                        OldValue = originalValue,
                                        NewValue = currentValue
                                    });
                                }
                            }

                            break;
                        case EntityState.Deleted:
                            foreach (var prop in entry.OriginalValues.Properties)
                            {
                                historyModel.Columns.Add(new HistoryTableColumnsModel()
                                {
                                    Name = prop.Name,
                                    OldValue = entry.CurrentValues[prop]
                                });
                            }
                            break;
                    }
                    if (historyModel.Columns.Count > 0)
                    {
                        historyCollection.Add(historyModel);
                    }
                }
            }

            return historyCollection;
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
