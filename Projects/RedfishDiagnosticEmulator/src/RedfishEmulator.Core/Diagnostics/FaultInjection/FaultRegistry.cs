using RedfishEmulator.Core.State;

namespace RedfishEmulator.Core.Diagnostics.FaultInjection;

/// <summary>
/// Default <see cref="IFaultRegistry"/>. Tracks which profiles are active and the
/// component each is applied to, mutating the shared <see cref="IComponentRepository"/>.
/// Activation and clearing are guarded by a lock so concurrent toggle/diagnostic
/// requests see consistent state.
/// </summary>
public sealed class FaultRegistry : IFaultRegistry
{
    private readonly IReadOnlyDictionary<string, IFaultProfile> _profiles;
    private readonly IComponentRepository _repository;
    private readonly Dictionary<string, string> _activeAffectedIds = new();
    private readonly Lock _gate = new();

    public FaultRegistry(IEnumerable<IFaultProfile> profiles, IComponentRepository repository)
    {
        _profiles = profiles.ToDictionary(p => p.Id);
        _repository = repository;
    }

    public IReadOnlyList<FaultProfileStatus> Status()
    {
        lock (_gate)
        {
            return _profiles.Values.Select(StatusFor).ToList();
        }
    }

    public FaultProfileStatus? Activate(string profileId)
    {
        lock (_gate)
        {
            if (!_profiles.TryGetValue(profileId, out var profile))
            {
                return null;
            }

            if (!_activeAffectedIds.ContainsKey(profileId) &&
                profile.Apply(_repository) is { } affectedId)
            {
                _activeAffectedIds[profileId] = affectedId;
            }

            return StatusFor(profile);
        }
    }

    public FaultProfileStatus? Clear(string profileId)
    {
        lock (_gate)
        {
            if (!_profiles.TryGetValue(profileId, out var profile))
            {
                return null;
            }

            if (_activeAffectedIds.Remove(profileId, out var affectedId))
            {
                profile.Restore(_repository, affectedId);
            }

            return StatusFor(profile);
        }
    }

    public void ClearAll()
    {
        lock (_gate)
        {
            foreach (var (profileId, affectedId) in _activeAffectedIds.ToList())
            {
                _profiles[profileId].Restore(_repository, affectedId);
            }

            _activeAffectedIds.Clear();
        }
    }

    private FaultProfileStatus StatusFor(IFaultProfile profile) => new()
    {
        Id = profile.Id,
        Name = profile.Name,
        Description = profile.Description,
        TargetKind = profile.TargetKind,
        Active = _activeAffectedIds.ContainsKey(profile.Id),
        AffectedComponentId = _activeAffectedIds.GetValueOrDefault(profile.Id),
    };
}
