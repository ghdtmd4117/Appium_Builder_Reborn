using System;
using System.IO;
using AppiumBuilder.Core;
using Xunit;

namespace AppiumBuilder.Tests;

public sealed class VisualAssertConfigTests
{
    [Fact]
    public void VisualConfig_RoundTripsThresholdAndMasks()
    {
        string dir = Path.Combine(Path.GetTempPath(), "AppiumBuilderTests", Guid.NewGuid().ToString("N"));
        try
        {
            var cfg = new VisualAssertConfig { defaultThreshold = 96.5 };
            cfg.steps["3"] = new VisualStepConfig { threshold = 98.0 };
            cfg.steps["3"].masks.Add(new VisualMaskRect { x = .1, y = .2, width = .3, height = .1 });
            cfg.Save(dir);
            VisualAssertConfig loaded = VisualAssertConfig.Load(dir);
            Assert.Equal(96.5, loaded.defaultThreshold, 1);
            Assert.Equal(98.0, loaded.steps["3"].threshold);
            Assert.Single(loaded.steps["3"].masks);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
