namespace RedfishEmulator.Core.Diagnostics.FaultInjection;

/// <summary>The current state of a fault profile.</summary>
public sealed class FaultProfileStatus
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string TargetKind { get; init; }
    public bool Active { get; init; }

    /// <summary>Id of the component the fault is currently applied to, when active.</summary>
    public string? AffectedComponentId { get; init; }
}

/// <summary>
/// Manages the set of fault profiles and their active state, applying and reverting
/// them against the shared component repository.
/// </summary>
public interface IFaultRegistry
{
    /// <summary>Current state of every known profile.</summary>
    IReadOnlyList<FaultProfileStatus> Status();

    /// <summary>Activates a profile (idempotent); <c>null</c> if the profile id is unknown.</summary>
    FaultProfileStatus? Activate(string profileId);

    /// <summary>Clears a profile (idempotent); <c>null</c> if the profile id is unknown.</summary>
    FaultProfileStatus? Clear(string profileId);

    /// <summary>Clears every active fault, returning the platform to health.</summary>
    void ClearAll();
}
