using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>
/// The Redfish <c>TaskService</c> at <c>/redfish/v1/TaskService</c> — the manager of
/// asynchronous tasks, linking to the collection of active and completed tasks.
/// </summary>
public sealed class TaskServiceResource : ResourceBase
{
    public bool ServiceEnabled { get; set; } = true;

    public Status Status { get; set; } = Status.Ok();

    public NavigationLink? Tasks { get; set; }
}
