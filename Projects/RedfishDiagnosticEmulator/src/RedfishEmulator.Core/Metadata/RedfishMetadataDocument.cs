namespace RedfishEmulator.Core.Metadata;

/// <summary>
/// Produces the Redfish OData CSDL/EDMX metadata document served at
/// <c>/redfish/v1/$metadata</c>. Each exposed schema is declared as an
/// <c>edmx:Reference</c> to its DMTF standard schema, plus the service's own
/// <c>EntityContainer</c> describing the top-level resources.
/// </summary>
/// <remarks>
/// Registering a new resource type is a one-line <see cref="SchemaReferences"/>
/// entry, so the metadata document stays in step with the controllers.
/// </remarks>
public static class RedfishMetadataDocument
{
    private const string DmtfSchemaBase = "http://redfish.dmtf.org/schemas/v1";

    /// <summary>Schema types the service references, in document order.</summary>
    private static readonly string[] SchemaReferences =
    [
        "ServiceRoot",
        "ComputerSystemCollection",
        "ComputerSystem",
        "ProcessorCollection",
        "Processor",
        "MemoryCollection",
        "Memory",
        "PCIeDeviceCollection",
        "PCIeDevice",
        "ChassisCollection",
        "Chassis",
        "Thermal",
        "Power",
        "TelemetryService",
        "MetricReportCollection",
        "MetricReport",
        "TaskService",
        "SessionService",
    ];

    /// <summary>Renders the full EDMX document as XML.</summary>
    public static string Build()
    {
        var references = string.Join("\n", SchemaReferences.Select(schema =>
            $"""
              <edmx:Reference Uri="{DmtfSchemaBase}/{schema}_v1.xml">
                <edmx:Include Namespace="{schema}" />
              </edmx:Reference>
            """));

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <edmx:Edmx xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx" Version="4.0">
              <edmx:Reference Uri="{DmtfSchemaBase}/RedfishExtensions_v1.xml">
                <edmx:Include Namespace="RedfishExtensions.v1_0_0" Alias="Redfish" />
              </edmx:Reference>
            {references}
              <edmx:DataServices>
                <Schema xmlns="http://docs.oasis-open.org/odata/ns/edm" Namespace="Service">
                  <EntityContainer Name="Service" Extends="ServiceRoot.v1_0_0.ServiceContainer" />
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;
    }
}
