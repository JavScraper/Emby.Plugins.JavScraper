using System;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Services.Model;

namespace Jellyfin.Plugin.JavScraper.Services
{
    /// <summary>
    /// 番号识别
    /// </summary>
    public static class JavIdRecognizer
    {
        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Compiled;

        private static readonly Func<string, JavId?>[] _funcs = new Func<string, JavId?>[] { Carib, Heyzo, FC2, Musume, OnlyNumber };

        /// <summary>
        /// 移除视频编码 1080p,720p 2k 之类的
        /// </summary>
        private static readonly Regex _vedioCodecRegex = new(@"(?<=^|[^\d])([\d]{3,5}p|[\d]{1,2}k)(?=$|[^a-z])", Options);

        private static readonly Regex[] _regexMusume = new Regex[]
        {
            new Regex(@"(?<id>[\d]{4,8}-[\d]{1,6})-(10mu)", Options),
            new Regex(@"(10Musume)-(?<id>[\d]{4,8}-[\d]{1,6})", Options)
        };

        private static readonly Regex[] _regexCarib = new Regex[]
        {
            new Regex(@"(?<id>[\d]{4,8}-[\d]{1,6})-(1pon|carib|paco|mura)", Options),
            new Regex(@"(1Pondo|Caribbean|Pacopacomama|muramura)-(?<id>[\d]{4,8}-[\d]{1,8})($|[^\d])", Options)
        };

        private static readonly Regex _regexHeyzo = new(@"Heyzo(|-| |.com)(HD-|)(?<id>[\d]{2,8})($|[^\d])", Options);

        private static readonly Regex _regexFC2 = new(@"FC2-*(PPV|)[^\d]{1,3}(?<id>[\d]{2,10})($|[^\d])", Options);

        private static readonly Regex _regexNumber = new(@"(?<id>[\d]{6,8}-[\d]{1,6})", Options);

        public static JavId? Parse(string name)
        {
            name = name.Replace("_", "-", StringComparison.Ordinal)
                .Replace(" ", "-", StringComparison.Ordinal)
                .Replace(".", "-", StringComparison.Ordinal);

            name = _vedioCodecRegex.Replace(name, string.Empty);

            foreach (var func in _funcs)
            {
                var result = func(name);
                if (result != null)
                {
                    return result;
                }
            }

            name = Regex.Replace(name, @"ts6[\d]+", string.Empty, Options);
            name = Regex.Replace(name, @"-*whole\d*", string.Empty, Options);
            name = Regex.Replace(name, @"-*full$", string.Empty, Options);
            name = name.Replace("tokyo-hot", string.Empty, StringComparison.OrdinalIgnoreCase);
            name = name.TrimEnd("-C").TrimEnd("-HD", "-full", "full").TrimStart("HD-").TrimStart("h-");
            name = Regex.Replace(name, @"\d{2,4}-\d{1,2}-\d{1,2}", string.Empty, Options); // 日期
            name = Regex.Replace(name, @"(.*)(00)(\d{3})", "$1-$3", Options); // FANZA cid AAA00111
            // 标准 AAA-111
            var match = Regex.Match(name, @"(^|[^a-z0-9])(?<id>[a-z0-9]{2,10}-[\d]{2,8})($|[^\d])", Options);
            if (match.Success && match.Groups["id"].Value.Length >= 4)
            {
                return match.Groups["id"].Value;
            }

            // 第二段带字母 AAA-B11
            match = Regex.Match(name, @"(^|[^a-z0-9])(?<id>[a-z]{2,10}-[a-z]{1,5}[\d]{2,8})($|[^\d])", Options);
            if (match.Success && match.Groups["id"].Value.Length >= 4)
            {
                return match.Groups["id"].Value;
            }

            // 没有横杠的 AAA111
            match = Regex.Match(name, @"(^|[^a-z0-9])(?<id>[a-z]{1,10}[\d]{2,8})($|[^\d])", Options);
            if (match.Success && match.Groups["id"].Value.Length >= 4)
            {
                return match.Groups["id"].Value;
            }

            return null;
        }

        private static JavId? Musume(string name)
        {
            foreach (var regex in _regexMusume)
            {
                var match = regex.Match(name);
                if (match.Success)
                {
                    return new JavId()
                    {
                        Matcher = nameof(Musume),
                        Type = JavIdType.Suren,
                        Id = match.Groups["id"].Value.Replace("_", "-", StringComparison.Ordinal)
                    };
                }
            }

            return null;
        }

        private static JavId? Carib(string name)
        {
            foreach (var regex in _regexCarib)
            {
                var match = regex.Match(name);
                if (match.Success)
                {
                    return new JavId()
                    {
                        Matcher = nameof(Carib),
                        Type = JavIdType.Uncensored,
                        Id = match.Groups["id"].Value.Replace("-", "_", StringComparison.Ordinal)
                    };
                }
            }

            return null;
        }

        private static JavId? Heyzo(string name)
        {
            var m = _regexHeyzo.Match(name);
            if (!m.Success)
            {
                return null;
            }

            return new JavId()
            {
                Matcher = nameof(Heyzo),
                Id = $"heyzo-{m.Groups["id"]}",
                Type = JavIdType.Uncensored
            };
        }

        public static JavId? FC2(string name)
        {
            var m = _regexFC2.Match(name);
            if (!m.Success)
            {
                return null;
            }

            return new JavId()
            {
                Id = $"fc2-ppv-{m.Groups["id"]}",
                Matcher = nameof(FC2),
                Type = JavIdType.Suren
            };
        }

        private static JavId? OnlyNumber(string name)
        {
            var m = _regexNumber.Match(name);
            if (!m.Success)
            {
                return null;
            }

            return new JavId()
            {
                Id = m.Groups["id"].Value,
                Matcher = nameof(OnlyNumber),
            };
        }
    }
}
