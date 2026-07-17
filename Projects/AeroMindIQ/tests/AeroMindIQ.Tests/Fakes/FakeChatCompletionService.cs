using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AeroMindIQ.Tests.Fakes;

/// <summary>
/// Scripted IChatCompletionService for testing agent logic (prompt construction, response
/// parsing) without hitting a real LLM API or needing a live network. Each call to
/// GetChatMessageContentsAsync returns the next response in the queue, so multi-turn
/// exchanges (e.g. tool-calling) can be scripted by queuing multiple responses.
/// </summary>
public sealed class FakeChatCompletionService(params ChatMessageContent[] responses) : IChatCompletionService
{
    private readonly Queue<ChatMessageContent> _responses = new(responses);

    public List<ChatHistory> ReceivedHistories { get; } = [];

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        ReceivedHistories.Add(chatHistory);

        if (_responses.Count == 0)
            throw new InvalidOperationException("FakeChatCompletionService has no more scripted responses.");

        return Task.FromResult<IReadOnlyList<ChatMessageContent>>([_responses.Dequeue()]);
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var results = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        foreach (var result in results)
            yield return new StreamingChatMessageContent(result.Role, result.Content);
    }
}
