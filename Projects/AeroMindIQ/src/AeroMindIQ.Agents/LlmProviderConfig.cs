namespace AeroMindIQ.Agents;

/// <summary>
/// One entry from appsettings.json's "Providers" dictionary, keyed by provider name
/// ("Gemini", "Claude", "OpenAI", "OpenAICompatible"). This is deliberately the same
/// shape a future "bring your own key" demo page would write into.
/// </summary>
public sealed record LlmProviderConfig(
    string Provider,
    string ApiKey,
    string? BaseUrl,
    string FetcherModel,
    string ReporterModel,
    string ReviewerModel,
    string JudgeModel);
