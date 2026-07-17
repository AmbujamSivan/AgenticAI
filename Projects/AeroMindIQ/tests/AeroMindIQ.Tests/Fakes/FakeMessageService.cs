using Anthropic.Core;
using Anthropic.Models.Messages;
using Anthropic.Services;
using Anthropic.Services.Messages;

namespace AeroMindIQ.Tests.Fakes;

/// <summary>
/// Scripted Anthropic IMessageService for testing AnthropicChatCompletionService's
/// translation and tool-calling-loop logic without a live network call. Implements only
/// Create (the one method the connector actually calls) — the rest of the interface
/// throws, since exercising them isn't needed for these tests.
/// </summary>
public sealed class FakeMessageService(params Func<MessageCreateParams, Message>[] responses) : IMessageService
{
    private readonly Queue<Func<MessageCreateParams, Message>> _responses = new(responses);

    public List<MessageCreateParams> ReceivedRequests { get; } = [];

    public IMessageServiceWithRawResponse WithRawResponse => throw new NotImplementedException();

    public IBatchService Batches => throw new NotImplementedException();

    public IMessageService WithOptions(Func<ClientOptions, ClientOptions> modifier) =>
        throw new NotImplementedException();

    public Task<Message> Create(MessageCreateParams parameters, CancellationToken cancellationToken = default)
    {
        ReceivedRequests.Add(parameters);

        if (_responses.Count == 0)
            throw new InvalidOperationException("FakeMessageService has no more scripted responses.");

        return Task.FromResult(_responses.Dequeue()(parameters));
    }

    public IAsyncEnumerable<RawMessageStreamEvent> CreateStreaming(MessageCreateParams parameters, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<MessageTokensCount> CountTokens(MessageCountTokensParams parameters, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
