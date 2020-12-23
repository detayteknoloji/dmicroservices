namespace DMicroservices.DataAccess.MongoRepository.Interfaces
{
    public interface IDatabaseSettings
    {
        string ConnectionString { get; set; }

        string DatabaseName { get; set; }

        string CollectionName { get; set; }

        int? CompanyNo { get; set; }

    }
}
