using RedfishEmulator.Core.Models;

namespace RedfishEmulator.Core.Services;

/// <summary>Stores the asynchronous tasks the service has created.</summary>
public interface ITaskStore
{
    /// <summary>Creates and stores a new task in the <c>New</c> state.</summary>
    RedfishTask Create(string name);

    RedfishTask? Get(string taskId);

    IReadOnlyList<RedfishTask> All();
}
