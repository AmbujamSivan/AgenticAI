using System.ComponentModel;
using AeroMindIQ.Data;
using Microsoft.SemanticKernel;

namespace AeroMindIQ.Agents;

/// <summary>
/// Agent B's only tool. Defense in depth, in layers: the Reviewer agent checks each
/// proposed query for logic errors/context leaks before it runs; the connection string
/// is a Postgres role with SELECT-only grants; SqlGuard rejects anything that isn't a
/// single bounded SELECT even before it reaches that role.
/// </summary>
public sealed class DatabasePlugin(
    string readOnlyConnectionString,
    ReviewerAgent reviewer,
    AnomalyContext anomaly,
    string schemaDescription)
{
    private const int MaxReviewAttempts = 3;

    private readonly List<UsageSample> _usage = [];
    private int _rejectionCount;

    public IReadOnlyList<UsageSample> Usage => _usage;

    [KernelFunction("run_read_only_query")]
    [Description("Executes a single read-only SELECT statement against the production database and returns the results as a Markdown table. Only SELECT statements are permitted; statement chaining, comments, and any write keywords are rejected. Every query is reviewed by a critic agent before it runs.")]
    public async Task<string> RunReadOnlyQueryAsync(
        [Description("A single SELECT statement, referencing only the tables/columns described in the schema.")] string sql)
    {
        var reviewSkipped = _rejectionCount >= MaxReviewAttempts;

        if (!reviewSkipped)
        {
            var verdict = await reviewer.ReviewAsync(sql, schemaDescription, anomaly);
            if (verdict.Usage is not null)
                _usage.Add(verdict.Usage);

            if (!verdict.Approved)
            {
                _rejectionCount++;
                return $"QUERY REVIEW REJECTED: {verdict.Feedback} Please revise your SELECT statement.";
            }
        }

        try
        {
            var validatedSql = SqlGuard.Validate(sql);
            var result = await QueryExecutor.RunAsync(readOnlyConnectionString, validatedSql);
            return reviewSkipped
                ? $"[Reviewer max attempts exceeded — executing without further review]\n{result}"
                : result;
        }
        catch (SqlGuardException ex)
        {
            return $"QUERY REJECTED: {ex.Message} Please rewrite as a single read-only SELECT.";
        }
        catch (Exception ex)
        {
            return $"QUERY FAILED: {ex.Message}";
        }
    }
}
