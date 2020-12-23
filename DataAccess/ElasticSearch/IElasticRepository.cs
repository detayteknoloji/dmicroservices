using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nest;

namespace DMicroservices.DataAccess.ElasticSearch
{
    public interface IElasticRepository<T> where T : class
    {
        IndexResponse AddIndex(T model, string indexName = null);

        Task<IndexResponse> AddIndexAsync(T model, string indexName = null);

        BulkResponse BulkIndexList(List<T> model, string indexName = null);

        Task<BulkResponse> BulkIndexListAsync(List<T> model, string indexName = null);

        BulkAllObservable<T> BulkAllObservable(List<T> model, int backOffTime, int backOffRetries, int size, string indexName = null);

        DeleteResponse DeleteIndex(string indexName, string id);

        ISearchResponse<T> Search(Func<QueryContainerDescriptor<T>, QueryContainer> searchTerms, string indexName = null);
    }
}
