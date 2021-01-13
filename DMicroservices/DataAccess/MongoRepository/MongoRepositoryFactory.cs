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

        public static MongoRepository<T> CreateMongoRepository<T>(DatabaseSettings databaseSettings) where T : class, IMongoRepositoryCollection
        {
            return new MongoRepository<T>(databaseSettings);
        }
    }
}
