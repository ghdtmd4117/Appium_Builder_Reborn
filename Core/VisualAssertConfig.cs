using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AppiumBuilder.Core
{
    public sealed class VisualMaskRect
    {
        public double x { get; set; }
        public double y { get; set; }
        public double width { get; set; }
        public double height { get; set; }
    }

    public sealed class VisualStepConfig
    {
        public double? threshold { get; set; }
        public List<VisualMaskRect> masks { get; set; } = new();
    }

    public sealed class VisualAssertConfig
    {
        public double defaultThreshold { get; set; } = 95.0;
        public Dictionary<string, VisualStepConfig> steps { get; set; } = new();

        public static VisualAssertConfig Load(string scenarioFolder)
        {
            string path = Path.Combine(scenarioFolder, "visual_assert.json");
            try
            {
                if (!File.Exists(path)) return new VisualAssertConfig();
                return JsonSerializer.Deserialize<VisualAssertConfig>(File.ReadAllText(path, Encoding.UTF8), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new VisualAssertConfig();
            }
            catch
            {
                return new VisualAssertConfig();
            }
        }

        public void Save(string scenarioFolder)
        {
            Directory.CreateDirectory(scenarioFolder);
            string path = Path.Combine(scenarioFolder, "visual_assert.json");
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }
    }
}
