using RcaEngine.Parsing;

namespace RcaEngine.Tests;

public class PcieAerParserTests
{
    [Fact]
    public void DecodeCorrectable_DecodesNamedBits()
    {
        // 0xC1 = bit0 ReceiverError + bit6 BadTLP + bit7 BadDLLP
        var decoded = PcieAerParser.DecodeCorrectable(0x000000C1);
        Assert.Equal(["ReceiverError", "BadTLP", "BadDLLP"], decoded);
    }

    [Fact]
    public void DecodeUncorrectable_DecodesCompletionTimeout()
    {
        var decoded = PcieAerParser.DecodeUncorrectable(0x00004000);
        Assert.Equal(["CompletionTimeout"], decoded);
    }

    [Fact]
    public void Decode_ZeroStatus_ReturnsEmpty()
    {
        Assert.Empty(PcieAerParser.DecodeCorrectable(0));
        Assert.Empty(PcieAerParser.DecodeUncorrectable(0));
    }

    [Fact]
    public void Parse_ReadsDeviceBlocksAndDetectsDowntrain()
    {
        const string dump =
            """
            # comment line
            Device: 0000:5e:00.0
            Description: NVIDIA A100 80GB PCIe Accelerator
            LinkCapabilities: Speed 16GT/s, Width x16
            LinkStatus: Speed 2.5GT/s, Width x16
            AerCorrectableStatus: 0x000000C1
            AerUncorrectableStatus: 0x00000000
            AerCorrectableCount: 18342

            Device: 0000:3b:00.0
            Description: Samsung PM9A3 NVMe SSD Controller
            LinkCapabilities: Speed 16GT/s, Width x4
            LinkStatus: Speed 16GT/s, Width x4
            AerCorrectableStatus: 0x00000000
            AerUncorrectableStatus: 0x00004000
            AerCorrectableCount: 3
            """;

        var devices = PcieAerParser.Parse(dump);

        Assert.Equal(2, devices.Count);

        var gpu = devices[0];
        Assert.Equal("0000:5e:00.0", gpu.Address);
        Assert.True(gpu.IsLinkDowntrained);
        Assert.Equal(18342, gpu.AerCorrectableCount);
        Assert.Contains("ReceiverError", gpu.DecodedCorrectableErrors);

        var nvme = devices[1];
        Assert.False(nvme.IsLinkDowntrained);
        Assert.Equal(["CompletionTimeout"], nvme.DecodedUncorrectableErrors);
    }
}
