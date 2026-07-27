using System.Globalization;
using RedfishEmulator.Core.Inventory;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Telemetry;

/// <summary>
/// Default <see cref="ITelemetryGenerator"/>. Synthesizes chassis thermal/power and
/// metric-report data from the seeded inventory, driven by a <see cref="TimeProvider"/>
/// so readings move over time and tests can supply a fixed clock. Sensor health is
/// derived from each reading against its critical threshold, so pushing a reading past
/// the threshold (Phase 5 fault injection) naturally turns the sensor Critical.
/// </summary>
public sealed class TelemetryGenerator : ITelemetryGenerator
{
    // Metric report identifiers.
    public const string CpuMetricsReport = "CPUMetrics";
    public const string GpuMetricsReport = "GPUMetrics";
    public const string PowerReport = "PlatformPowerUsage";

    private const double CpuCriticalC = 95.0;
    private const double GpuCriticalC = 90.0;

    private readonly IReadOnlyList<Processor> _cpus;
    private readonly IReadOnlyList<Processor> _gpus;
    private readonly HashSet<string> _chassisIds;
    private readonly TimeProvider _time;

    public TelemetryGenerator(IInventorySeedSource seed, TimeProvider time)
    {
        _time = time;
        var processors = seed.Processors();
        _cpus = processors.Where(p => p.ProcessorType == ProcessorType.CPU).ToList();
        _gpus = processors.Where(p => p.ProcessorType == ProcessorType.GPU).ToList();
        _chassisIds = seed.Chassis().Select(c => c.Id!).ToHashSet();
    }

    public IReadOnlyList<string> ReportIds { get; } =
        [CpuMetricsReport, GpuMetricsReport, PowerReport];

    private double Seconds => (_time.GetUtcNow() - DateTimeOffset.UnixEpoch).TotalSeconds;

    public Thermal? GetThermal(string chassisId)
    {
        if (!_chassisIds.Contains(chassisId))
        {
            return null;
        }

        var s = Seconds;
        var basePath = $"/redfish/v1/Chassis/{chassisId}/Thermal";
        var temps = new List<Temperature>();
        var member = 0;

        temps.Add(TemperatureSensor(member++, "Inlet Temp", IntakeTemp(s), null, "Intake"));
        foreach (var cpu in _cpus)
        {
            temps.Add(TemperatureSensor(member++, $"{cpu.Socket} Temp", CpuTemp(cpu.Id!, s), CpuCriticalC, "CPU"));
        }

        foreach (var gpu in _gpus)
        {
            temps.Add(TemperatureSensor(member++, $"{gpu.Socket} Temp", GpuTemp(gpu.Id!, s), GpuCriticalC, "GPU"));
        }

        var fans = Enumerable.Range(1, 4)
            .Select(i => new Fan
            {
                MemberId = (i - 1).ToString(CultureInfo.InvariantCulture),
                Name = $"Fan {i}",
                Reading = Math.Round(Signal.Clamp(
                    Signal.Oscillate(8500, 1500, 40, Signal.PhaseFor($"fan{i}"), s), 0, 15000)),
            })
            .ToList();

        return new Thermal
        {
            ODataId = basePath,
            ODataType = "#Thermal.v1_7_1.Thermal",
            ODataContext = "/redfish/v1/$metadata#Thermal.Thermal",
            Id = "Thermal",
            Name = "Thermal",
            Temperatures = temps,
            Fans = fans,
        };
    }

    public Power? GetPower(string chassisId)
    {
        if (!_chassisIds.Contains(chassisId))
        {
            return null;
        }

        var s = Seconds;
        var basePath = $"/redfish/v1/Chassis/{chassisId}/Power";

        return new Power
        {
            ODataId = basePath,
            ODataType = "#Power.v1_7_1.Power",
            ODataContext = "/redfish/v1/$metadata#Power.Power",
            Id = "Power",
            Name = "Power",
            PowerControl =
            [
                new PowerControl
                {
                    MemberId = "0",
                    Name = "System Power Control",
                    PowerConsumedWatts = Math.Round(TotalPowerWatts(s)),
                    PowerCapacityWatts = 4800,
                },
            ],
            Voltages =
            [
                VoltageRail("0", "12V Rail", 12.0, 0.15, s),
                VoltageRail("1", "5V Rail", 5.0, 0.08, s),
                VoltageRail("2", "3.3V Rail", 3.3, 0.05, s),
            ],
            PowerSupplies =
            [
                new PowerSupply { MemberId = "0", Name = "PSU 1", LineInputVoltage = 230, PowerCapacityWatts = 2400 },
                new PowerSupply { MemberId = "1", Name = "PSU 2", LineInputVoltage = 230, PowerCapacityWatts = 2400 },
            ],
        };
    }

    public MetricReport? GetMetricReport(string reportId)
    {
        if (!ReportIds.Contains(reportId))
        {
            return null;
        }

        var s = Seconds;
        var now = _time.GetUtcNow();
        var values = reportId switch
        {
            CpuMetricsReport => _cpus.SelectMany(c => ProcessorMetrics(c, CpuTemp(c.Id!, s), CpuUtil(c.Id!, s), now)),
            GpuMetricsReport => _gpus.SelectMany(g => ProcessorMetrics(g, GpuTemp(g.Id!, s), GpuUtil(g.Id!, s), now)),
            PowerReport => PowerMetrics(s, now),
            _ => [],
        };

        return new MetricReport
        {
            ODataId = $"/redfish/v1/TelemetryService/MetricReports/{reportId}",
            ODataType = "#MetricReport.v1_5_0.MetricReport",
            ODataContext = "/redfish/v1/$metadata#MetricReport.MetricReport",
            Id = reportId,
            Name = $"{reportId} Metric Report",
            Timestamp = now,
            MetricValues = values.ToList(),
        };
    }

    // --- per-sensor signal functions (baseline/amplitude/period tuned for plausibility) ---

    private static double IntakeTemp(double s) => Signal.Oscillate(22, 2, 90, Signal.PhaseFor("intake"), s);
    private static double CpuTemp(string id, double s) => Signal.Oscillate(58, 6, 30, Signal.PhaseFor(id), s);
    private static double GpuTemp(string id, double s) => Signal.Oscillate(68, 8, 20, Signal.PhaseFor(id), s);

    private static double CpuUtil(string id, double s) =>
        Signal.Clamp(Signal.Oscillate(45, 20, 25, Signal.PhaseFor(id), s), 0, 100);

    private static double GpuUtil(string id, double s) =>
        Signal.Clamp(Signal.Oscillate(72, 22, 15, Signal.PhaseFor(id), s), 0, 100);

    private double TotalPowerWatts(double s)
    {
        var cpu = _cpus.Sum(c => 120 + (CpuUtil(c.Id!, s) / 100.0 * 230));
        var gpu = _gpus.Sum(g => 150 + (GpuUtil(g.Id!, s) / 100.0 * 550));
        return 200 + cpu + gpu;   // platform base + compute
    }

    // --- builders ---

    private static Temperature TemperatureSensor(
        int memberId, string name, double reading, double? critical, string context) => new()
    {
        MemberId = memberId.ToString(CultureInfo.InvariantCulture),
        Name = name,
        ReadingCelsius = Math.Round(reading, 1),
        UpperThresholdCritical = critical,
        PhysicalContext = context,
        Status = new Status { State = ResourceState.Enabled, Health = ThermalHealth(reading, critical) },
    };

    private static Voltage VoltageRail(string memberId, string name, double nominal, double tolerance, double s) => new()
    {
        MemberId = memberId,
        Name = name,
        ReadingVolts = Math.Round(Signal.Oscillate(nominal, tolerance, 35, Signal.PhaseFor(name), s), 3),
        PhysicalContext = "VoltageRegulator",
    };

    private static IEnumerable<MetricReading> ProcessorMetrics(
        Processor p, double temp, double util, DateTimeOffset now)
    {
        yield return new MetricReading
        {
            MetricId = $"{p.Id}Temperature",
            MetricValue = Math.Round(temp, 1).ToString(CultureInfo.InvariantCulture),
            Timestamp = now,
            MetricProperty = p.ODataId,
        };
        yield return new MetricReading
        {
            MetricId = $"{p.Id}UtilizationPercent",
            MetricValue = Math.Round(util, 1).ToString(CultureInfo.InvariantCulture),
            Timestamp = now,
            MetricProperty = p.ODataId,
        };
    }

    private IEnumerable<MetricReading> PowerMetrics(double s, DateTimeOffset now)
    {
        yield return new MetricReading
        {
            MetricId = "TotalPowerConsumedWatts",
            MetricValue = Math.Round(TotalPowerWatts(s)).ToString(CultureInfo.InvariantCulture),
            Timestamp = now,
            MetricProperty = "/redfish/v1/Chassis/1/Power#/PowerControl/0/PowerConsumedWatts",
        };

        foreach (var gpu in _gpus)
        {
            var watts = 150 + (GpuUtil(gpu.Id!, s) / 100.0 * 550);
            yield return new MetricReading
            {
                MetricId = $"{gpu.Id}PowerWatts",
                MetricValue = Math.Round(watts).ToString(CultureInfo.InvariantCulture),
                Timestamp = now,
                MetricProperty = gpu.ODataId,
            };
        }
    }

    private static Health ThermalHealth(double reading, double? critical)
    {
        if (critical is not { } limit)
        {
            return Health.OK;
        }

        if (reading >= limit)
        {
            return Health.Critical;
        }

        return reading >= 0.9 * limit ? Health.Warning : Health.OK;
    }
}
