namespace RcaEngine.Llm;

public sealed class LlmOptions
{
    public string Provider { get; set; } = "Ollama";   // Ollama | OpenAI | AzureOpenAI
    public OllamaOptions Ollama { get; set; } = new();
    public OpenAIOptions OpenAI { get; set; } = new();
    public AzureOpenAIOptions AzureOpenAI { get; set; } = new();
}

public sealed class OllamaOptions
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string ModelId { get; set; } = "llama3.2";
}

public sealed class OpenAIOptions
{
    public string ModelId { get; set; } = "gpt-4o-mini";
    public string ApiKey { get; set; } = "";
}

public sealed class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = "";
    public string DeploymentName { get; set; } = "";
    public string ApiKey { get; set; } = "";
}
