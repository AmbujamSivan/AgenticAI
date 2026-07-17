namespace AeroMindIQ.Tests;

/// <summary>
/// Connection strings for the opt-in <c>Category=Live</c> tests. These default to the
/// throwaway local Postgres from db/docker-compose.yml (matching the credentials in
/// db/docker-compose.yml and db/seed.sql), so a bare `docker compose up` reproduces the
/// live tests with zero config. Override via the AEROMINDIQ_TEST_DB_ADMIN /
/// AEROMINDIQ_TEST_DB_READER env vars to point CI or another environment elsewhere —
/// and so a real credential is never hardcoded here.
/// </summary>
internal static class LiveTestDb
{
    public static readonly string AdminConnectionString =
        Environment.GetEnvironmentVariable("AEROMINDIQ_TEST_DB_ADMIN")
        ?? "Host=localhost;Port=5432;Database=aeromindiq;Username=aeromind_admin;Password=aeromind_admin_pw";

    public static readonly string ReadOnlyConnectionString =
        Environment.GetEnvironmentVariable("AEROMINDIQ_TEST_DB_READER")
        ?? "Host=localhost;Port=5432;Database=aeromindiq;Username=aeromind_reader;Password=aeromind_reader_pw";
}
