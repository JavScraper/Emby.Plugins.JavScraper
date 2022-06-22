using Jellyfin.Plugin.JavScraper.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Test.Services
{
    [TestClass]
    public class PersonSearchServiceTests
    {
        [TestMethod]
        public void PersonSearchServiceTest()
        {
            var gfriendsAvatarService = new GfriendsAvatarService(Holder.GetLoggerFactory(), Holder.GetHttpClientManager());
            var personSearchService = new PersonSearchService(Holder.GetLoggerFactory().CreateLogger<PersonSearchService>(), gfriendsAvatarService, Holder.GetHttpClientManager());
            var indexList = personSearchService.SearchPersonByName("三上悠亜").Result.ToList();
            Assert.AreNotEqual(0, indexList.Count);
            Console.WriteLine(indexList.First());
            var person = personSearchService.GetDetail(indexList.First()).Result;
            Assert.IsNotNull(person);
            Console.WriteLine(person);
        }
    }
}
