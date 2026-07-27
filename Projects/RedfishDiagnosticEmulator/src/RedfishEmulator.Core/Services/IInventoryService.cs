using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Services;

/// <summary>
/// Read model over the emulated hardware inventory. Returns Redfish resources and
/// collections; a <c>null</c> return means the requested resource does not exist
/// and the caller should surface an HTTP 404.
/// </summary>
public interface IInventoryService
{
    ResourceCollection GetSystems();
    ComputerSystem? GetSystem(string systemId);

    ResourceCollection? GetProcessors(string systemId);
    Processor? GetProcessor(string systemId, string processorId);

    ResourceCollection? GetMemory(string systemId);
    Memory? GetMemoryModule(string systemId, string memoryId);

    ResourceCollection? GetPCIeDevices(string systemId);
    PCIeDevice? GetPCIeDevice(string systemId, string deviceId);

    ResourceCollection GetChassisCollection();
    Chassis? GetChassis(string chassisId);
}
