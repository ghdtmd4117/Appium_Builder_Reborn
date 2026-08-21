using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppiumBuilder.Utils;

namespace AppiumBuilder.Core
{
    public enum LocalTcExecutionMode
    {
        LocalPc,
        IntranetServer
    }

    public sealed class LocalTcExecutionSettings
    {
        public LocalTcExecutionMode Mode { get; set; } = LocalTcExecutionMode.LocalPc;
        public string ServerEndpoint { get; set; } = "http://127.0.0.1:7788";
        [JsonIgnore]
        public string ServerToken { get; set; } = string.Empty;
        public string ProtectedServerToken { get; set; } = string.Empty;

        public bool UsesRemoteServer => Mode == LocalTcExecutionMode.IntranetServer;
    }

    public static class LocalTcExecutionSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string StoreFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppiumBuilderReborn",
            "TC");

        public static string StorePath => Path.Combine(StoreFolder, "tc_execution_settings.json");

        public static LocalTcExecutionSettings Load()
        {
            try
            {
                if (!File.Exists(StorePath)) return new LocalTcExecutionSettings();
                string json = File.ReadAllText(StorePath, Encoding.UTF8);
                LocalTcExecutionSettings? settings = JsonSerializer.Deserialize<LocalTcExecutionSettings>(json, JsonOptions);
                settings ??= new LocalTcExecutionSettings();
                if (!string.IsNullOrWhiteSpace(settings.ProtectedServerToken))
                {
                    try { settings.ServerToken = SecretStore.UnprotectFromBase64(settings.ProtectedServerToken); }
                    catch { settings.ServerToken = string.Empty; }
                }
                return Normalize(settings);
            }
            catch
            {
                return new LocalTcExecutionSettings();
            }
        }

        public static void Save(LocalTcExecutionSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            settings = Normalize(settings);
            settings.ProtectedServerToken = string.IsNullOrWhiteSpace(settings.ServerToken)
                ? string.Empty
                : SecretStore.ProtectToBase64(settings.ServerToken);
            Directory.CreateDirectory(StoreFolder);
            string temp = StorePath + ".tmp";
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            File.Move(temp, StorePath, overwrite: true);
        }

        private static LocalTcExecutionSettings Normalize(LocalTcExecutionSettings settings)
        {
            settings.ServerEndpoint = (settings.ServerEndpoint ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(settings.ServerEndpoint))
                settings.ServerEndpoint = "http://127.0.0.1:7788";
            settings.ServerToken = (settings.ServerToken ?? string.Empty).Trim();
            return settings;
        }
    }

    public sealed class LocalTcSourceReference
    {
        public string SourcePath { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;

        public string DisplaySummary => $"{FileName} · {Kind} · 서버/실행 시 분석";

        public static LocalTcSourceReference ExistingTc(string path) => new()
        {
            SourcePath = path,
            FileName = Path.GetFileName(path),
            Category = "example",
            Kind = Path.GetExtension(path).TrimStart('.').ToUpperInvariant()
        };

        public static LocalTcSourceReference Document(string path) => new()
        {
            SourcePath = path,
            FileName = Path.GetFileName(path),
            Category = "document",
            Kind = KindFromPath(path)
        };

        private static string KindFromPath(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".pptx" => "PPTX",
                ".pdf" => "PDF",
                ".docx" => "DOCX",
                ".txt" or ".md" => "TEXT",
                _ => "IMAGE"
            };
        }
    }
}
