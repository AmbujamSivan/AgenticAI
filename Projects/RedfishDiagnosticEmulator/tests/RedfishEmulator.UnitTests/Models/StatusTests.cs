using System.Text.Json;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.UnitTests.Models;

/// <summary>Unit tests for the Redfish <see cref="Status"/> value object.</summary>
public sealed class StatusTests
{
    [Fact]
    public void Ok_factory_produces_enabled_healthy_status()
    {
        var status = Status.Ok();

        Assert.Equal(ResourceState.Enabled, status.State);
        Assert.Equal(Health.OK, status.Health);
    }

    [Fact]
    public void Health_serializes_as_redfish_string_enum()
    {
        var json = JsonSerializer.Serialize(new Status { Health = Health.Critical });

        // Redfish requires the literal token "Critical", not the numeric enum value.
        Assert.Contains("\"Health\":\"Critical\"", json);
    }
}
