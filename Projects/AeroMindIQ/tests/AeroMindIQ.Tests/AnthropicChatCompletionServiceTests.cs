#pragma warning disable SKEXP0001

using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using AeroMindIQ.Agents;
using AeroMindIQ.Tests.Fakes;
using Anthropic.Exceptions;
using AnthropicMessages = Anthropic.Models.Messages;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AeroMindIQ.Tests;

/// <summary>
/// Regression coverage for the exact bug found during Phase 3 live verification: a first
/// version of this connector made a single call to Claude and returned, so a tool-use
/// response was never followed up on — the kernel function was never invoked and Claude
/// was never asked to continue. These tests exercise the isolated repro that caught it
/// (a mocked tool-calling exchange) without touching a real API.
/// </summary>
public class AnthropicChatCompletionServiceTests
{
    [Fact]
    public async Task SimpleTextResponse_ReturnsTextWithoutLoopingAsync()
    {
        var fakeMessages = new FakeMessageService(_ => TextResponse("Hello there.", inputTokens: 10, outputTokens: 5));
        var service = new AnthropicChatCompletionService("claude-sonnet-5", fakeMessages);

        var history = new ChatHistory();
        history.AddUserMessage("Say hello.");

        var results = await service.GetChatMessageContentsAsync(history);

        Assert.Single(results);
        Assert.Equal("Hello there.", results[0].Content);
        Assert.Single(fakeMessages.ReceivedRequests);
    }

    [Fact]
    public async Task ToolUseResponse_InvokesKernelFunctionAndContinuesAsync()
    {
        var invoked = false;

        var fakeMessages = new FakeMessageService(
            _ => ToolUseResponse("Test__get_number", "call-1", inputTokens: 20, outputTokens: 10),
            _ => TextResponse("The number is 42.", inputTokens: 15, outputTokens: 8));

        var service = new AnthropicChatCompletionService("claude-sonnet-5", fakeMessages);

        var builder = Kernel.CreateBuilder();
        var kernel = builder.Build();
        kernel.Plugins.AddFromObject(new TestPlugin(() => invoked = true), "Test");

        var history = new ChatHistory();
        history.AddUserMessage("Call the tool and tell me the number.");

        var results = await service.GetChatMessageContentsAsync(history, kernel: kernel);

        Assert.True(invoked, "The kernel function should have been invoked after the tool-use turn.");
        Assert.Equal(2, fakeMessages.ReceivedRequests.Count);
        Assert.Single(results);
        Assert.Equal("The number is 42.", results[0].Content);
    }

    [Fact]
    public async Task ToolUseResponse_AccumulatesUsageAcrossIterationsAsync()
    {
        var fakeMessages = new FakeMessageService(
            _ => ToolUseResponse("Test__get_number", "call-1", inputTokens: 20, outputTokens: 10),
            _ => TextResponse("Done.", inputTokens: 15, outputTokens: 8));

        var service = new AnthropicChatCompletionService("claude-sonnet-5", fakeMessages);

        var builder = Kernel.CreateBuilder();
        var kernel = builder.Build();
        kernel.Plugins.AddFromObject(new TestPlugin(() => { }), "Test");

        var history = new ChatHistory();
        history.AddUserMessage("Call the tool.");

        var results = await service.GetChatMessageContentsAsync(history, kernel: kernel);

        var metadata = results[0].Metadata!;
        Assert.Equal(35, metadata["PromptTokenCount"]); // 20 + 15
        Assert.Equal(18, metadata["CandidatesTokenCount"]); // 10 + 8
    }

    [Fact]
    public async Task RateLimitException_RethrowsAsHttpOperationExceptionWithTooManyRequestsAsync()
    {
        var fakeMessages = new FakeMessageService(
            _ => throw new AnthropicRateLimitException(new HttpRequestException("rate limited"))
            {
                StatusCode = HttpStatusCode.TooManyRequests,
                ResponseBody = null!
            });

        var service = new AnthropicChatCompletionService("claude-sonnet-5", fakeMessages);

        var history = new ChatHistory();
        history.AddUserMessage("Hi");

        var ex = await Assert.ThrowsAsync<HttpOperationException>(() => service.GetChatMessageContentsAsync(history));
        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Fact]
    public async Task SystemMessage_IsExtractedIntoSystemPromptNotMessagesAsync()
    {
        var fakeMessages = new FakeMessageService(_ => TextResponse("ok", inputTokens: 1, outputTokens: 1));
        var service = new AnthropicChatCompletionService("claude-sonnet-5", fakeMessages);

        var history = new ChatHistory();
        history.AddSystemMessage("You are a helpful assistant.");
        history.AddUserMessage("Hi");

        await service.GetChatMessageContentsAsync(history);

        var request = fakeMessages.ReceivedRequests.Single();
        Assert.Single(request.Messages); // only the user message, system pulled out separately
        Assert.NotNull(request.System);
    }

    private static AnthropicMessages.Message TextResponse(string text, int inputTokens, int outputTokens) =>
        NewMessage([new AnthropicMessages.TextBlock { Text = text, Citations = [] }], inputTokens, outputTokens);

    private static AnthropicMessages.Message ToolUseResponse(string toolName, string toolUseId, int inputTokens, int outputTokens) =>
        NewMessage(
            [
                new AnthropicMessages.ToolUseBlock
                {
                    ID = toolUseId,
                    Name = toolName,
                    Input = new Dictionary<string, JsonElement>(),
                    Caller = null!
                }
            ],
            inputTokens,
            outputTokens);

    private static AnthropicMessages.Message NewMessage(List<AnthropicMessages.ContentBlock> content, int inputTokens, int outputTokens) =>
        new()
        {
            ID = "test-message-id",
            Container = null!,
            Content = content,
            Model = "claude-sonnet-5",
            StopDetails = null!,
            StopReason = "end_turn",
            StopSequence = null!,
            Usage = new AnthropicMessages.Usage
            {
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CacheCreation = null!,
                CacheCreationInputTokens = null,
                CacheReadInputTokens = null,
                InferenceGeo = null!,
                OutputTokensDetails = null!,
                ServerToolUse = null!,
                ServiceTier = null!
            }
        };

    private sealed class TestPlugin(Action onInvoked)
    {
        [KernelFunction("get_number")]
        [Description("Returns a number.")]
        public int GetNumber()
        {
            onInvoked();
            return 42;
        }
    }
}
