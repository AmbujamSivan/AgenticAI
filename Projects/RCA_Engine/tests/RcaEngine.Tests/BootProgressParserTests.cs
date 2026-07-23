using RcaEngine.Parsing;

namespace RcaEngine.Tests;

public class BootProgressParserTests
{
    [Fact]
    public void Parse_ReadsPostCodesStagesAndErrors()
    {
        string[] log =
        [
            "# comment",
            "POST 0x32 PEI: memory init OK (24 DIMMs trained)",
            "POST 0x9A DXE: WARN config read failed 0000:b3:00.1 (vendor id 0xffffffff)",
            "POST 0xAD OS: OS loader handoff",
        ];

        var entries = BootProgressParser.Parse(log);

        Assert.Equal(3, entries.Count);
        Assert.Equal(0x32, entries[0].Code);
        Assert.Equal(BootStage.Pei, entries[0].Stage);
        Assert.False(entries[0].IsError);
        Assert.Equal(BootStage.Dxe, entries[1].Stage);
        Assert.True(entries[1].IsError);
        Assert.True(BootProgressParser.ReachedOsHandoff(entries));
    }

    [Fact]
    public void ReachedOsHandoff_FalseWhenBootStalls()
    {
        var entries = BootProgressParser.Parse(["POST 0x61 DXE: driver dispatch start"]);
        Assert.False(BootProgressParser.ReachedOsHandoff(entries));
    }
}
