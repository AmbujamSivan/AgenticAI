#pragma warning disable SKEXP0070 // Gemini connector is experimental
#pragma warning disable SKEXP0010 // custom OpenAI-compatible endpoint constructor is experimental

using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AeroMindIQ.Agents;

/// <summary>
/// The single place provider-branching logic lives, instead of every agent duplicating
/// its own Add***ChatCompletion() call. Adding another OpenAI-compatible provider later
/// (Groq, Together, etc.) needs zero code changes here — only a new "Providers" config
/// entry with a BaseUrl.
///
/// Returns a plain IChatCompletionService rather than a built Kernel, deliberately: each
/// agent takes that service via constructor injection instead of a LlmProviderConfig, so
/// tests can substitute a fake chat completion service without touching provider-selection
/// logic or hitting a real API.
/// </summary>
public static class LlmKernelFactory
{
    public static IChatCompletionService CreateChatCompletionService(LlmProviderConfig config, string modelId) =>
        config.Provider switch
        {
            "Gemini" => new GoogleAIGeminiChatCompletionService(modelId: modelId, apiKey: config.ApiKey),
            "Claude" => new AnthropicChatCompletionService(modelId, config.ApiKey),
            "OpenAI" => new OpenAIChatCompletionService(modelId: modelId, apiKey: config.ApiKey),
            "OpenAICompatible" => CreateOpenAICompatible(config, modelId),
            _ => throw new InvalidOperationException($"Unknown LlmProvider: '{config.Provider}'. Expected Gemini, Claude, OpenAI, or OpenAICompatible.")
        };

    /// <summary>Wraps an already-constructed chat completion service in a bare Kernel so it can be assigned to a ChatCompletionAgent.</summary>
    public static Kernel WrapInKernel(IChatCompletionService chatCompletionService)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatCompletionService);
        return builder.Build();
    }

    private static IChatCompletionService CreateOpenAICompatible(LlmProviderConfig config, string modelId)
    {
        if (string.IsNullOrWhiteSpace(config.BaseUrl))
            throw new InvalidOperationException("OpenAICompatible provider requires a BaseUrl in config.");

        return new OpenAIChatCompletionService(modelId, new Uri(config.BaseUrl), config.ApiKey);
    }
}
