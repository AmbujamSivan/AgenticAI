using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.State;

namespace RedfishEmulator.Core.Diagnostics.FaultInjection;

/// <summary>
/// A reproducible hardware failure mode that can be injected into the emulated
/// platform to drive diagnostics and telemetry into edge-case states. Each profile
/// knows how to <see cref="Apply"/> its fault to a target component and how to
/// <see cref="Restore"/> that component to health.
/// </summary>
public interface IFaultProfile
{
    /// <summary>Stable identifier, e.g. <c>GpuOffBus</c>.</summary>
    string Id { get; }

    string Name { get; }
    string Description { get; }

    /// <summary>Kind of component this profile targets, e.g. <c>GPU</c>, <c>Memory</c>.</summary>
    string TargetKind { get; }

    /// <summary>
    /// Applies the fault to an eligible component and returns that component's id,
    /// or <c>null</c> if no eligible component exists.
    /// </summary>
    string? Apply(IComponentRepository repository);

    /// <summary>Restores the previously-affected component to a healthy state.</summary>
    void Restore(IComponentRepository repository, string componentId);
}

/// <summary>
/// Base class for fault profiles: provides the shared "mark a component faulted"
/// and "restore to healthy" mechanics so each concrete profile only declares its
/// target and its condition.
/// </summary>
public abstract class FaultProfile : IFaultProfile
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string TargetKind { get; }

    public abstract string? Apply(IComponentRepository repository);

    public virtual void Restore(IComponentRepository repository, string componentId)
    {
        if (repository.DiagnosableComponents.FirstOrDefault(c => c.Id == componentId) is { } component)
        {
            component.Status.State = ResourceState.Enabled;
            component.Status.Health = Health.OK;
            component.Status.Conditions = null;
        }
    }

    /// <summary>Marks a component faulted with a condition, returning its id (or null).</summary>
    protected static string? Fault(
        IDiagnosable? target, Health health, ResourceState state, string messageId, string message)
    {
        if (target is null)
        {
            return null;
        }

        target.Status.State = state;
        target.Status.Health = health;
        target.Status.Conditions =
        [
            new Condition { MessageId = messageId, Message = message, Severity = health },
        ];

        return target.Id;
    }
}
