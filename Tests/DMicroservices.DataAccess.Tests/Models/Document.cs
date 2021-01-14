using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace DMicroservices.DataAccess.Tests.Models
{
    public class Document
    {
        [BsonId]
        public string Id { get; set; }

        public byte[] Data { get; set; }
    }
}
