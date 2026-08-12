using System;
using System.IO;
using AppiumBuilder.Core;
using Xunit;

namespace AppiumBuilder.Tests;

public sealed class LogRetentionTests
{
    [Fact]
    public void Cleanup_PreservesScenarioSourcesAndHistory()
    {
        string root = Path.Combine(Path.GetTempPath(), "AppiumBuilderTests", Guid.NewGuid().ToString("N"));
        try
        {
            string scenario = Path.Combine(root, "AUTO_TEST", "TEST_SET", "Login", "scenario.csv");
            string history = Path.Combine(root, "test_history.json");
            string oldLog = Path.Combine(root, "LOG", "old.log");
            Directory.CreateDirectory(Path.GetDirectoryName(scenario)!);
            Directory.CreateDirectory(Path.GetDirectoryName(oldLog)!);
            File.WriteAllText(scenario, "Step\n[Sleep] 1 초");
            File.WriteAllText(history, "[]");
            File.WriteAllText(oldLog, "old");
            DateTime old = DateTime.Now.AddDays(-90);
            File.SetLastWriteTime(scenario, old);
            File.SetLastWriteTime(history, old);
            File.SetLastWriteTime(oldLog, old);

            LogRetention.Cleanup(root, retentionDays: 30, maxBytes: long.MaxValue);

            Assert.True(File.Exists(scenario));
            Assert.True(File.Exists(history));
            Assert.False(File.Exists(oldLog));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public void Cleanup_RemovesOldRunArtifactsButKeepsBaseline()
    {
        string root = Path.Combine(Path.GetTempPath(), "AppiumBuilderTests", Guid.NewGuid().ToString("N"));
        try
        {
            string baseline = Path.Combine(root, "AUTO_TEST", "TEST_SET", "Login", "baseline", "step_001.png");
            string runArtifact = Path.Combine(root, "AUTO_TEST", "TEST_SET", "Login", "runs", "old_run", "screen.png");
            Directory.CreateDirectory(Path.GetDirectoryName(baseline)!);
            Directory.CreateDirectory(Path.GetDirectoryName(runArtifact)!);
            File.WriteAllText(baseline, "baseline");
            File.WriteAllText(runArtifact, "run");
            DateTime old = DateTime.Now.AddDays(-90);
            File.SetLastWriteTime(baseline, old);
            File.SetLastWriteTime(runArtifact, old);

            LogRetention.Cleanup(root, retentionDays: 30, maxBytes: long.MaxValue);

            Assert.True(File.Exists(baseline));
            Assert.False(File.Exists(runArtifact));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
