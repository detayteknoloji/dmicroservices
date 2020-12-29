using DMicroservices.DataAccess.DynamicQuery.Enum;
using DMicroservices.DataAccess.MongoRepository.Interfaces;
using DMicroservices.DataAccess.MongoRepository.Settings;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DMicroservices.Utils.Logger;

namespace DMicroservices.DataAccess.MongoRepository
{
    public class MongoRepository<T> : IDisposable, IMongoRepository<T> where T : class
    {
        //All mongodb databases and collections generated from mongoclient and mongoclient itself is threadsafe. because of that caching is logical choice.
        private static readonly ReaderWriterLockSlim _databaseLocker = new ReaderWriterLockSlim();
        private static readonly ReaderWriterLockSlim _clientLocker = new ReaderWriterLockSlim();


        private static Dictionary<string, IMongoDatabase> Databases { get; set; } = new Dictionary<string, IMongoDatabase>();
        private static Dictionary<string, MongoClient> MongoClients { get; set; } = new Dictionary<string, MongoClient>();
        private IMongoDatabase database;

        public DatabaseSettings DatabaseSettings { get; set; } = new DatabaseSettings();


        public IMongoCollection<T> CurrentCollection { get; set; }

        public IMongoDatabase Database
        {
            get
            {
                if (database == null)
                    database = GetDatabase(DatabaseSettings);
                return database;
            }
            set { database = value; }
        }

        public MongoRepository()
        {
            Database = GetDatabase(DatabaseSettings);
            if (Database.GetCollection<T>(typeof(T).Name) == null)
            {
                Database.CreateCollection(typeof(T).Name);
            }

            CurrentCollection = GetCollection(DatabaseSettings);
        }

        public MongoRepository(DatabaseSettings dbSettings)
        {
            if (string.IsNullOrWhiteSpace(dbSettings.ConnectionString))
                dbSettings.ConnectionString = Environment.GetEnvironmentVariable("MONGO_URI");
            if (string.IsNullOrWhiteSpace(dbSettings.CollectionName))
                dbSettings.CollectionName = typeof(T).Name;
            if (string.IsNullOrWhiteSpace(dbSettings.DatabaseName))
                dbSettings.DatabaseName = Environment.GetEnvironmentVariable("MONGO_DB_NAME");

            DatabaseSettings = dbSettings;
            Database = GetDatabase(DatabaseSettings);
            if (Database.GetCollection<T>(typeof(T).Name) == null)
            {
                Database.CreateCollection(typeof(T).Name);
            }

            CurrentCollection = GetCollection(DatabaseSettings);
        }

        private IMongoCollection<T> GetCollection(IDatabaseSettings dbSettings)
        {
            return GetDatabase(dbSettings).GetCollection<T>(typeof(T).Name);
        }

        private IMongoDatabase GetDatabase(IDatabaseSettings dbSettings)
        {
            IMongoDatabase database = null;
            _databaseLocker.EnterReadLock();
            try
            {
                if (Databases.ContainsKey(dbSettings.DatabaseName))
                {
                    database = Databases[dbSettings.DatabaseName];
                }
                else
                {

                    _databaseLocker.ExitReadLock();
                    _databaseLocker.EnterWriteLock();
                    try
                    {
                        if (!MongoClients.ContainsKey(dbSettings.ConnectionString))
                        {
                            database = GetClient(dbSettings.ConnectionString).GetDatabase(dbSettings.DatabaseName);
                            Databases.Add(dbSettings.ConnectionString, database);
                        }
                        database = Databases[dbSettings.ConnectionString];
                    }
                    finally
                    {
                        _databaseLocker.ExitWriteLock();
                    }
                }
            }
            finally
            {
                if (_databaseLocker.IsReadLockHeld)
                    _databaseLocker.ExitReadLock();
            }
            return database;
        }

        private MongoClient GetClient(string connectionString)
        {
            MongoClient client = null;
            _clientLocker.EnterReadLock();
            try
            {
                if (MongoClients.ContainsKey(connectionString))
                {
                    client = MongoClients[connectionString];
                }
                else
                {
                    _clientLocker.ExitReadLock();
                    _clientLocker.EnterWriteLock();
                    try
                    {
                        if (!MongoClients.ContainsKey(connectionString))
                        {
                            client = new MongoClient(connectionString);
                            MongoClients.Add(connectionString, client);
                        }
                        client = MongoClients[connectionString];
                    }
                    finally
                    {
                        _clientLocker.ExitWriteLock();
                    }
                }
            }
            finally
            {
                if (_clientLocker.IsReadLockHeld)
                    _clientLocker.ExitReadLock();
            }
            return client;
        }

        public bool Add(T entity)
        {
            try
            {
                CurrentCollection.InsertOne(entity);
                return true;
            }
            catch (Exception ex)
            {
                string messageTemplate = $"Mongo adding error on :{typeof(T).Name}";
                ElasticLogger.Instance.Error(ex, messageTemplate);
                return false;
            }
        }

        public bool Delete(Expression<Func<T, bool>> predicate, bool forceDelete = false)
        {
            try
            {
                CurrentCollection.DeleteOne(predicate);
                return true;
            }
            catch (Exception ex)
            {
                string messageTemplate = $"Mongo deleting error on :{typeof(T).Name}";
                ElasticLogger.Instance.Error(ex, messageTemplate);
                return false;
            }
        }

        public bool Delete<TField>(FieldDefinition<T, TField> field, TField date)
        {
            try
            {
                FilterDefinition<T> filter = Builders<T>.Filter.Lte(field, date);
                CurrentCollection.DeleteMany(filter);
                return true;
            }
            catch (Exception ex)
            {
                string messageTemplate = $"Mongo delete many error on :{typeof(T).Name}";
                ElasticLogger.Instance.Error(ex, messageTemplate);
                return false;
            }
        }

        public bool Update(Expression<Func<T, bool>> predicate, T entity)
        {
            try
            {
                CurrentCollection.ReplaceOneAsync(predicate, entity);
                return true;
            }
            catch (Exception ex)
            {
                string messageTemplate = $"Mongo upate many error on :{typeof(T).Name}";
                ElasticLogger.Instance.Error(ex, messageTemplate);
                return false;
            }
        }

        public int Count(Expression<Func<T, bool>> predicate)
        {
            return (int)CurrentCollection.Find(predicate).CountDocuments();
        }

        public IQueryable<T> GetAll(Expression<Func<T, bool>> predicate)
        {
            return CurrentCollection.Find(predicate).ToEnumerable().AsQueryable();
        }

        /// <summary>
        /// Şarta göre tek veri getirir
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public T Get(Expression<Func<T, bool>> predicate)
        {
            return CurrentCollection.Find(predicate).FirstOrDefault();
        }

        /// <summary>
        /// Aynı kayıt eklememek için objeyi kontrol ederek true veya false dönderir.
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public bool Any(Expression<Func<T, bool>> predicate)
        {
            return CurrentCollection.Find(predicate).FirstOrDefault() != null;
        }

        IQueryable<T> IMongoRepository<T>.GetAll()
        {
            throw new NotImplementedException();
        }

        public int Count()
        {
            throw new NotImplementedException();
        }

        public IQueryable<dynamic> SelectList(Expression<Func<T, bool>> where, Expression<Func<T, dynamic>> select)
        {
            throw new NotImplementedException();
        }

        public IQueryable<T> GetDataPart(Expression<Func<T, bool>> where, Expression<Func<T, dynamic>> sort, SortTypeEnum sortType, int skipCount, int takeCount)
        {
            throw new NotImplementedException();
        }

        public List<T> SendSql(string sqlQuery)
        {
            throw new NotImplementedException();
        }

        public bool Delete(T entity, bool forceDelete = false)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }

        public bool Update(T entity)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> Query()
        {
            return CurrentCollection.Find(FilterDefinition<T>.Empty).ToList();
        }

        public bool Truncate()
        {
            FilterDefinition<T> completedFilter = Builders<T>.Filter.And(new[]
            {
                Builders<T>.Filter.Where(p => true),
            });

            DeleteResult result = CurrentCollection.DeleteMany(completedFilter);
            return (result.DeletedCount > 0);
        }

        public bool BulkDelete(Expression<Func<T, bool>> predicate)
        {
            FilterDefinition<T> completedFilter = Builders<T>.Filter.And(new[]
            {
                Builders<T>.Filter.Where(predicate),
            });

            DeleteResult result = CurrentCollection.DeleteMany(completedFilter);
            return (result.DeletedCount > 0);
        }

        public bool BulkInsert(List<T> entityList)
        {
            if (entityList.Any())
            {
                CurrentCollection.InsertMany(entityList);
                return true;
            }
            return false;
        }

        public bool AddAsync(T entity)
        {
            CurrentCollection.InsertOneAsync(entity);
            return true;
        }

        public bool UpdateAsync(Expression<Func<T, bool>> predicate, T entity)
        {
            Task<ReplaceOneResult> taskResult = CurrentCollection.ReplaceOneAsync(predicate, entity);

            if (taskResult.IsCompleted)
            {
                return (taskResult.Result.ModifiedCount > 0);
            }

            return true;
        }

        public bool BulkInsertAsync(List<T> entityList)
        {
            if (entityList.Any())
            {
                CurrentCollection.InsertManyAsync(entityList);
                return true;
            }
            return false;
        }

    }
}
