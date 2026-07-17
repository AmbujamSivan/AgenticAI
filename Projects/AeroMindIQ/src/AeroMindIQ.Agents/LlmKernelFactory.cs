#pragma warning disable SKEXP0070 // Gemini connector is experimental

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AeroMindIQ.Agents;

/// <summary>
/// The single place provider-branching logic lives, instead of every agent duplicating
/// its own Kernel.CreateBuilder().Add***ChatCompletion() call. Adding another
/// OpenAI-compatible provider later (Groq, Together, etc.) needs zero code changes here —
/// only a new "Providers" config entry with a BaseUrl.
/// </summary>
public static class LlmKernelFactory
{
    public static Kernel CreateKernel(LlmProviderConfig config, string modelId)
    {
        var builder = Kernel.CreateBuilder();

        switch (config.Provider)
        {
            case "Gemini":
                builder.AddGoogleAIGeminiChatCompletion(modelId: modelId, apiKey: config.ApiKey);
                break;

            case "Claude":
                builder.AddAnthropicChatCompletion(modelId: modelId, apiKey: config.ApiKey);
                break;

            case "OpenAI":
                builder.AddOpenAIChatCompletion(modelId: modelId, apiKey: config.ApiKey);
                break;

            case "OpenAICompatible":
                if (string.IsNullOrWhiteSpace(config.BaseUrl))
                    throw new InvalidOperationException("OpenAICompatible provider requires a BaseUrl in config.");
                builder.AddOpenAIChatCompletion(modelId: modelId, apiKey: config.ApiKey, endpoint: new Uri(config.BaseUrl));
                break;

            default:
                throw new InvalidOperationException($"Unknown LlmProvider: '{config.Provider}'. Expected Gemini, Claude, OpenAI, or OpenAICompatible.");
        }

        return builder.Build();
    }
}
