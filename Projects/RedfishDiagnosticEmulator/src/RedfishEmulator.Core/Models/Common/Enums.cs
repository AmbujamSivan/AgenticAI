using System.Text.Json.Serialization;

namespace RedfishEmulator.Core.Models.Common;

/// <summary>The kind of a <c>Processor</c> resource (Redfish <c>ProcessorType</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProcessorType
{
    CPU,
    GPU,
    FPGA,
    DSP,
    Accelerator,
    Other,
}

/// <summary>Power state of a system or chassis (Redfish <c>PowerState</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PowerState
{
    On,
    Off,
    PoweringOn,
    PoweringOff,
}
