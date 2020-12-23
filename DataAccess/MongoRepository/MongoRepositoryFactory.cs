using DMicroservices.DataAccess.MongoRepository.Interfaces;
using DMicroservices.DataAccess.MongoRepository.Settings;

namespace DMicroservices.DataAccess.MongoRepository
{
    public static class MongoRepositoryFactory
    {
        public static MongoRepository<T> CreateMongoRepository<T>() where T : class
        {
            return new MongoRepository<T>();
        }

        public static FilteredMongoRepository<T> CreateMongoRepository<T>(int? companyNo) where T : class, IMongoRepositoryCollection
        {
            return new FilteredMongoRepository<T>(companyNo);
        }

        public static FilteredMongoRepository<T> CreateMongoRepository<T>(DatabaseSettings databaseSettings) where T : class, IMongoRepositoryCollection
        {
            return new FilteredMongoRepository<T>(databaseSettings.CompanyNo, databaseSettings);
        }

        public static FilteredMongoRepository<T> CreateMongoRepository<T>(int companyNo, string collectionName) where T : class, IMongoRepositoryCollection
        {
            return new FilteredMongoRepository<T>(companyNo, new DatabaseSettings() { CollectionName = collectionName });
        }
    }
}
