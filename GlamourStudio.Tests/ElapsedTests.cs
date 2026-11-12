using System;
using GlamourStudio.UI.Model;
using Xunit;

namespace GlamourStudio.Tests;

/// <summary>A recording's clock in Settings → Making a report (UI/Model/Elapsed.cs).</summary>
public class ElapsedTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(7, "0:07")]
    [InlineData(252, "4:12")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3852, "1:04:12")]
    [InlineData(-5, "0:00")]
    public void ClockReadsAsAClockDoes(int seconds, string expected) =>
        Assert.Equal(expected, Elapsed.Clock(TimeSpan.FromSeconds(seconds)));
}
