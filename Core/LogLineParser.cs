using System;
using System.Text.RegularExpressions;

namespace AppiumBuilder.Core
{
    public static class LogLineParser
    {
        private static readonly Regex LevelRegex = new Regex(
            @"^\s*(?:\d{2}-\d{2}\s+)?\d{2}:\d{2}:\d{2}(?:\.\d+)?\s+\d+\s+\d+\s+([VDIWEF])\s+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string GetLevel(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return string.Empty;
            Match match = LevelRegex.Match(line);
            return match.Success ? match.Groups[1].Value.ToUpperInvariant() : string.Empty;
        }

        public static bool Matches(string? line, string? levelFilter, string? searchText)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string level = (levelFilter ?? string.Empty).Trim().ToUpperInvariant();
            if (level.Length > 0 && level != "ALL" && level != "전체" && GetLevel(line) != level) return false;

            string search = (searchText ?? string.Empty).Trim();
            if (search.Length > 0 && line.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) return false;
            return true;
        }
    }
}
