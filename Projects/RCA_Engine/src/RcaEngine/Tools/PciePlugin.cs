using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using RcaEngine.Telemetry;

namespace RcaEngine.Tools;

/// <summary>Agent tool surface over the PCIe AER register dump.</summary>
public sealed class PciePlugin(DiagnosticBundle bundle)
{
    [KernelFunction("list_pcie_devices")]
    [Description("Lists all PCIe devices in the diagnostic bundle with their link state and whether any AER error bits are set. Use this to spot devices with errors or downtrained links.")]
    public string ListDevices()
    {
        if (bundle.PcieDevices.Count == 0) return "No PCIe register data in this bundle.";

        var sb = new StringBuilder();
        foreach (var dev in bundle.PcieDevices)
        {
            sb.Append($"{dev.Address} — {dev.Description}: link {dev.LinkStatus.Speed} {dev.LinkStatus.Width}");
            if (dev.IsLinkDowntrained)
                sb.Append($" [DOWNTRAINED from {dev.LinkCapabilities.Speed} {dev.LinkCapabilities.Width}]");
            if (dev.AerCorrectableStatus != 0)
                sb.Append($" correctable-errors-set={dev.AerCorrectableStatus:X8} (count {dev.AerCorrectableCount})");
            if (dev.AerUncorrectableStatus != 0)
                sb.Append($" UNCORRECTABLE-errors-set={dev.AerUncorrectableStatus:X8}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    [KernelFunction("decode_pcie_aer_registers")]
    [Description("Decodes the AER (Advanced Error Reporting) status registers of one PCIe device into named error bits per the PCIe specification. Pass the device address, e.g. '0000:3b:00.0'.")]
    public string DecodeAerRegisters(
        [Description("PCIe device address, e.g. 0000:3b:00.0")] string deviceAddress)
    {
        var dev = bundle.PcieDevices.FirstOrDefault(d =>
            d.Address.Equals(deviceAddress.Trim(), StringComparison.OrdinalIgnoreCase));
        if (dev is null)
            return $"Device {deviceAddress} not found. Known devices: {string.Join(", ", bundle.PcieDevices.Select(d => d.Address))}";

        var sb = new StringBuilder();
        sb.AppendLine($"Device {dev.Address} ({dev.Description})");
        sb.AppendLine($"  Link capability: {dev.LinkCapabilities.Speed} {dev.LinkCapabilities.Width}; current: {dev.LinkStatus.Speed} {dev.LinkStatus.Width}"
                      + (dev.IsLinkDowntrained ? "  << LINK DOWNTRAINED" : ""));
        sb.AppendLine($"  Correctable Error Status 0x{dev.AerCorrectableStatus:X8}: "
                      + (dev.DecodedCorrectableErrors.Count > 0 ? string.Join(", ", dev.DecodedCorrectableErrors) : "none"));
        sb.AppendLine($"  Correctable error count since boot: {dev.AerCorrectableCount}");
        sb.AppendLine($"  Uncorrectable Error Status 0x{dev.AerUncorrectableStatus:X8}: "
                      + (dev.DecodedUncorrectableErrors.Count > 0 ? string.Join(", ", dev.DecodedUncorrectableErrors) : "none"));
        return sb.ToString();
    }
}
