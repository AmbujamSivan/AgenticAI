using RcaEngine.Models;
using RcaEngine.Telemetry;
using RcaEngine.Triage;

namespace RcaEngine.Tests;

/// <summary>
/// End-to-end triage tests against the sample bundles shipped in samples/.
/// These assert the deterministic path isolates the correct subsystem for each scenario.
/// </summary>
public class DeterministicTriageTests
{
    private static string SamplesDir()
    {
        // Walk up from the test bin directory to the repo root (marked by the solution file).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null &&
               !File.Exists(Path.Combine(dir.FullName, "RcaEngine.sln")) &&
               !File.Exists(Path.Combine(dir.FullName, "RcaEngine.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "samples");
    }

    [Theory]
    [InlineData("bundle-memory-ce-storm", FailureCategory.MemorySubsystem)]
    [InlineData("bundle-nvme-controller-failure", FailureCategory.StorageNvme)]
    [InlineData("bundle-pcie-link-degrade", FailureCategory.PcieLink)]
    public void Run_IsolatesCorrectSubsystem(string bundleName, FailureCategory expected)
    {
        var bundle = DiagnosticBundle.Load(Path.Combine(SamplesDir(), bundleName));

        var report = DeterministicTriage.Run(bundle);

        Assert.Equal(expected, report.Category);
        Assert.NotEmpty(report.Evidence);
        Assert.NotEmpty(report.RecommendedActions);
        Assert.InRange(report.Confidence, 0.3, 1.0);
        Assert.Equal("deterministic", report.GeneratedBy);
    }

    [Fact]
    public void Run_MemoryStorm_IdentifiesDimm()
    {
        var bundle = DiagnosticBundle.Load(Path.Combine(SamplesDir(), "bundle-memory-ce-storm"));
        var report = DeterministicTriage.Run(bundle);
        Assert.Contains("DIMM", report.FailingComponent);
    }

    [Fact]
    public void Run_EmptyBundle_ReturnsUnknown()
    {
        var bundle = new DiagnosticBundle
        {
            Metadata = new BundleMetadata(),
            RedfishEvents = [],
            PcieDevices = [],
            DmesgLines = []
        };

        var report = DeterministicTriage.Run(bundle);

        Assert.Equal(FailureCategory.Unknown, report.Category);
    }
}
