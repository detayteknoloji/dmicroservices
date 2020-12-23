using DMicroservices.DataAccess.MongoRepository.Interfaces;

namespace DMicroservices.DataAccess.MongoRepository.Settings
{
    public class DatabaseSettings : IDatabaseSettings
    {
        public string ConnectionString { get; set; }

        public string DatabaseName { get; set; }

        public string CollectionName { get; set; }

        public int? CompanyNo { get; set; }
    }
}
