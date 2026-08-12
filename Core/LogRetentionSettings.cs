using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AppiumBuilder.Core
{
    public sealed class LogRetentionSettings
    {
        public int retentionDays { get; set; } = 30;
        public double maxSizeGb { get; set; } = 2.0;

        public static LogRetentionSettings Load(string logFolder)
        {
            string path = Path.Combine(logFolder, "retention_settings.json");
            try
            {
                if (!File.Exists(path)) return new LogRetentionSettings();
                var settings = JsonSerializer.Deserialize<LogRetentionSettings>(File.ReadAllText(path, Encoding.UTF8), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new LogRetentionSettings();
                settings.retentionDays = Math.Clamp(settings.retentionDays, 1, 3650);
                settings.maxSizeGb = Math.Clamp(settings.maxSizeGb, 0.1, 1000.0);
                return settings;
            }
            catch { return new LogRetentionSettings(); }
        }

        public void Save(string logFolder)
        {
            Directory.CreateDirectory(logFolder);
            retentionDays = Math.Clamp(retentionDays, 1, 3650);
            maxSizeGb = Math.Clamp(maxSizeGb, 0.1, 1000.0);
            File.WriteAllText(Path.Combine(logFolder, "retention_settings.json"), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }

        public long MaxBytes => (long)(maxSizeGb * 1024D * 1024D * 1024D);
    }
}
