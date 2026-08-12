using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AppiumBuilder.Core
{
    public sealed class TestStepRecord
    {
        public int index { get; set; }
        public int loop { get; set; }
        public string raw { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string startedAt { get; set; } = string.Empty;
        public string timestamp { get; set; } = string.Empty;
        public long durationMs { get; set; }
        public string? message { get; set; }
        public string? artifactFolder { get; set; }
        public double? matchRate { get; set; }
    }

    public sealed class TestRunRecord
    {
        public string runId { get; set; } = string.Empty;
        public string? batchId { get; set; }
        public string scenario { get; set; } = string.Empty;
        public string startedAt { get; set; } = string.Empty;
        public string timestamp { get; set; } = string.Empty;
        public int totalSteps { get; set; }
        public bool pass { get; set; }
        public string status { get; set; } = string.Empty;
        public long durationMs { get; set; }
        public string deviceSerial { get; set; } = string.Empty;
        public string deviceModel { get; set; } = string.Empty;
        public string osVersion { get; set; } = string.Empty;
        public string? failMessage { get; set; }
        public List<TestStepRecord> steps { get; set; } = new();
    }

    /// <summary>
    /// test_history.json을 temp -> atomic replace 방식으로 보존한다.
    /// 주 파일이 손상되면 .bak을 자동 복구해 다음 실행에서 과거 이력이 덮어써지는 것을 막는다.
    /// </summary>
    public static class TestHistoryStore
    {
        private static readonly object Gate = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string GetHistoryPath(string logFolder) => Path.Combine(logFolder, "test_history.json");
        public static string GetBackupPath(string logFolder) => GetHistoryPath(logFolder) + ".bak";

        public static List<TestRunRecord> Load(string logFolder)
        {
            lock (Gate)
            {
                Directory.CreateDirectory(logFolder);
                string path = GetHistoryPath(logFolder);
                string backup = GetBackupPath(logFolder);

                if (TryRead(path, out List<TestRunRecord>? main)) return main;
                if (TryRead(backup, out List<TestRunRecord>? recovered))
                {
                    try { SaveInternal(logFolder, recovered, createBackup: false); } catch { }
                    return recovered;
                }
                return new List<TestRunRecord>();
            }
        }

        public static void Save(string logFolder, IReadOnlyList<TestRunRecord> records, int maxRecords = 1000)
        {
            lock (Gate)
            {
                var copy = new List<TestRunRecord>(records);
                if (maxRecords > 0 && copy.Count > maxRecords)
                    copy.RemoveRange(0, copy.Count - maxRecords);
                SaveInternal(logFolder, copy, createBackup: true);
            }
        }

        public static void Append(string logFolder, TestRunRecord record, int maxRecords = 1000)
        {
            lock (Gate)
            {
                List<TestRunRecord> list = Load(logFolder);
                list.Add(record);
                if (maxRecords > 0 && list.Count > maxRecords)
                    list.RemoveRange(0, list.Count - maxRecords);
                SaveInternal(logFolder, list, createBackup: true);
            }
        }

        private static bool TryRead(string path, out List<TestRunRecord> records)
        {
            records = new List<TestRunRecord>();
            try
            {
                if (!File.Exists(path)) return false;
                string json = File.ReadAllText(path, Encoding.UTF8);
                var parsed = JsonSerializer.Deserialize<List<TestRunRecord>>(json, JsonOptions);
                if (parsed == null) return false;
                records = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SaveInternal(string logFolder, IReadOnlyList<TestRunRecord> records, bool createBackup)
        {
            Directory.CreateDirectory(logFolder);
            string path = GetHistoryPath(logFolder);
            string backup = GetBackupPath(logFolder);
            string temp = path + ".tmp";
            string json = JsonSerializer.Serialize(records, JsonOptions);

            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            // temp 자체가 정상 JSON인지 확인한 후에만 주 파일을 교체한다.
            if (!TryRead(temp, out _))
            {
                try { File.Delete(temp); } catch { }
                throw new InvalidDataException("테스트 이력 임시 파일 검증에 실패했습니다.");
            }

            if (!File.Exists(path))
            {
                File.Move(temp, path, overwrite: true);
                return;
            }

            try
            {
                if (createBackup)
                    File.Replace(temp, path, backup, ignoreMetadataErrors: true);
                else
                    File.Replace(temp, path, null, ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                FallbackReplace(temp, path, backup, createBackup);
            }
            catch (IOException)
            {
                FallbackReplace(temp, path, backup, createBackup);
            }
        }

        private static void FallbackReplace(string temp, string path, string backup, bool createBackup)
        {
            if (createBackup && File.Exists(path)) File.Copy(path, backup, overwrite: true);
            File.Move(temp, path, overwrite: true);
        }
    }
}
