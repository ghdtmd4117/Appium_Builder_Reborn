using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AppiumBuilder.Core
{
    public static class LogRetention
    {
        private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "test_history.json", "test_history.json.bak", "gemini_key.dat", "selected_device.txt", "retention_settings.json"
        };

        public static void Cleanup(string root, int retentionDays = 30, long maxBytes = 2L * 1024 * 1024 * 1024)
        {
            try
            {
                if (!Directory.Exists(root)) return;
                DateTime cutoff = DateTime.Now.AddDays(-Math.Max(1, retentionDays));
                var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .Where(info => !IsProtected(root, info.FullName, info.Name))
                    .OrderBy(info => info.LastWriteTimeUtc)
                    .ToList();

                foreach (var file in files.Where(f => f.LastWriteTime < cutoff).ToList()) TryDelete(file);
                files = files.Where(f => f.Exists).ToList();
                long total = files.Sum(f => SafeLength(f));
                foreach (var file in files)
                {
                    if (total <= maxBytes) break;
                    long len = SafeLength(file);
                    if (TryDelete(file)) total = Math.Max(0, total - len);
                }
            }
            catch { }
        }

        private static bool IsProtected(string root, string fullPath, string fileName)
        {
            if (ProtectedNames.Contains(fileName)) return true;

            string relative;
            try { relative = Path.GetRelativePath(root, fullPath); }
            catch { relative = fullPath; }
            relative = relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            string sep = Path.DirectorySeparatorChar.ToString();
            string[] persistentRoots =
            {
                "AUTO_TEST" + sep + "CSV",
                "AUTO_TEST" + sep + "PY_SCRIPT",
                "Scenarios"
            };
            if (persistentRoots.Any(prefix =>
                relative.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith(prefix + sep, StringComparison.OrdinalIgnoreCase)))
                return true;

            string testSetRoot = "AUTO_TEST" + sep + "TEST_SET";
            if (relative.Equals(testSetRoot, StringComparison.OrdinalIgnoreCase)) return true;
            if (relative.StartsWith(testSetRoot + sep, StringComparison.OrdinalIgnoreCase))
            {
                // 시나리오 정의와 baseline은 영구 보존하지만, 실행마다 쌓이는 runs 산출물은 보존 정책 대상이다.
                string runsSegment = sep + "runs" + sep;
                if (relative.Contains(runsSegment, StringComparison.OrdinalIgnoreCase)) return false;
                return true;
            }

            return relative.Contains(sep + "baseline" + sep, StringComparison.OrdinalIgnoreCase) ||
                   relative.StartsWith("baseline" + sep, StringComparison.OrdinalIgnoreCase);
        }

        private static long SafeLength(FileInfo file) { try { return file.Length; } catch { return 0; } }
        private static bool TryDelete(FileInfo file) { try { file.Delete(); return true; } catch { return false; } }
    }
}
