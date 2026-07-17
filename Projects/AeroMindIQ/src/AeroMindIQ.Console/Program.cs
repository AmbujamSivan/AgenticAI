using AeroMindIQ.Agents;
using AeroMindIQ.Data;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;

namespace AeroMindIQ.Console;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        using var tracerProvider = Telemetry.Build(
            config["Langfuse:OtlpEndpoint"],
            config["Langfuse:PublicKey"],
            config["Langfuse:SecretKey"]);

        try
        {
            using var cycleActivity = Telemetry.ActivitySource.StartActivity("aeromindiq.cycle");

            var adminConnectionString = config.GetConnectionString("AdminConnection")
                ?? throw new InvalidOperationException("Missing ConnectionStrings:AdminConnection in configuration.");
            var readOnlyConnectionString = config.GetConnectionString("ReadOnlyConnection")
                ?? throw new InvalidOperationException("Missing ConnectionStrings:ReadOnlyConnection in configuration.");

            var providerName = config["LlmProvider"];
            if (string.IsNullOrWhiteSpace(providerName))
            {
                System.Console.Error.WriteLine("Missing LlmProvider in configuration (expected Gemini, Claude, OpenAI, or OpenAICompatible).");
                return 1;
            }

            var providerSection = config.GetSection($"Providers:{providerName}");
            var providerApiKey = providerSection["ApiKey"];
            if (string.IsNullOrWhiteSpace(providerApiKey))
            {
                System.Console.Error.WriteLine(
                    $"Missing Providers:{providerName}:ApiKey. Set it in src/AeroMindIQ.Console/appsettings.Development.json " +
                    "(gitignored) or the corresponding environment variable.");
                return 1;
            }

            var providerConfig = new LlmProviderConfig(
                Provider: providerName,
                ApiKey: providerApiKey,
                BaseUrl: providerSection["BaseUrl"],
                FetcherModel: providerSection["FetcherModel"] ?? throw new InvalidOperationException($"Missing Providers:{providerName}:FetcherModel"),
                ReporterModel: providerSection["ReporterModel"] ?? throw new InvalidOperationException($"Missing Providers:{providerName}:ReporterModel"),
                ReviewerModel: providerSection["ReviewerModel"] ?? throw new InvalidOperationException($"Missing Providers:{providerName}:ReviewerModel"),
                JudgeModel: providerSection["JudgeModel"] ?? throw new InvalidOperationException($"Missing Providers:{providerName}:JudgeModel"));

            System.Console.WriteLine($"LLM provider: {providerName}");

            var mlScoringBaseUrl = config["MlScoringService:BaseUrl"] ?? "http://localhost:8500";
            using var mlHttpClient = new HttpClient { BaseAddress = new Uri(mlScoringBaseUrl), Timeout = TimeSpan.FromSeconds(10) };
            var mlScoringClient = new MlScoringClient(mlHttpClient);

            System.Console.WriteLine("AeroMind IQ — running one detection cycle...");
            System.Console.WriteLine();

            System.Console.WriteLine("Agent A (Auditor): checking production_runs for anomalies (Isolation Forest, falling back to 3-sigma)...");
            var anomalies = await AuditorAgent.CheckForAnomaliesViaModelAsync(adminConnectionString, mlScoringClient);

            if (anomalies.Count == 0)
            {
                System.Console.WriteLine("No anomalies detected. Nothing to investigate.");
                return 0;
            }

            var anomaly = anomalies[0];
            System.Console.WriteLine($"Anomaly triggered: {anomaly.Describe()}");
            System.Console.WriteLine();

            cycleActivity?.SetTag("anomaly.line_id", anomaly.LineId);
            cycleActivity?.SetTag("anomaly.trigger_source", anomaly.TriggerSource);

            var usageTracker = new UsageTracker();

            System.Console.WriteLine("Agent B (Fetcher): gathering supporting evidence via safe SQL (reviewed by Agent Reviewer)...");
            var schemaDescription = await SchemaReader.DescribeSchemaAsync(adminConnectionString);

            var reviewerChat = LlmKernelFactory.CreateChatCompletionService(providerConfig, providerConfig.ReviewerModel);
            var reviewer = new ReviewerAgent(reviewerChat, providerConfig.ReviewerModel);

            var fetcherChat = LlmKernelFactory.CreateChatCompletionService(providerConfig, providerConfig.FetcherModel);
            var fetcher = new FetcherAgent(fetcherChat, providerConfig.FetcherModel, readOnlyConnectionString, schemaDescription, reviewer);
            var fetcherResult = await fetcher.InvestigateAsync(anomaly);
            foreach (var sample in fetcherResult.Usage)
                usageTracker.Record(sample);

            System.Console.WriteLine("Fetcher findings:");
            System.Console.WriteLine(fetcherResult.QueryFindings);
            System.Console.WriteLine();

            System.Console.WriteLine("Agent C (Reporter): drafting root-cause report...");
            var reporterChat = LlmKernelFactory.CreateChatCompletionService(providerConfig, providerConfig.ReporterModel);
            var reporter = new ReporterAgent(reporterChat, providerConfig.ReporterModel);
            var reporterResult = await reporter.DraftReportAsync(anomaly, fetcherResult.QueryFindings);
            foreach (var sample in reporterResult.Usage)
                usageTracker.Record(sample);

            System.Console.WriteLine("Agent D (Judge): grading the report's groundedness...");
            var judgeChat = LlmKernelFactory.CreateChatCompletionService(providerConfig, providerConfig.JudgeModel);
            var judge = new GroundednessJudge(judgeChat, providerConfig.JudgeModel);
            var judgeResult = await judge.EvaluateAsync(reporterResult.ReportMarkdown, fetcherResult.QueryFindings);
            if (judgeResult.Usage is not null)
                usageTracker.Record(judgeResult.Usage);

            cycleActivity?.SetTag("judge.grounded", judgeResult.Grounded);

            var reportsDir = Path.Combine(FindRepoRoot(), "reports");
            Directory.CreateDirectory(reportsDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var reportPath = Path.Combine(reportsDir, $"root-cause-report-{timestamp}.md");
            var reportContent = $"""
                # AeroMind IQ — Root Cause Report

                {reporterResult.ReportMarkdown}

                ---

                {judgeResult.Summarize()}

                ---

                {usageTracker.Summarize()}
                """;
            await File.WriteAllTextAsync(reportPath, reportContent);

            System.Console.WriteLine();
            System.Console.WriteLine(judgeResult.Summarize());
            System.Console.WriteLine();
            System.Console.WriteLine(usageTracker.Summarize());
            System.Console.WriteLine($"Report written to {reportPath}");

            return 0;
        }
        finally
        {
            // This is a short-lived console app, not a hosted service: OpenTelemetry
            // batches spans in memory and flushes them on a background timer by default,
            // so without an explicit flush here, the process could exit before the last
            // batch of spans is ever sent to Langfuse.
            tracerProvider?.ForceFlush();
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null &&
               !File.Exists(Path.Combine(dir.FullName, "AeroMindIQ.slnx")) &&
               !File.Exists(Path.Combine(dir.FullName, "AeroMindIQ.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
