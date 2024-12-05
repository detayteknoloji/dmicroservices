using DMicroservices.Utils.Logger;

namespace DMicroservices.Utils.Tests
{
    public class ElasticLoggerTest
    {
        [Fact]
        public void InfoSpecificIndexFormatInFileTest()
        {
            ElasticLogger.Instance.InfoSpecificIndexFormatInFile("Deneme Logu", "slow-query", Environment.CurrentDirectory);
        }
    }
}