#pragma warning disable SKEXP0001 // Agent framework abstractions are experimental

using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Services;
using AnthropicMessages = Anthropic.Models.Messages;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AeroMindIQ.Agents;

/// <summary>
/// Wraps Anthropic's official .NET SDK behind Semantic Kernel's IChatCompletionService.
///
/// Important lesson learned while building this: SK's ChatCompletionAgent does NOT drive
/// a tool-calling loop itself. Each connector (Gemini's, OpenAI's) implements the "call
/// model -> if tool calls, invoke them -> call model again -> repeat until a final text
/// response" loop internally, inside its own GetChatMessageContentsAsync. A version of
/// this connector that made a single call and returned was confirmed (via a minimal
/// isolated repro) to only ever yield the tool-call turn itself, with SK's agent layer
/// never invoking the tool or asking Claude to continue — so the loop below is required,
/// not optional.
/// </summary>
public sealed class AnthropicChatCompletionService : IChatCompletionService
{
    private const int MaxTokens = 4096;
    private const int MaxAutoInvokeIterations = 10;
    private const string ToolNameSeparator = "__";

    private readonly IMessageService _messages;
    private readonly string _modelId;

    public AnthropicChatCompletionService(string modelId, string apiKey)
        : this(modelId, new AnthropicClient { ApiKey = apiKey }.Messages)
    {
    }

    /// <summary>
    /// Depends on IMessageService (the single method surface this connector actually
    /// calls) rather than the whole AnthropicClient, so tests can substitute a fake
    /// without needing to implement AnthropicClient's much larger member surface.
    /// </summary>
    public AnthropicChatCompletionService(string modelId, IMessageService messageService)
    {
        _modelId = modelId;
        _messages = messageService;
    }

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var workingHistory = new ChatHistory(chatHistory);
        var tools = kernel is not null ? BuildTools(kernel) : [];
        var totalPromptTokens = 0;
        var totalCompletionTokens = 0;

        for (var iteration = 0; iteration < MaxAutoInvokeIterations; iteration++)
        {
            var (systemPrompt, messages) = TranslateHistory(workingHistory);

            var createParams = new AnthropicMessages.MessageCreateParams
            {
                Model = _modelId,
                MaxTokens = MaxTokens,
                Messages = messages,
                System = string.IsNullOrEmpty(systemPrompt) ? null : systemPrompt,
                Tools = tools.Count > 0 ? tools : null
            };

            AnthropicMessages.Message response;
            try
            {
                response = await _messages.Create(createParams, cancellationToken);
            }
            catch (AnthropicRateLimitException ex)
            {
                // Normalizes both connectors onto the same exception shape so LlmRetryPolicy
                // needs no provider-specific branching.
                throw new HttpOperationException(HttpStatusCode.TooManyRequests, ex.Message, ex.Message, ex);
            }

            totalPromptTokens += (int)response.Usage.InputTokens;
            totalCompletionTokens += (int)response.Usage.OutputTokens;

            var resultMessage = TranslateResponse(response);
            var functionCalls = resultMessage.Items.OfType<FunctionCallContent>().ToList();

            if (functionCalls.Count == 0 || kernel is null)
            {
                // Final turn: report cumulative usage across every iteration this call
                // took, not just the last one, so cost tracking doesn't undercount
                // multi-query investigations.
                resultMessage.Metadata = new Dictionary<string, object?>
                {
                    ["PromptTokenCount"] = totalPromptTokens,
                    ["CandidatesTokenCount"] = totalCompletionTokens
                };
                return [resultMessage];
            }

            workingHistory.Add(resultMessage);
            foreach (var call in functionCalls)
            {
                var functionResult = await call.InvokeAsync(kernel, cancellationToken);
                workingHistory.Add(functionResult.ToChatMessage());
            }
        }

        throw new InvalidOperationException(
            $"Anthropic connector exceeded {MaxAutoInvokeIterations} tool-calling iterations without a final response.");
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Nothing in AeroMind IQ streams responses today; this yields the whole reply as
        // one chunk rather than implementing true SSE streaming for an unused code path.
        var results = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        foreach (var result in results)
            yield return new StreamingChatMessageContent(result.Role, result.Content) { ModelId = _modelId };
    }

    private static (string? SystemPrompt, List<AnthropicMessages.MessageParam> Messages) TranslateHistory(ChatHistory chatHistory)
    {
        string? systemPrompt = null;
        var messages = new List<AnthropicMessages.MessageParam>();

        foreach (var entry in chatHistory)
        {
            if (entry.Role == AuthorRole.System)
            {
                systemPrompt = systemPrompt is null ? entry.Content : $"{systemPrompt}\n{entry.Content}";
                continue;
            }

            var toolResults = entry.Items.OfType<FunctionResultContent>().ToList();
            if (toolResults.Count > 0)
            {
                var blocks = toolResults
                    .Select(fr => (AnthropicMessages.ContentBlockParam)new AnthropicMessages.ToolResultBlockParam
                    {
                        ToolUseID = fr.CallId,
                        Content = fr.Result?.ToString() ?? string.Empty
                    })
                    .ToList();
                messages.Add(new AnthropicMessages.MessageParam { Role = AnthropicMessages.Role.User, Content = blocks });
                continue;
            }

            var toolCalls = entry.Items.OfType<FunctionCallContent>().ToList();
            if (toolCalls.Count > 0)
            {
                var blocks = toolCalls
                    .Select(fc => (AnthropicMessages.ContentBlockParam)new AnthropicMessages.ToolUseBlockParam
                    {
                        ID = fc.Id,
                        Name = BuildToolName(fc.PluginName, fc.FunctionName),
                        Input = ConvertArguments(fc.Arguments)
                    })
                    .ToList();
                messages.Add(new AnthropicMessages.MessageParam { Role = AnthropicMessages.Role.Assistant, Content = blocks });
                continue;
            }

            messages.Add(new AnthropicMessages.MessageParam
            {
                Role = entry.Role == AuthorRole.Assistant ? AnthropicMessages.Role.Assistant : AnthropicMessages.Role.User,
                Content = entry.Content ?? string.Empty
            });
        }

        return (systemPrompt, messages);
    }

    private ChatMessageContent TranslateResponse(AnthropicMessages.Message response)
    {
        var items = new ChatMessageContentItemCollection();
        var textParts = new List<string>();

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock))
            {
                textParts.Add(textBlock.Text);
                items.Add(new TextContent(textBlock.Text));
            }
            else if (block.TryPickToolUse(out var toolUseBlock))
            {
                var (pluginName, functionName) = ParseToolName(toolUseBlock.Name);
                items.Add(new FunctionCallContent(functionName, pluginName, toolUseBlock.ID, ConvertInputToArguments(toolUseBlock.Input)));
            }
        }

        var message = new ChatMessageContent(AuthorRole.Assistant, string.Join("\n", textParts))
        {
            Items = items,
            ModelId = _modelId,
            Metadata = new Dictionary<string, object?>
            {
                // Matches the key names UsageExtractor (UsageTracker.cs) already reads
                // from Gemini responses, so no provider-specific branching is needed there.
                ["PromptTokenCount"] = (int)response.Usage.InputTokens,
                ["CandidatesTokenCount"] = (int)response.Usage.OutputTokens
            }
        };

        return message;
    }

    private static List<AnthropicMessages.ToolUnion> BuildTools(Kernel kernel)
    {
        var tools = new List<AnthropicMessages.ToolUnion>();

        foreach (var function in kernel.Plugins.GetFunctionsMetadata())
        {
            var properties = new Dictionary<string, JsonElement>();
            var required = new List<string>();

            foreach (var parameter in function.Parameters)
            {
                properties[parameter.Name] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = parameter.Description
                });

                if (parameter.IsRequired)
                    required.Add(parameter.Name);
            }

            var schema = new AnthropicMessages.InputSchema
            {
                Type = JsonSerializer.SerializeToElement("object"),
                Properties = properties,
                Required = required
            };

            tools.Add(new AnthropicMessages.Tool
            {
                Name = BuildToolName(function.PluginName, function.Name),
                Description = function.Description,
                InputSchema = schema
            });
        }

        return tools;
    }

    private static IReadOnlyDictionary<string, JsonElement> ConvertArguments(KernelArguments? arguments)
    {
        var result = new Dictionary<string, JsonElement>();
        if (arguments is null)
            return result;

        foreach (var pair in arguments)
            result[pair.Key] = JsonSerializer.SerializeToElement(pair.Value);

        return result;
    }

    private static KernelArguments ConvertInputToArguments(IReadOnlyDictionary<string, JsonElement> input)
    {
        var arguments = new KernelArguments();
        foreach (var pair in input)
        {
            arguments[pair.Key] = pair.Value.ValueKind == JsonValueKind.String
                ? pair.Value.GetString()
                : pair.Value.ToString();
        }

        return arguments;
    }

    private static string BuildToolName(string? pluginName, string functionName) =>
        string.IsNullOrEmpty(pluginName) ? functionName : $"{pluginName}{ToolNameSeparator}{functionName}";

    private static (string PluginName, string FunctionName) ParseToolName(string toolName)
    {
        var parts = toolName.Split(ToolNameSeparator, 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (string.Empty, toolName);
    }
}
