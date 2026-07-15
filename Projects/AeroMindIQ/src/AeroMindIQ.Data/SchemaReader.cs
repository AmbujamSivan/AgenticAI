using System.Text;
using Npgsql;

namespace AeroMindIQ.Data;

/// <summary>
/// Reads live Postgres schema metadata so agent prompts stay grounded in the real
/// table/column shape instead of a hardcoded description that can drift.
/// </summary>
public static class SchemaReader
{
    public static async Task<string> DescribeSchemaAsync(string connectionString, string schema = "public")
    {
        const string query = """
            SELECT table_name, column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_schema = @schema
            ORDER BY table_name, ordinal_position;
            """;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("schema", schema);

        var byTable = new Dictionary<string, List<string>>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            var column = reader.GetString(1);
            var dataType = reader.GetString(2);
            var nullable = reader.GetString(3) == "YES" ? "NULL" : "NOT NULL";

            if (!byTable.TryGetValue(table, out var columns))
            {
                columns = [];
                byTable[table] = columns;
            }

            columns.Add($"{column} {dataType} {nullable}");
        }

        var sb = new StringBuilder();
        foreach (var (table, columns) in byTable)
        {
            sb.AppendLine($"### {table}");
            foreach (var column in columns)
                sb.AppendLine($"- {column}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
