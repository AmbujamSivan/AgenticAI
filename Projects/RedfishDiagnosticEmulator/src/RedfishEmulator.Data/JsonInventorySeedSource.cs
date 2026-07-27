using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using RedfishEmulator.Core.Inventory;
using RedfishEmulator.Core.Models;

namespace RedfishEmulator.Data;

/// <summary>
/// <see cref="IInventorySeedSource"/> backed by JSON documents embedded in this
/// assembly (under <c>Seed/</c>). Swapping those JSON files re-models the emulated
/// hardware without touching code. All documents are parsed once at construction.
/// </summary>
public sealed class JsonInventorySeedSource : IInventorySeedSource
{
    private const string SeedNamespace = "RedfishEmulator.Data.Seed";

    private static readonly JsonSerializerOptions SeedOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IReadOnlyList<SystemSeed> _systems;
    private readonly IReadOnlyList<Processor> _processors;
    private readonly IReadOnlyList<Memory> _memory;
    private readonly IReadOnlyList<PCIeDevice> _pcieDevices;
    private readonly IReadOnlyList<Chassis> _chassis;

    public JsonInventorySeedSource()
    {
        _systems = Load<SystemSeed>("systems.json");
        _processors = Load<Processor>("processors.json");
        _memory = Load<Memory>("memory.json");
        _pcieDevices = Load<PCIeDevice>("pcie-devices.json");
        _chassis = Load<Chassis>("chassis.json");
    }

    public IReadOnlyList<SystemSeed> Systems() => _systems;
    public IReadOnlyList<Processor> Processors() => _processors;
    public IReadOnlyList<Memory> MemoryModules() => _memory;
    public IReadOnlyList<PCIeDevice> PCIeDevices() => _pcieDevices;
    public IReadOnlyList<Chassis> Chassis() => _chassis;

    private static IReadOnlyList<T> Load<T>(string fileName)
    {
        var resource = $"{SeedNamespace}.{fileName}";
        var assembly = typeof(JsonInventorySeedSource).Assembly;

        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException(
                $"Embedded seed resource '{resource}' was not found. " +
                $"Available: {string.Join(", ", assembly.GetManifestResourceNames())}");

        return JsonSerializer.Deserialize<List<T>>(stream, SeedOptions)
            ?? throw new InvalidOperationException($"Seed resource '{resource}' deserialized to null.");
    }
}
