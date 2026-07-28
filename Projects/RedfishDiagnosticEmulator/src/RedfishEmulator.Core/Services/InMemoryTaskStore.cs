using System.Collections.Concurrent;
using RedfishEmulator.Core.Models;

namespace RedfishEmulator.Core.Services;

/// <summary>
/// Thread-safe in-memory <see cref="ITaskStore"/>. Task ids are monotonically
/// increasing integers, matching the opaque-but-ordered ids typical of a BMC.
/// </summary>
public sealed class InMemoryTaskStore(TimeProvider time) : ITaskStore
{
    private const string TasksBase = "/redfish/v1/TaskService/Tasks";

    private readonly ConcurrentDictionary<string, RedfishTask> _tasks = new();
    private int _nextId;

    public RedfishTask Create(string name)
    {
        var id = Interlocked.Increment(ref _nextId).ToString();

        var task = new RedfishTask
        {
            ODataId = $"{TasksBase}/{id}",
            ODataType = "#Task.v1_7_3.Task",
            ODataContext = "/redfish/v1/$metadata#Task.Task",
            Id = id,
            Name = name,
            TaskState = TaskState.New,
            StartTime = time.GetUtcNow(),
        };

        _tasks[id] = task;
        return task;
    }

    public RedfishTask? Get(string taskId) => _tasks.GetValueOrDefault(taskId);

    public IReadOnlyList<RedfishTask> All() =>
        _tasks.Values.OrderBy(t => int.Parse(t.Id!)).ToList();
}
