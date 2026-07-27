using System.Globalization;
using RedfishEmulator.Core.Inventory;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.Telemetry;

namespace RedfishEmulator.UnitTests.Telemetry;

/// <summary>
/// Unit tests for <see cref="TelemetryGenerator"/>. A fixed <see cref="TimeProvider"/>
/// makes the time-varying readings deterministic.
/// </summary>
public sealed class TelemetryGeneratorTests
{
    private static TelemetryGenerator At(DateTimeOffset when) =>
        new(new FakeSeed(), new FixedTime(when));

    private static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch.AddSeconds(1000);

    [Fact]
    public void Thermal_reports_inlet_cpu_and_gpu_sensors_within_bounds()
    {
        var thermal = At(T0).GetThermal("1");

        Assert.NotNull(thermal);
        // 1 inlet + 2 CPU + 4 GPU = 7 temperature sensors, plus fans.
        Assert.Equal(7, thermal!.Temperatures.Count);
        Assert.NotEmpty(thermal.Fans);
        Assert.All(thermal.Temperatures, t => Assert.InRange(t.ReadingCelsius, 10, 90));
        Assert.Contains(thermal.Temperatures, t => t.PhysicalContext == "GPU");
    }

    [Fact]
    public void Thermal_sensors_are_healthy_at_baseline()
    {
        var thermal = At(T0).GetThermal("1");

        Assert.All(thermal!.Temperatures, t => Assert.Equal(Health.OK, t.Status.Health));
    }

    [Fact]
    public void Power_reports_positive_draw_within_capacity_and_two_psus()
    {
        var power = At(T0).GetPower("1");

        Assert.NotNull(power);
        var control = Assert.Single(power!.PowerControl);
        Assert.InRange(control.PowerConsumedWatts, 1, control.PowerCapacityWatts!.Value);
        Assert.Equal(2, power.PowerSupplies.Count);
        Assert.Equal(3, power.Voltages.Count);
    }

    [Fact]
    public void GpuMetrics_report_has_temperature_and_utilization_per_gpu()
    {
        var report = At(T0).GetMetricReport(TelemetryGenerator.GpuMetricsReport);

        Assert.NotNull(report);
        Assert.Equal(8, report!.MetricValues.Count);   // 4 GPUs x (temp + utilization)
        Assert.Contains(report.MetricValues, m => m.MetricId == "GPU1Temperature");
        Assert.Contains(report.MetricValues, m => m.MetricId == "GPU1UtilizationPercent");
    }

    [Fact]
    public void Readings_change_over_time()
    {
        var early = At(T0).GetMetricReport(TelemetryGenerator.PowerReport)!;
        var later = At(T0.AddSeconds(7)).GetMetricReport(TelemetryGenerator.PowerReport)!;

        var earlyWatts = double.Parse(early.MetricValues[0].MetricValue, CultureInfo.InvariantCulture);
        var laterWatts = double.Parse(later.MetricValues[0].MetricValue, CultureInfo.InvariantCulture);

        Assert.NotEqual(earlyWatts, laterWatts);
    }

    [Fact]
    public void Unknown_report_and_chassis_return_null()
    {
        var generator = At(T0);

        Assert.Null(generator.GetMetricReport("NoSuchReport"));
        Assert.Null(generator.GetThermal("99"));
        Assert.Null(generator.GetPower("99"));
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeSeed : IInventorySeedSource
    {
        public IReadOnlyList<SystemSeed> Systems() => [new SystemSeed { Id = "1" }];

        public IReadOnlyList<Processor> Processors() =>
        [
            Proc("CPU1", ProcessorType.CPU), Proc("CPU2", ProcessorType.CPU),
            Proc("GPU1", ProcessorType.GPU), Proc("GPU2", ProcessorType.GPU),
            Proc("GPU3", ProcessorType.GPU), Proc("GPU4", ProcessorType.GPU),
        ];

        public IReadOnlyList<Memory> MemoryModules() => [];
        public IReadOnlyList<PCIeDevice> PCIeDevices() => [];

        public IReadOnlyList<Chassis> Chassis() =>
        [
            new Chassis { ODataId = "/redfish/v1/Chassis/1", ODataType = "#Chassis.v1.Chassis", Id = "1" },
        ];

        private static Processor Proc(string id, ProcessorType type) => new()
        {
            ODataId = $"/redfish/v1/Systems/1/Processors/{id}",
            ODataType = "#Processor.v1.Processor",
            Id = id,
            Socket = id,
            ProcessorType = type,
        };
    }
}
