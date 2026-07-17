using AeroMindIQ.Data;

namespace AeroMindIQ.Tests;

public class SqlGuardTests
{
    [Fact]
    public void Validate_SelectWithoutLimit_AppendsLimit()
    {
        var result = SqlGuard.Validate("SELECT * FROM production_runs");

        Assert.EndsWith($"LIMIT {SqlGuard.MaxRows}", result);
    }

    [Fact]
    public void Validate_SelectWithLimitUnderCap_LeavesLimitUnchanged()
    {
        var result = SqlGuard.Validate("SELECT * FROM production_runs LIMIT 10");

        Assert.Contains("LIMIT 10", result);
        Assert.DoesNotContain($"LIMIT {SqlGuard.MaxRows}", result);
    }

    [Fact]
    public void Validate_SelectWithLimitOverCap_ClampsToMaxRows()
    {
        var result = SqlGuard.Validate("SELECT * FROM production_runs LIMIT 99999");

        Assert.Contains($"LIMIT {SqlGuard.MaxRows}", result);
        Assert.DoesNotContain("99999", result);
    }

    [Theory]
    [InlineData("SELECT * FROM a; SELECT * FROM b")]
    [InlineData("SELECT * FROM a WHERE x = 1; DROP TABLE a")]
    public void Validate_StatementChaining_ThrowsSqlGuardException(string sql)
    {
        Assert.Throws<SqlGuardException>(() => SqlGuard.Validate(sql));
    }

    [Theory]
    [InlineData("SELECT * FROM a -- drop everything")]
    [InlineData("SELECT * FROM a /* comment */")]
    public void Validate_SqlComments_ThrowsSqlGuardException(string sql)
    {
        Assert.Throws<SqlGuardException>(() => SqlGuard.Validate(sql));
    }

    [Theory]
    [InlineData("UPDATE production_runs SET yield_pct = 0")]
    [InlineData("DELETE FROM production_runs")]
    [InlineData("DROP TABLE production_runs")]
    [InlineData("INSERT INTO production_runs VALUES (1)")]
    public void Validate_NonSelectStatement_ThrowsSqlGuardException(string sql)
    {
        Assert.Throws<SqlGuardException>(() => SqlGuard.Validate(sql));
    }

    [Fact]
    public void Validate_EmptyStatement_ThrowsSqlGuardException()
    {
        Assert.Throws<SqlGuardException>(() => SqlGuard.Validate("   "));
    }

    [Fact]
    public void Validate_ForbiddenKeywordAsSubstringOfIdentifier_DoesNotThrow()
    {
        // "created_at" contains "CREATE" as a substring but is a legitimate column name —
        // the word-boundary regex must not false-positive on it.
        var result = SqlGuard.Validate("SELECT created_at FROM production_runs");

        Assert.Contains("created_at", result);
    }

    [Fact]
    public void Validate_ForbiddenKeywordAsWholeWord_Throws()
    {
        // A single statement (no chaining) where a forbidden keyword shows up as its own
        // token — proves the keyword check itself fires, independent of the chaining check.
        Assert.Throws<SqlGuardException>(() => SqlGuard.Validate("SELECT * FROM a WHERE EXEC = 1"));
    }
}
