using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nest;

namespace DMicroservices.DataAccess.ElasticSearch
{
    public class ElasticHelper<T> : IElasticRepository<T> where T : class
    {
        #region Singleton Section

        private static readonly Lazy<ElasticHelper<T>> _instance = new Lazy<ElasticHelper<T>>(() => new ElasticHelper<T>());

        private ElasticHelper()
        {
        }

        public static ElasticHelper<T> Instance => _instance.Value;

        #endregion

        #region Members

        private ConnectionSettings _connectionSettings;

        private ElasticClient _elasticClient;

        #endregion

        #region Property
        private ConnectionSettings ConnectionSettings
        {
            get
            {
                if (_connectionSettings == null)
                {
                    string elasticUri = Environment.GetEnvironmentVariable("ELASTIC_URI");

                    if (string.IsNullOrEmpty(elasticUri))
                        throw new ArgumentNullException($"ELASTIC_URI environment variable can not be empty");

                    _connectionSettings = new ConnectionSettings(new Uri(elasticUri));
                }

                return _connectionSettings;
            }
        }

        private ElasticClient ElasticClient
        {
            get
            {
                if (_elasticClient == null)
                    _elasticClient = new ElasticClient(ConnectionSettings);
                return _elasticClient;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Elasticsearch'e verilen modeli indexler.
        /// </summary>
        /// <param name="model"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public IndexResponse AddIndex(T model, string indexName = null)
        {
            return ElasticClient.Index(model, p => p.Index(indexName?.ToLower() ?? typeof(T).Name.ToLower()));
        }

        /// <summary>
        /// Elasticsearch'e verilen modeli asenkron olarak indexler.
        /// </summary>
        /// <param name="model"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public Task<IndexResponse> AddIndexAsync(T model, string indexName = null)
        {
            return ElasticClient.IndexAsync(model, p => p.Index(indexName?.ToLower() ?? typeof(T).Name.ToLower()));
        }

        /// <summary>
        /// Elasticsearch'e verilen model listesini ekler.
        /// </summary>
        /// <param name="model"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public BulkResponse BulkIndexList(List<T> model, string indexName = null)
        {
            if (!model.Any())
                throw new ArgumentNullException($"model cannot be null!");

            return ElasticClient.Bulk(p => p
                .Index(indexName?.ToLower() ?? typeof(T).Name.ToLower())
                .IndexMany(model)
            );
        }

        /// <summary>
        ///  Elasticsearch'e verilen model listesini asenkron olarak ekler.
        /// </summary>
        /// <param name="model"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public Task<BulkResponse> BulkIndexListAsync(List<T> model, string indexName = null)
        {
            if (!model.Any())
                throw new ArgumentNullException($"model cannot be null!");

            return ElasticClient.BulkAsync(p => p
                .Index(indexName?.ToLower() ?? typeof(T).Name.ToLower())
                .IndexMany(model)
            );
        }

        /// <summary>
        /// Elasticsearch'e verilen model listesini ekler.
        /// </summary>
        /// <param name="model"></param>
        /// <param name="backOffTime">Yeniden denemeden önce beklenilecek süre</param>
        /// <param name="backOffRetries">Hata alındığında deneme sayısı</param>
        /// <param name="size">Toplu istek başı gönderilecek öge sayısı</param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public BulkAllObservable<T> BulkAllObservable(List<T> model, int backOffTime, int backOffRetries, int size, string indexName = null)
        {
            if (!model.Any())
                throw new ArgumentNullException($"model cannot be null!");

            return ElasticClient.BulkAll(model, b => b
                .Index(indexName?.ToLower() ?? typeof(T).Name.ToLower())
                .BackOffTime(backOffTime)
                .BackOffRetries(backOffRetries)
                .MaxDegreeOfParallelism(Environment.ProcessorCount)
                .Size(size)
            );
        }

        /// <summary>
        /// Verilen index ismine göre gönderilen idyi siler.
        /// </summary>
        /// <param name="indexName"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public DeleteResponse DeleteIndex(string indexName, string id)
        {
            if (string.IsNullOrEmpty(indexName) || id == null)
            {
                throw new ArgumentNullException($"indexName and id cannot be null!");
            }

            return ElasticClient.Delete(new DeleteRequest(indexName, id));
        }

        /// <summary>
        /// Elasticsearchün kendi querysi ile gönderilen search querysi ile arama yapar.
        /// </summary>
        /// <param name="searchTerms"></param>
        /// <param name="indexName"></param>
        /// <returns></returns>
        public ISearchResponse<T> Search(Func<QueryContainerDescriptor<T>, QueryContainer> searchTerms, string indexName = null)
        {
            if (indexName == null)
                indexName = typeof(T).Name;

            return ElasticClient.Search<T>(p => p
                .Query(searchTerms)
                .Index(indexName.ToLower())
                .Size(20)
            );
        }

        #endregion
    }
}
