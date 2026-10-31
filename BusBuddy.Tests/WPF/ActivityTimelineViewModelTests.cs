using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels.Activity;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class ActivityTimelineViewModelTests
{
    [Test]
    public async Task FilterChanges_DoNotOverlapGetLogsCalls()
    {
        var inflight = 0;
        var maxInflight = 0;
        var logs = new Mock<IActivityLogService>();
        logs.Setup(s => s.GetLogsAsync(It.IsAny<int>()))
            .Returns(async () =>
            {
                var now = Interlocked.Increment(ref inflight);
                maxInflight = Math.Max(maxInflight, now);
                await Task.Delay(40);
                Interlocked.Decrement(ref inflight);
                return Array.Empty<ActivityLog>();
            });

        var vm = new ActivityTimelineViewModel(logs.Object);
        foreach (var option in vm.EventTypes)
        {
            option.IsSelected = false;
        }

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (inflight == 0 && !vm.IsLoading)
            {
                await Task.Delay(50);
                if (inflight == 0 && !vm.IsLoading)
                {
                    break;
                }
            }
            else
            {
                await Task.Delay(20);
            }
        }

        Assert.That(maxInflight, Is.EqualTo(1));
        Assert.That(inflight, Is.EqualTo(0));
        Assert.That(vm.IsLoading, Is.False);
    }
}
