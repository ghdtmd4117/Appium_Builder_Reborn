using System;
using System.Collections.Generic;
using System.IO;
using AppiumBuilder.Core;
using Xunit;

namespace AppiumBuilder.Tests;

public sealed class TestHistoryStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsRecords()
    {
        string dir = Path.Combine(Path.GetTempPath(), "AppiumBuilderTests", Guid.NewGuid().ToString("N"));
        try
        {
            TestHistoryStore.Save(dir, new List<TestRunRecord>
            {
                new() { runId = "1", scenario = "Login", status = "PASS", pass = true, totalSteps = 3 }
            });
            List<TestRunRecord> records = TestHistoryStore.Load(dir);
            Assert.Single(records);
            Assert.Equal("Login", records[0].scenario);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void CorruptedMain_RecoversFromBackup()
    {
        string dir = Path.Combine(Path.GetTempPath(), "AppiumBuilderTests", Guid.NewGuid().ToString("N"));
        try
        {
            TestHistoryStore.Save(dir, new List<TestRunRecord> { new() { runId = "old", scenario = "A", status = "PASS", pass = true } });
            TestHistoryStore.Save(dir, new List<TestRunRecord> { new() { runId = "new", scenario = "B", status = "PASS", pass = true } });
            File.WriteAllText(TestHistoryStore.GetHistoryPath(dir), "{broken json");
            List<TestRunRecord> records = TestHistoryStore.Load(dir);
            Assert.Single(records);
            Assert.Equal("A", records[0].scenario);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
