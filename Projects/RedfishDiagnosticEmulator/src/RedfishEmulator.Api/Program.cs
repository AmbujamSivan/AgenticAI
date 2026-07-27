using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using RedfishEmulator.Core.Inventory;
using RedfishEmulator.Core.Services;
using RedfishEmulator.Core.Telemetry;
using RedfishEmulator.Data;

var builder = WebApplication.CreateBuilder(args);

// Inventory: JSON seed source (Data) → inventory read service (Core).
// Singletons so the seeded component state persists for the process lifetime
// (later phases mutate that state during diagnostics and fault injection).
builder.Services.AddSingleton<IInventorySeedSource, JsonInventorySeedSource>();
builder.Services.AddSingleton<IInventoryService, InventoryService>();

// Telemetry: time-varying sensor generator driven by the system clock.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITelemetryGenerator, TelemetryGenerator>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Redfish resources use PascalCase property names and string-valued enums.
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            JsonIgnoreCondition.WhenWritingNull;
    });

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Redfish Diagnostic & Telemetry Emulator",
        Version = "v1",
        Description =
            "A mock BMC (Baseboard Management Controller) exposing DMTF Redfish " +
            "REST APIs for component inventory, telemetry and automated diagnostics.",
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Redfish Emulator v1");
        options.DocumentTitle = "Redfish Emulator — API Explorer";
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Convenience: send the bare root to the Redfish service entry point.
app.MapGet("/", () => Results.Redirect("/redfish/v1")).ExcludeFromDescription();

app.Run();

/// <summary>
/// Exposed so the integration-test projects can bootstrap the API host via
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
