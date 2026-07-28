using Microsoft.AspNetCore.Mvc;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.Services;

namespace RedfishEmulator.Api.Controllers;

/// <summary>Serves the Redfish TaskService and the collection of asynchronous tasks.</summary>
[ApiController]
[Produces("application/json")]
public sealed class TaskServiceController(ITaskStore tasks) : ControllerBase
{
    private const string Base = "/redfish/v1/TaskService";

    [HttpGet(Base)]
    public TaskServiceResource GetTaskService() => new()
    {
        ODataId = Base,
        ODataType = "#TaskService.v1_2_0.TaskService",
        ODataContext = "/redfish/v1/$metadata#TaskService.TaskService",
        Id = "TaskService",
        Name = "Task Service",
        Tasks = new NavigationLink($"{Base}/Tasks"),
    };

    [HttpGet(Base + "/Tasks")]
    public ResourceCollection GetTasks() => ResourceCollection.Of(
        $"{Base}/Tasks", "TaskCollection", "Task Collection",
        tasks.All().Select(t => t.ODataId));

    [HttpGet(Base + "/Tasks/{taskId}")]
    public ActionResult<RedfishTask> GetTask(string taskId) =>
        tasks.Get(taskId) is { } task ? task : NotFound();
}
