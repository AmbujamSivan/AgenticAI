using RcaEngine.Parsing;

namespace RcaEngine.Tests;

public class DpuConsoleParserTests
{
    [Theory]
    [InlineData("NIC subsystem firmware image slot 1: crc mismatch (stored 0x8c41f2aa, computed 0x8c41f000)", DpuConsoleCategory.BootError)]
    [InlineData("watchdog: recovery attempt 3/3 exhausted, NIC subsystem HANG in pre-init", DpuConsoleCategory.BootError)]
    [InlineData("pipe entry add failed: OUT_OF_RESOURCES (steering table full)", DpuConsoleCategory.OffloadError)]
    [InlineData("hw-offload disabled for new flows, notifying host datapath", DpuConsoleCategory.OffloadError)]
    [InlineData("DPU ARM cores nominal, thermals nominal, mgmt link up", DpuConsoleCategory.Info)]
    public void Classify_RecognizesFaultClasses(string message, DpuConsoleCategory expected)
    {
        Assert.Equal(expected, DpuConsoleParser.Classify(message));
    }

    [Fact]
    public void Parse_ExtractsTimestampSubsystemAndMessage()
    {
        var lines = DpuConsoleParser.Parse(
            ["2026-07-21T07:40:05Z fw: NIC subsystem firmware load FAILED, staying in pre-init hold"]);

        var line = Assert.Single(lines);
        Assert.Equal("fw", line.Subsystem);
        Assert.StartsWith("NIC subsystem firmware load FAILED", line.Message);
        Assert.Equal(DpuConsoleCategory.BootError, line.Category);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 7, 40, 5, TimeSpan.Zero), line.Timestamp);
    }

    [Fact]
    public void Parse_SkipsCommentsAndNonMatchingLines()
    {
        var lines = DpuConsoleParser.Parse(["# header", "", "not a console line"]);
        Assert.Empty(lines);
    }
}
