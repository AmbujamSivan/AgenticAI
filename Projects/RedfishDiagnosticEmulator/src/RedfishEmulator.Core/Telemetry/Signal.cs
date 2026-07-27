namespace RedfishEmulator.Core.Telemetry;

/// <summary>
/// Deterministic, bounded signal helpers used to synthesize plausible, time-varying
/// sensor readings. A reading is a baseline plus a sinusoid, so repeated polls show
/// smooth movement within a fixed envelope rather than random jumps.
/// </summary>
internal static class Signal
{
    /// <summary>
    /// A value oscillating around <paramref name="baseline"/> by ±<paramref name="amplitude"/>
    /// with the given period, offset by a per-sensor <paramref name="phase"/>.
    /// </summary>
    public static double Oscillate(
        double baseline, double amplitude, double periodSeconds, double phase, double seconds) =>
        baseline + amplitude * Math.Sin((2.0 * Math.PI * seconds / periodSeconds) + phase);

    /// <summary>A stable per-key phase in [0, 2π) so different sensors move out of lockstep.</summary>
    public static double PhaseFor(string key)
    {
        var hash = 17;
        foreach (var c in key)
        {
            hash = (hash * 31) + c;
        }

        return Math.Abs(hash) % 1000 / 1000.0 * 2.0 * Math.PI;
    }

    /// <summary>Constrains a value to the inclusive range [min, max].</summary>
    public static double Clamp(double value, double min, double max) =>
        Math.Clamp(value, min, max);
}
