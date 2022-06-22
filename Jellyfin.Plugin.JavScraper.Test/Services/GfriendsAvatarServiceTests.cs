using Jellyfin.Plugin.JavScraper.Services;

namespace Jellyfin.Plugin.JavScraper.Test.Services
{
    [TestClass]
    public class GfriendsAvatarServiceTests
    {
        [TestMethod]
        public void FindAvatarAddressAsyncTest()
        {
            var gfriendsAvatarService = new GfriendsAvatarService(Holder.GetLoggerFactory(), Holder.GetHttpClientManager());
            var result  = gfriendsAvatarService.FindAvatarAddressAsync("三上悠亚", CancellationToken.None).Result;
            Assert.AreEqual("https://raw.githubusercontent.com/xinxin8816/gfriends/master/Content/0-Hand-Storage/三上悠亜.jpg", result);
        }
    }
}
