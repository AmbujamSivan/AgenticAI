using RcaEngine.Agents;

namespace RcaEngine.Tests;

/// <summary>
/// Covers the orchestrator's remediation safety net: it grafts evidence-derived actions
/// when the agent's remediation shares no substantive fix term with them.
/// </summary>
public class RemediationSafetyNetTests
{
    private static readonly string[] EvidenceActions =
    [
        "Reflash the DPU NIC-subsystem firmware (slot 1) or boot the alternate image slot",
        "Verify the new image's checksum before the next boot",
        "If the reflash fails, capture the DPU firmware logs and RMA the device",
    ];

    [Fact]
    public void Diverges_WhenAgentRecommendsRebootForFirmwareCorruption()
    {
        // The evidence-backed fix is a reflash; a reboot/config-check shares no substantive
        // fix term, so the net must flag divergence and graft the deterministic actions.
        string[] agent = ["Reboot the device and check its configuration", "Escalate to the vendor"];
        Assert.True(RcaOrchestrator.RemediationDiverges(agent, EvidenceActions));
    }

    [Fact]
    public void DoesNotDiverge_WhenAgentAlsoRecommendsReflash()
    {
        // The live llama3.2 output for the enum bundle — must be recognized as aligned.
        string[] agent = ["Reflash firmware or boot alternate image slot"];
        Assert.False(RcaOrchestrator.RemediationDiverges(agent, EvidenceActions));
    }

    [Fact]
    public void Diverges_IgnoresGenericVerbsSharedByUnrelatedFixes()
    {
        // "Verify" / "check" are shared filler, not evidence of an aligned fix.
        string[] agent = ["Verify the system and check the logs"];
        Assert.True(RcaOrchestrator.RemediationDiverges(agent, EvidenceActions));
    }

    [Fact]
    public void DoesNotDiverge_OnSubstantiveOverlap()
    {
        string[] deterministic = ["Replace the NVMe drive in Bay3", "Resync replication after replacement"];
        string[] agent = ["Replace the failed drive and resync the array"];
        Assert.False(RcaOrchestrator.RemediationDiverges(agent, deterministic));
    }
}
