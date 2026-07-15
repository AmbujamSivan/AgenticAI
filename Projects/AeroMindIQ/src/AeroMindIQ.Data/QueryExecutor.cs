using System.Text;
using Npgsql;

namespace AeroMindIQ.Data;

/// <summary>
/// Executes a pre-validated (SqlGuard) read-only query against Postgres and renders
/// the result as a Markdown table, capped to keep LLM context usage predictable.
/// </summary>
public static class QueryExecutor
{
    public static async Task<string> RunAsync(string connectionString, string validatedSql, int statementTimeoutMs = SqlGuard.StatementTimeoutMs)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using (var timeoutCmd = new NpgsqlCommand($"SET statement_timeout = {statementTimeoutMs};", conn))
        {
            await timeoutCmd.ExecuteNonQueryAsync();
        }

        await using var cmd = new NpgsqlCommand(validatedSql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var columnNames = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();

        var sb = new StringBuilder();
        sb.AppendLine("| " + string.Join(" | ", columnNames) + " |");
        sb.AppendLine("| " + string.Join(" | ", columnNames.Select(_ => "---")) + " |");

        var rowCount = 0;
        while (await reader.ReadAsync())
        {
            var values = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString() ?? "");
            sb.AppendLine("| " + string.Join(" | ", values) + " |");
            rowCount++;
        }

        return rowCount == 0 ? "_(query returned no rows)_" : sb.ToString();
    }
}
