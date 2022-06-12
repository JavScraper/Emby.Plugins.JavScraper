using System.Linq.Expressions;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Http;
using LiteDB;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jellyfin.Plugin.JavScraper.Scrapers.Test
{
    [TestClass]
    public class ScraperTest
    {
        readonly ILoggerFactory _loggerFactory = new LoggerFactory();
        readonly Mock<ApplicationDbContext> _applicationDbContextStub = new("test");
        readonly Mock<ILiteCollection<Plot>> _plotsStub = new();
        readonly Mock<ICustomHttpClientFactory> _httpClientFactoryStub = new();

        [TestInitialize]
        public void BeforeTest()
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36");
            _plotsStub.Setup(x => x.Find(It.IsAny<Expression<Func<Plot, bool>>>(), It.IsAny<int>(), It.IsAny<int>()));
            _plotsStub.Setup(x => x.Insert(It.IsAny<Plot>()));
            _applicationDbContextStub.SetupGet(x => x.Plots).Returns(_plotsStub.Object);
            _httpClientFactoryStub.Setup(x => x.GetClient()).Returns(httpClient);
        }

        [TestMethod]
        public void TestAVSOXScraper()
        {
            var scraper = new AVSOXScraper(_loggerFactory, _applicationDbContextStub.Object, _httpClientFactoryStub.Object);
            var searchResult = scraper.Query("032416_525").Result;
            Assert.AreNotEqual(0, searchResult.Count);
            var detail = scraper.GetJavVedio(searchResult[0]).Result;
            Assert.IsNotNull(detail);
        }

        [TestMethod]
        public void TestFC2Scraper()
        {
            var scraper = new FC2Scraper(_loggerFactory, _applicationDbContextStub.Object, _httpClientFactoryStub.Object);
            var result = scraper.Query("FC2-2543981").Result;
            Assert.AreNotEqual(0, result.Count);
        }

        [TestMethod]
        public void TestJav123Scraper()
        {
            var scraper = new Jav123Scraper(_loggerFactory, _applicationDbContextStub.Object, _httpClientFactoryStub.Object);
            var result = scraper.Query("midv00119").Result;
            Assert.AreNotEqual(0, result.Count);
        }
    }
}
