namespace RedfishEmulator.Core.Models.Common;

/// <summary>
/// Aggregates the health of subordinate resources into a single rollup value,
/// following the Redfish rule that the rollup reflects the worst health present
/// (<c>Critical</c> &gt; <c>Warning</c> &gt; <c>OK</c>).
/// </summary>
public static class HealthRollup
{
    /// <summary>Returns the worst health among the supplied values, or <c>OK</c> if none.</summary>
    public static Health Of(IEnumerable<Health?> healths)
    {
        var worst = Health.OK;
        foreach (var health in healths)
        {
            if (health is { } h && h > worst)
            {
                worst = h;
            }
        }

        return worst;
    }

    /// <summary>Rolls up the health of a set of resources' <see cref="Status"/> objects.</summary>
    public static Health Of(IEnumerable<Status> statuses) =>
        Of(statuses.Select(s => s.Health));
}
