using System.ComponentModel;
using AeroMindIQ.Data;
using Microsoft.SemanticKernel;

namespace AeroMindIQ.Agents;

/// <summary>
/// Agent B's only tool. Defense in depth: the connection string is a Postgres role with
/// SELECT-only grants, and SqlGuard rejects anything that isn't a single bounded SELECT
/// even before it reaches that role.
/// </summary>
public sealed class DatabasePlugin(string readOnlyConnectionString)
{
    [KernelFunction("run_read_only_query")]
    [Description("Executes a single read-only SELECT statement against the production database and returns the results as a Markdown table. Only SELECT statements are permitted; statement chaining, comments, and any write keywords are rejected.")]
    public async Task<string> RunReadOnlyQueryAsync(
        [Description("A single SELECT statement, referencing only the tables/columns described in the schema.")] string sql)
    {
        try
        {
            var validatedSql = SqlGuard.Validate(sql);
            return await QueryExecutor.RunAsync(readOnlyConnectionString, validatedSql);
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
