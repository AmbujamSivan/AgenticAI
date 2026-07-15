using AeroMindIQ.Agents;
using AeroMindIQ.Data;
using Microsoft.Extensions.Configuration;

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

        var adminConnectionString = config.GetConnectionString("AdminConnection")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:AdminConnection in configuration.");
        var readOnlyConnectionString = config.GetConnectionString("ReadOnlyConnection")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ReadOnlyConnection in configuration.");

        var geminiApiKey = config["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(geminiApiKey))
        {
            System.Console.Error.WriteLine(
                "Missing Gemini:ApiKey. Set it in src/AeroMindIQ.Console/appsettings.Development.json " +
                "(gitignored) or the GEMINI__APIKEY environment variable.");
            return 1;
        }

        var fetcherModel = config["Gemini:FetcherModel"] ?? "gemini-2.5-flash";
        var reporterModel = config["Gemini:ReporterModel"] ?? "gemini-2.5-flash";

        System.Console.WriteLine("AeroMind IQ — running one detection cycle...");
        System.Console.WriteLine();

        System.Console.WriteLine("Agent A (Auditor): checking production_runs for anomalies...");
        var anomalies = await AuditorAgent.CheckForAnomaliesAsync(adminConnectionString);

        if (anomalies.Count == 0)
        {
            System.Console.WriteLine("No anomalies detected (nothing exceeds the 3-sigma threshold). Nothing to investigate.");
            return 0;
        }

        var anomaly = anomalies[0];
        System.Console.WriteLine($"Anomaly triggered: {anomaly.Describe()}");
        System.Console.WriteLine();

        var usageTracker = new UsageTracker();

        System.Console.WriteLine("Agent B (Fetcher): gathering supporting evidence via safe SQL...");
        var schemaDescription = await SchemaReader.DescribeSchemaAsync(adminConnectionString);
        var fetcher = new FetcherAgent(geminiApiKey, fetcherModel, readOnlyConnectionString, schemaDescription);
        var fetcherResult = await fetcher.InvestigateAsync(anomaly);
        foreach (var sample in fetcherResult.Usage)
            usageTracker.Record(sample);

        System.Console.WriteLine("Fetcher findings:");
        System.Console.WriteLine(fetcherResult.QueryFindings);
        System.Console.WriteLine();

        System.Console.WriteLine("Agent C (Reporter): drafting root-cause report...");
        var reporter = new ReporterAgent(geminiApiKey, reporterModel);
        var reporterResult = await reporter.DraftReportAsync(anomaly, fetcherResult.QueryFindings);
        foreach (var sample in reporterResult.Usage)
            usageTracker.Record(sample);

        var reportsDir = Path.Combine(FindRepoRoot(), "reports");
        Directory.CreateDirectory(reportsDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var reportPath = Path.Combine(reportsDir, $"root-cause-report-{timestamp}.md");
        var reportContent = $"""
            # AeroMind IQ — Root Cause Report

            {reporterResult.ReportMarkdown}

            ---

            {usageTracker.Summarize()}
            """;
        await File.WriteAllTextAsync(reportPath, reportContent);

        System.Console.WriteLine();
        System.Console.WriteLine(usageTracker.Summarize());
        System.Console.WriteLine($"Report written to {reportPath}");

        return 0;
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
