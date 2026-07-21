using System.ClientModel;
using Microsoft.SemanticKernel;
using OpenAI;

namespace RcaEngine.Llm;

/// <summary>
/// Builds a Semantic Kernel wired to the configured chat endpoint.
/// Ollama is reached through its OpenAI-compatible /v1 API, so a single connector
/// covers local quantized models, OpenAI, and Azure OpenAI.
/// </summary>
public static class KernelFactory
{
    public static (Kernel Kernel, string ModelId) Create(LlmOptions options)
    {
        var builder = Kernel.CreateBuilder();
        string modelId;

        switch (options.Provider.ToLowerInvariant())
        {
            case "ollama":
                modelId = options.Ollama.ModelId;
                var ollamaClient = new OpenAIClient(
                    new ApiKeyCredential("ollama"),   // Ollama ignores the key but the client requires one
                    new OpenAIClientOptions { Endpoint = new Uri(options.Ollama.Endpoint.TrimEnd('/') + "/v1") });
                builder.AddOpenAIChatCompletion(modelId, ollamaClient);
                break;

            case "openai":
                modelId = options.OpenAI.ModelId;
                builder.AddOpenAIChatCompletion(modelId, options.OpenAI.ApiKey);
                break;

            case "azureopenai":
                modelId = options.AzureOpenAI.DeploymentName;
                builder.AddAzureOpenAIChatCompletion(
                    options.AzureOpenAI.DeploymentName,
                    options.AzureOpenAI.Endpoint,
                    options.AzureOpenAI.ApiKey);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown LLM provider '{options.Provider}'. Expected Ollama, OpenAI, or AzureOpenAI.");
        }

        return (builder.Build(), modelId);
    }
}
