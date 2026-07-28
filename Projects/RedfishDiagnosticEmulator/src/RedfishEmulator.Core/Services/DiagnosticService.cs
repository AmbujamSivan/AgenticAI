using RedfishEmulator.Core.Diagnostics;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Services;

/// <summary>
/// Default <see cref="IDiagnosticService"/>. Runs the diagnostic engine and captures
/// the report in a completed <see cref="RedfishTask"/> — summary and per-component
/// findings as Redfish <see cref="Message"/>s, the structured report under <c>Oem</c>.
/// </summary>
/// <remarks>
/// The run completes synchronously, so the returned task is already <c>Completed</c>;
/// the async task/monitor contract is still honoured (the action replies 202 with the
/// task location). A real BMC would execute the passes on a background worker.
/// </remarks>
public sealed class DiagnosticService(
    IDiagnosticEngine engine,
    ITaskStore tasks,
    TimeProvider time) : IDiagnosticService
{
    public RedfishTask? StartDiagnostics(string systemId)
    {
        var report = engine.Run(systemId);
        if (report is null)
        {
            return null;
        }

        var task = tasks.Create($"Diagnostic run for system {systemId}");
        task.TaskState = TaskState.Completed;
        task.PercentComplete = 100;
        task.EndTime = time.GetUtcNow();
        task.TaskStatus = report.OverallHealth;
        task.Messages = BuildMessages(report);
        task.Oem = new Dictionary<string, object> { ["RedfishEmulator"] = new { DiagnosticReport = report } };

        return task;
    }

    private static List<Message> BuildMessages(DiagnosticReport report)
    {
        var messages = new List<Message>
        {
            new()
            {
                MessageId = "RedfishEmulator.1.0.DiagnosticComplete",
                Text = $"Diagnostics completed for system {report.SystemId}: " +
                       $"{report.PassedCount} passed, {report.WarningCount} warning(s), {report.FailedCount} failed.",
                Severity = report.OverallHealth,
            },
        };

        messages.AddRange(report.Results
            .Where(r => r.Outcome != DiagnosticOutcome.Pass)
            .Select(r => new Message
            {
                MessageId = r.Outcome == DiagnosticOutcome.Fail
                    ? "RedfishEmulator.1.0.ComponentFailed"
                    : "RedfishEmulator.1.0.ComponentDegraded",
                Text = $"[{r.Category}] {r.Message}",
                Severity = r.Outcome == DiagnosticOutcome.Fail ? Health.Critical : Health.Warning,
            }));

        return messages;
    }
}
