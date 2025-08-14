using NUnit.Framework;
using System;
using System.IO;
using BusBuddy.WPF.Utilities;

namespace BusBuddy.Tests.Logging
{
    [TestFixture]
    public class LogLifecycleManagerTests
    {
        private string _tempDir = null!;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "bb-logs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void Teardown()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        [Test]
        public void GetLogSummary_EmptyDirectory_ReturnsZero()
        {
            var mgr = new LogLifecycleManager(_tempDir);
            var summary = mgr.GetLogSummary();
            Assert.That(summary.TotalFiles, Is.EqualTo(0));
            Assert.That(summary.TotalSize, Is.EqualTo(0));
        }

        [Test]
        public void PerformIntelligentCleanup_DeletesOldUiLogs()
        {
            var uiLog = Path.Combine(_tempDir, "ui-interactions-20240101.log");
            File.WriteAllText(uiLog, "test");
            File.SetLastWriteTime(uiLog, DateTime.Now.AddDays(-10));

            var mgr = new LogLifecycleManager(_tempDir);
            mgr.PerformIntelligentCleanup();

            Assert.That(File.Exists(uiLog), Is.False);
        }
    }
}
