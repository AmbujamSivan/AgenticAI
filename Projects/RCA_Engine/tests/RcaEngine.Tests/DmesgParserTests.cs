using RcaEngine.Parsing;

namespace RcaEngine.Tests;

public class DmesgParserTests
{
    [Theory]
    [InlineData("EDAC MC1: 1 CE memory read error on CPU_SrcID#0_MC#1_Chan#0_DIMM#0 (channel:0 slot:0)", DmesgCategory.MemoryCorrectedError)]
    [InlineData("EDAC MC0: 1 UE memory read error on CPU_SrcID#0_MC#0_Chan#1_DIMM#0", DmesgCategory.MemoryUncorrectedError)]
    [InlineData("mce: [Hardware Error]: Machine check events logged", DmesgCategory.MachineCheck)]
    [InlineData("nvme nvme0: I/O 566 QID 3 timeout, aborting", DmesgCategory.NvmeError)]
    [InlineData("nvme nvme0: controller is down; will reset: CSTS=0xffffffff, PCI_STATUS=0x10", DmesgCategory.NvmeError)]
    [InlineData("pcieport 0000:5d:01.0: AER: Corrected error received: 0000:5e:00.0", DmesgCategory.PcieAer)]
    [InlineData("Buffer I/O error on dev nvme0n1p1, logical block 101609024, lost async page write", DmesgCategory.IoError)]
    [InlineData("systemd[1]: Started Daily apt download activities.", DmesgCategory.Other)]
    public void Classify_RecognizesFaultClasses(string message, DmesgCategory expected)
    {
        Assert.Equal(expected, DmesgParser.Classify(message));
    }

    [Fact]
    public void Parse_ExtractsTimestampAndMessage()
    {
        var lines = DmesgParser.Parse(["[ 8811.204518] EDAC MC1: 1 CE memory read error on CPU_SrcID#0_MC#1_Chan#0_DIMM#0"]);

        var line = Assert.Single(lines);
        Assert.Equal(8811.204518, line.Timestamp, precision: 6);
        Assert.StartsWith("EDAC MC1", line.Message);
        Assert.Equal(DmesgCategory.MemoryCorrectedError, line.Category);
    }

    [Fact]
    public void Parse_SkipsBlankLines()
    {
        var lines = DmesgParser.Parse(["", "   ", "[1.0] hello"]);
        Assert.Single(lines);
    }
}
