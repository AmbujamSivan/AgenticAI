using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using RedfishEmulator.Api.Auth;
using RedfishEmulator.Api.Filters;
using RedfishEmulator.Core.Diagnostics;
using RedfishEmulator.Core.Diagnostics.FaultInjection;
using RedfishEmulator.Core.Diagnostics.Passes;
using RedfishEmulator.Core.Inventory;
using RedfishEmulator.Core.Services;
using RedfishEmulator.Core.State;
using RedfishEmulator.Core.Telemetry;
using RedfishEmulator.Data;

var builder = WebApplication.CreateBuilder(args);

// Inventory: JSON seed (Data) → mutable component repository → inventory read service.
// Singletons so the seeded component state persists for the process lifetime and is
// shared by every reader (inventory, telemetry, diagnostics mutate the same objects).
builder.Services.AddSingleton<IInventorySeedSource, JsonInventorySeedSource>();
builder.Services.AddSingleton<IComponentRepository, InMemoryComponentRepository>();
builder.Services.AddSingleton<IInventoryService, InventoryService>();

// Telemetry: time-varying sensor generator driven by the system clock.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITelemetryGenerator, TelemetryGenerator>();

// Diagnostics: pluggable per-category passes → engine → async task orchestration.
builder.Services.AddSingleton<IDiagnosticPass, CpuDiagnosticPass>();
builder.Services.AddSingleton<IDiagnosticPass, GpuDiagnosticPass>();
builder.Services.AddSingleton<IDiagnosticPass, MemoryDiagnosticPass>();
builder.Services.AddSingleton<IDiagnosticPass, PCIeDiagnosticPass>();
builder.Services.AddSingleton<IDiagnosticEngine, DiagnosticEngine>();
builder.Services.AddSingleton<ITaskStore, InMemoryTaskStore>();
builder.Services.AddSingleton<IDiagnosticService, DiagnosticService>();

// Fault injection: pluggable failure-mode profiles + registry that toggles them.
builder.Services.AddSingleton<IFaultProfile, GpuOffBusFault>();
builder.Services.AddSingleton<IFaultProfile, PcieLinkDownFault>();
builder.Services.AddSingleton<IFaultProfile, MemoryEccFault>();
builder.Services.AddSingleton<IFaultProfile, ThermalTripFault>();
builder.Services.AddSingleton<IFaultRegistry, FaultRegistry>();

// Authentication: Redfish sessions (X-Auth-Token) + HTTP Basic. Every endpoint
// requires an authenticated user (fallback policy) except those marked [AllowAnonymous]
// (ServiceRoot, metadata, and session creation).
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddSingleton<RedfishCredentials>();
builder.Services
    .AddAuthentication(RedfishAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, RedfishAuthenticationHandler>(
        RedfishAuthenticationHandler.SchemeName, configureOptions: null);
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services
    .AddControllers(options => options.Filters.Add<RedfishErrorResultFilter>())
    // Don't let [ApiController] rewrite 4xx results into ProblemDetails; our filter
    // turns them into Redfish error bodies instead.
    .ConfigureApiBehaviorOptions(options => options.SuppressMapClientErrors = true)
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Convenience: send the bare root to the Redfish service entry point.
app.MapGet("/", () => Results.Redirect("/redfish/v1")).ExcludeFromDescription().AllowAnonymous();

app.Run();

/// <summary>
/// Exposed so the integration-test projects can bootstrap the API host via
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
