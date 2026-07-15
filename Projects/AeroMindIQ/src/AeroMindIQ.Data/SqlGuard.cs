using System.Text.RegularExpressions;

namespace AeroMindIQ.Data;

public sealed class SqlGuardException(string message) : Exception(message);

/// <summary>
/// Defense-in-depth guard for LLM-generated SQL: rejects anything that isn't a single
/// read-only SELECT and caps the row count, on top of the DB-level read-only role.
/// </summary>
public static class SqlGuard
{
    public const int MaxRows = 200;
    public const int StatementTimeoutMs = 5000;

    private static readonly string[] ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "TRUNCATE",
        "GRANT", "REVOKE", "CREATE", "EXEC", "EXECUTE", "CALL", "COPY", "MERGE"
    ];

    public static string Validate(string sql)
    {
        var trimmed = sql.Trim().TrimEnd(';').Trim();

        if (trimmed.Length == 0)
            throw new SqlGuardException("Empty SQL statement.");

        if (trimmed.Contains(';'))
            throw new SqlGuardException("Statement chaining is not allowed — only a single SELECT statement is permitted.");

        if (trimmed.Contains("--") || trimmed.Contains("/*"))
            throw new SqlGuardException("SQL comments are not allowed.");

        if (!Regex.IsMatch(trimmed, @"^\s*SELECT\b", RegexOptions.IgnoreCase))
            throw new SqlGuardException("Only SELECT statements are allowed.");

        foreach (var keyword in ForbiddenKeywords)
        {
            if (Regex.IsMatch(trimmed, $@"\b{keyword}\b", RegexOptions.IgnoreCase))
                throw new SqlGuardException($"Forbidden keyword detected: {keyword}");
        }

        return EnforceRowLimit(trimmed);
    }

    private static string EnforceRowLimit(string sql)
    {
        var limitMatch = Regex.Match(sql, @"\bLIMIT\s+(\d+)\b", RegexOptions.IgnoreCase);
        if (!limitMatch.Success)
            return $"{sql} LIMIT {MaxRows}";

        var requested = int.Parse(limitMatch.Groups[1].Value);
        return requested > MaxRows
            ? Regex.Replace(sql, @"\bLIMIT\s+\d+\b", $"LIMIT {MaxRows}", RegexOptions.IgnoreCase)
            : sql;
    }
}
