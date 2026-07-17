using AeroMindIQ.Agents;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AeroMindIQ.Tests;

public class LlmKernelFactoryTests
{
    private static LlmProviderConfig Config(string provider, string? baseUrl = null) =>
        new(provider, ApiKey: "test-key", BaseUrl: baseUrl,
            FetcherModel: "m", ReporterModel: "m", ReviewerModel: "m", JudgeModel: "m");

    [Fact]
    public void CreateChatCompletionService_Gemini_ReturnsGeminiConnector()
    {
        var service = LlmKernelFactory.CreateChatCompletionService(Config("Gemini"), "gemini-2.5-flash");

        Assert.IsType<GoogleAIGeminiChatCompletionService>(service);
    }

    [Fact]
    public void CreateChatCompletionService_Claude_ReturnsAnthropicConnector()
    {
        var service = LlmKernelFactory.CreateChatCompletionService(Config("Claude"), "claude-sonnet-5");

        Assert.IsType<AnthropicChatCompletionService>(service);
    }

    [Fact]
    public void CreateChatCompletionService_OpenAI_ReturnsOpenAIConnector()
    {
        var service = LlmKernelFactory.CreateChatCompletionService(Config("OpenAI"), "gpt-5");

        Assert.IsType<OpenAIChatCompletionService>(service);
    }

    [Fact]
    public void CreateChatCompletionService_OpenAICompatibleWithBaseUrl_ReturnsOpenAIConnector()
    {
        var service = LlmKernelFactory.CreateChatCompletionService(
            Config("OpenAICompatible", baseUrl: "https://api.deepseek.com/v1"), "deepseek-chat");

        Assert.IsType<OpenAIChatCompletionService>(service);
    }

    [Fact]
    public void CreateChatCompletionService_OpenAICompatibleWithoutBaseUrl_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LlmKernelFactory.CreateChatCompletionService(Config("OpenAICompatible"), "deepseek-chat"));
    }

    [Fact]
    public void CreateChatCompletionService_UnknownProvider_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LlmKernelFactory.CreateChatCompletionService(Config("Grok"), "some-model"));
    }

    [Fact]
    public void WrapInKernel_RegistersTheGivenChatCompletionServiceForResolution()
    {
        var service = LlmKernelFactory.CreateChatCompletionService(Config("Claude"), "claude-sonnet-5");

        var kernel = LlmKernelFactory.WrapInKernel(service);

        var resolved = kernel.GetRequiredService<IChatCompletionService>();
        Assert.Same(service, resolved);
    }
}
