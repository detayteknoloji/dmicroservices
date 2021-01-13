using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DMicroservices.DataAccess.MongoRepository.Base
{
    public abstract class MongoBaseModel
    {
        [BsonId]
        public ObjectId Id { get; set; }
    }
}
