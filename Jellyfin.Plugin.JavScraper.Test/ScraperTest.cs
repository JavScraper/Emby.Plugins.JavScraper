using System.Linq.Expressions;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Scrapers;
using LiteDB;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jellyfin.Plugin.JavScraper.Scrapers.Test
{
    [TestClass]
    public class ScraperTest
    {
        Mock<ApplicationDbContext> applicationDbContextStub = new("test");
        Mock<ILiteCollection<Plot>> plotsStub = new();
        Mock<IHttpClientFactory> httpClientFactoryStub = new();

        [TestInitialize]
        public void beforeTest()
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36");
            plotsStub.Setup(x => x.Find(It.IsAny<Expression<Func<Plot, bool>>>(), It.IsAny<int>(), It.IsAny<int>()));
            plotsStub.Setup(x => x.Insert(It.IsAny<Plot>()));
            applicationDbContextStub.SetupGet(x => x.Plots).Returns(plotsStub.Object);
            httpClientFactoryStub.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
        }

        [TestMethod]
        public void TestAVSOXScraper()
        {
            var scraper = new AVSOXScraper(new LoggerFactory(), applicationDbContextStub.Object, httpClientFactoryStub.Object);
            var result = scraper.Query("032416_525").Result;
            Assert.AreNotEqual(0, result.Count);
        }

        [TestMethod]
        public void TestFC2Scraper()
        {
            var scraper = new FC2Scraper(new LoggerFactory(), applicationDbContextStub.Object, httpClientFactoryStub.Object);
            var result = scraper.Query("FC2-2543981").Result;
            Assert.AreNotEqual(0, result.Count);
        }

        [TestMethod]
        public void TestJav123Scraper()
        {
            var scraper = new Jav123Scraper(new LoggerFactory(), applicationDbContextStub.Object, httpClientFactoryStub.Object);
            var result = scraper.Query("midv00119").Result;
            Assert.AreNotEqual(0, result.Count);
        }
    }
}
