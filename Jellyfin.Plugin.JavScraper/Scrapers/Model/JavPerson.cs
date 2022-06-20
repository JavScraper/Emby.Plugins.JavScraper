using System;
using System.Collections.Generic;
using Jellyfin.Plugin.JavScraper.Extensions;

namespace Jellyfin.Plugin.JavScraper.Scrapers.Model
{
    public class JavPerson : JavPersonIndex
    {
        /// <summary>
        /// 封面
        /// </summary>
        public string Cover { get; set; } = string.Empty;

        public DateTime? Birthday { get; set; }

        public string? Nationality { get; set; }

        /// <summary>
        /// 样品图片
        /// </summary>
        public IReadOnlyList<string> Samples { get; set; } = System.Array.Empty<string>();

        public override string ToString() => this.ToJson();
    }
}
