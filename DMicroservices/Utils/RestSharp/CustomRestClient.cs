using System.Net;
using DMicroservices.Utils.Logger;
using RestSharp;

namespace DMicroservices.Utils.RestSharp
{
    public class CustomRestClient : RestClient
    {
        private string _baseUrl;
        public CustomRestClient(string baseUrl) : base(baseUrl)
        {
            _baseUrl = baseUrl;
        }

        public override IRestResponse Execute(IRestRequest request)
        {
            var result = base.Execute(request);
            if (result.StatusCode != HttpStatusCode.OK)
            {
                ElasticLogger.Instance.Info($"Request not OK. Requested Url : {_baseUrl} Status Code : {result.StatusCode} Content : {result.Content} ");
            }
            return result;
        }
    }
}
