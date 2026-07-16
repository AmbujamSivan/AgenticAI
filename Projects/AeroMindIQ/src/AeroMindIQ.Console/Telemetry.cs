using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AeroMindIQ.Console;

/// <summary>
/// Wires OpenTelemetry tracing to Langfuse's OTLP ingestion endpoint. Subscribes to
/// Semantic Kernel's own ActivitySource ("Microsoft.SemanticKernel*") so every kernel
/// function invocation and chat completion becomes a span automatically, plus a custom
/// ActivitySource used for one root span per cycle (see Program.cs) so every agent call
/// nests under a single trace in Langfuse's UI instead of showing up as disconnected spans.
/// </summary>
public static class Telemetry
{
    private const string ServiceName = "AeroMindIQ";
    public static readonly ActivitySource ActivitySource = new(ServiceName);

    public static TracerProvider? Build(string? otlpEndpoint, string? publicKey, string? secretKey)
    {
        if (string.IsNullOrWhiteSpace(otlpEndpoint) || string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            System.Console.Error.WriteLine(
                "[Telemetry] Langfuse:OtlpEndpoint/PublicKey/SecretKey not configured — tracing disabled " +
                "for this run. Set them in appsettings.Development.json to send traces to Langfuse.");
            return null;
        }

        // Langfuse's OTEL endpoint authenticates via HTTP Basic Auth using the
        // project's public/secret key pair, base64-encoded exactly like any other
        // Basic Auth header.
        var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}"));

        return Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ServiceName))
            .AddSource(ServiceName)
            .AddSource("Microsoft.SemanticKernel*")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
                options.Headers = $"Authorization=Basic {authHeader}";
            })
            .Build();
    }
}
