using Jellyfin.Plugin.JavScraper.Scrapers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Test.Scrapers
{
    [TestClass]
    public class ScraperTest
    {
        [DataTestMethod]
        [TestCategory("IgnoreOnBuild")]
        [DataRow(typeof(AvsoxScraper), "032416_525")]
        [DataRow(typeof(FC2Scraper), "FC2-2543981")]
        [DataRow(typeof(Jav123Scraper), "midv00119")]
        [DataRow(typeof(Jav123Scraper), "jul-535")]
        [DataRow(typeof(Jav123Scraper), "ddhp-001")]
        [DataRow(typeof(JavBusScraper), "midv-119")]
        [DataRow(typeof(JavBusScraper), "080521-004")]
        [DataRow(typeof(JavDbScraper), "midv-119")]
        [DataRow(typeof(JavDbScraper), "080521-004")]
        [DataRow(typeof(MgsTageScraper), "ABW-250")]
        [DataRow(typeof(R18Scraper), "ssis-335")]
        public void TestScraper(Type type, string id)
        {
            Assert.IsNotNull(type.FullName);
            var args = new object[]
            {
                Holder.GetLoggerFactory().CreateLogger(type), Holder.GetHttpClientManager(), Holder.GetDmmService()
            };
            var createdObject = type.Assembly.CreateInstance(type.FullName, false, System.Reflection.BindingFlags.CreateInstance, null, args, null, null);
            if (createdObject is not IScraper scraper)
            {
                Assert.Fail("test class should be scraper.");
                return;
            }
            var searchResult = scraper.Search(id).Result;
            Assert.AreNotEqual(0, searchResult.Count);
            Console.WriteLine(searchResult[0]);
            var detail = scraper.GetJavVideo(searchResult[0]).Result;
            Assert.IsNotNull(detail);
            Console.WriteLine(detail);
        }
    }
}
