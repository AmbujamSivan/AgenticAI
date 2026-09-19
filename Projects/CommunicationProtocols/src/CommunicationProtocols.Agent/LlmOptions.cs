namespace CommunicationProtocols.Agent;

/// <summary>Which LLM backend the agent talks to. Selected by config/env so the
/// same agent runs against a free local model or any cloud provider.</summary>
public enum LlmProvider
{
    Ollama,
    OpenAI,
    Google,
}

/// <summary>
/// LLM configuration, read from environment/config. Defaults to a free local Ollama
/// model (matching the "pluggable provider" pattern from the RCA Engine project), so
/// the agent runs with zero cloud keys; set LLM__Provider=OpenAI (+ OPENAI_API_KEY)
/// to switch. Keys are never hardcoded or committed.
/// </summary>
public sealed class LlmOptions
{
    public LlmProvider Provider { get; init; } = LlmProvider.Ollama;

    /// <summary>Model id. Defaults per provider when not set.</summary>
    public string? Model { get; init; }

    /// <summary>OpenAI-compatible endpoint (also used for Ollama).</summary>
    public string OllamaEndpoint { get; init; } = "http://localhost:11434/v1";

    public string? OpenAiApiKey { get; init; }

    public string? GoogleApiKey { get; init; }

    public string ResolveModel() => Model ?? Provider switch
    {
        LlmProvider.Ollama => "llama3.2",
        LlmProvider.OpenAI => "gpt-4o-mini",
        LlmProvider.Google => "gemini-flash-latest",
        _ => throw new NotSupportedException($"Unknown provider {Provider}."),
    };
}
