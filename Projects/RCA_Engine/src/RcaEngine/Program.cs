using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RcaEngine.Agents;
using RcaEngine.Llm;
using RcaEngine.Reporting;
using RcaEngine.Telemetry;
using RcaEngine.Triage;

const string Usage =
    """
    Agentic RCA & Failure Triage Engine

    Usage:
      rca-engine <bundle-dir> [options]

    Options:
      --no-llm             Run the deterministic triage path only (no LLM required)
      --output <dir>       Output directory for report.json / report.md
                           (default: <bundle-dir>/rca_output)
      --provider <name>    Override LLM provider: Ollama | OpenAI | AzureOpenAI
      --model <id>         Override model id / deployment name for the provider

    Examples:
      rca-engine samples/bundle-memory-ce-storm
      rca-engine samples/bundle-nvme-controller-failure --no-llm
      rca-engine samples/bundle-pcie-link-degrade --provider Ollama --model llama3.2
    """;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine(Usage);
    return args.Length == 0 ? 1 : 0;
}

var bundleDir = args[0];
var noLlm = args.Contains("--no-llm");
string? outputDir = GetOptionValue(args, "--output");
string? providerOverride = GetOptionValue(args, "--provider");
string? modelOverride = GetOptionValue(args, "--model");

using var loggerFactory = LoggerFactory.Create(b => b
    .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; })
    .SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger("RcaEngine");

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "RCA_")
    .Build();

var llmOptions = config.GetSection("Llm").Get<LlmOptions>() ?? new LlmOptions();
if (providerOverride is not null) llmOptions.Provider = providerOverride;
if (modelOverride is not null)
{
    llmOptions.Ollama.ModelId = modelOverride;
    llmOptions.OpenAI.ModelId = modelOverride;
    llmOptions.AzureOpenAI.DeploymentName = modelOverride;
}

logger.LogInformation("Loading diagnostic bundle from {BundleDir}", bundleDir);
var bundle = DiagnosticBundle.Load(bundleDir);
logger.LogInformation(
    "Bundle loaded: node={NodeId} incident={IncidentId} — {RedfishCount} Redfish events, {PcieCount} PCIe devices, {DmesgCount} dmesg lines",
    bundle.Metadata.NodeId, bundle.Metadata.IncidentId,
    bundle.RedfishEvents.Count, bundle.PcieDevices.Count, bundle.DmesgLines.Count);

var report = noLlm
    ? DeterministicTriage.Run(bundle)
    : await new RcaOrchestrator(llmOptions, logger).RunAsync(bundle);

if (noLlm)
{
    report.IncidentId = bundle.Metadata.IncidentId;
    report.NodeId = bundle.Metadata.NodeId;
}

outputDir ??= Path.Combine(bundleDir, "rca_output");
Directory.CreateDirectory(outputDir);

var jsonPath = Path.Combine(outputDir, "report.json");
var mdPath = Path.Combine(outputDir, "report.md");
await File.WriteAllTextAsync(jsonPath,
    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
var markdown = MarkdownReportRenderer.Render(report);
await File.WriteAllTextAsync(mdPath, markdown);

Console.WriteLine();
Console.WriteLine(markdown);
logger.LogInformation("Reports written: {JsonPath}, {MdPath}", jsonPath, mdPath);
return 0;

static string? GetOptionValue(string[] args, string option)
{
    var index = Array.IndexOf(args, option);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
