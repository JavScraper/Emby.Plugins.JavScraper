using Jellyfin.Plugin.JavScraper.Scrapers;

namespace Jellyfin.Plugin.JavScraper.Test.Scrapers
{
    [TestClass]
    public class ScraperTest
    {
        [DataTestMethod]
        [TestCategory("IgnoreOnBuild")]
        [DataRow(typeof(AVSOXScraper), "032416_525")]
        [DataRow(typeof(FC2Scraper), "FC2-2543981")]
        [DataRow(typeof(Jav123Scraper), "midv00119")]
        [DataRow(typeof(JavBusScraper), "midv-119")]
        [DataRow(typeof(JavBusScraper), "080521-004")]
        [DataRow(typeof(JavDBScraper), "midv-119")]
        [DataRow(typeof(JavDBScraper), "080521-004")]
        [DataRow(typeof(MgsTageScraper), "ABW-250")]
        [DataRow(typeof(R18Scraper), "ssis-335")]
        public void TestScraper(Type type, string id)
        {
            Assert.IsNotNull(type.FullName);
            var createdObject = type.Assembly.CreateInstance(type.FullName, false, System.Reflection.BindingFlags.CreateInstance, null, new object[] { Holder.GetLoggerFactory(), Holder.GetHttpClientManager(), Holder.GetDmmService() }, null, null);
            if (createdObject is IScraper scraper)
            {
                var searchResult = scraper.Search(id).Result;
                Assert.AreNotEqual(0, searchResult.Count);
                Console.WriteLine(searchResult[0]);
                var detail = scraper.GetJavVideo(searchResult[0]).Result;
                Assert.IsNotNull(detail);
                Console.WriteLine(detail);
            }
        }
    }
}
