using HtmlAgilityPack;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jellyfin.GenerateConfigurationPage
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var path = "../../../../Emby.Plugins.JavScraper/Configuration";
            Do(path, "ConfigPage.html", "Jellyfin.ConfigPage.html");
            Do(path, "JavOrganizationConfigPage.html", "Jellyfin.JavOrganizationConfigPage.html");
            Console.WriteLine("Hello World!");
        }

        private static HtmlNode selectArrowContainerNode = HtmlNode.CreateNode(@"<div class=""selectArrowContainer"">
<div style=""visibility:hidden;display:none;"">0</div>
<span class=""selectArrow material-icons keyboard_arrow_down""></span>
</div>");

        private static void Do(string path, string input, string output)
        {
            var en = Environment.CurrentDirectory;
            var file = Path.Combine(path, input);

            var doc = new HtmlDocument();
            var html = File.ReadAllText(file);

            html = html.Replace(@"<i class=""md-icon"">search</i>", @"<span class=""material-icons search""></span>");

            html = Regex.Replace(html, "<button.+btnViewItemUp.+</button>", @"<button type=""button"" is=""paper-icon-button-light"" title=""上"" class=""btnSortable paper-icon-button-light btnSortableMoveUp btnViewItemUp"" data-pluginindex=""2""><span class=""material-icons keyboard_arrow_up""></span></button>");
            html = Regex.Replace(html, "<button.+btnViewItemDown.+</button>", @"<button type=""button"" is=""paper-icon-button-light"" title=""下"" class=""btnSortable paper-icon-button-light btnSortableMoveDown btnViewItemDown"" data-pluginindex=""0""><span class=""material-icons keyboard_arrow_down""></span></button>");

            doc.LoadHtml(html);

            //替换折叠的
            var nodes = doc.DocumentNode.SelectNodes("//div[@is='emby-collapse']");
            if (nodes?.Any() == true)
                foreach (var node in nodes)
                {
                    var title = node.GetAttributeValue("title", string.Empty);
                    var body = node.SelectSingleNode("//div[@class='collapseContent']") ?? node;

                    var nw = HtmlNode.CreateNode(@$"<fieldset class=""verticalSection verticalSection-extrabottompadding"">
	<legend><h3>{title}</h3></legend>
</fieldset>");
                    nw.AppendChildren(body.ChildNodes);

                    node.ParentNode.InsertBefore(nw, node);
                    node.Remove();
                }

            //替换下拉
            nodes = doc.DocumentNode.SelectNodes("//div[@class='selectArrowContainer']");
            if (nodes?.Any() == true)
                foreach (var node in nodes)
                {
                    node.ParentNode.InsertBefore(selectArrowContainerNode, node);
                    node.Remove();
                }

            file = Path.Combine(path, output);
            doc.Save(file);
        }
    }
}