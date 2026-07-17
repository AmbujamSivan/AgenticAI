using System.Net;
using Microsoft.SemanticKernel;

namespace AeroMindIQ.Agents;

/// <summary>
/// Retries an LLM agent call on HTTP 429, regardless of which provider is active — every
/// connector (Gemini's official one, the custom Anthropic one) normalizes rate-limit
/// failures onto SK's own HttpOperationException, so this needs no provider-specific
/// branching. Originally written against Gemini's free tier (5 req/min on
/// gemini-2.5-flash), where the Reviewer and Fetcher could both be mid-backoff at once,
/// compounding how long the shared per-minute quota stayed exhausted — 4 attempts topping
/// out at 60s wasn't always enough for the window to clear, hence the 6-attempt/90s cap.
/// Each retry rebuilds the whole call (fresh kernel/agent/thread) rather than resuming
/// mid-stream, since Semantic Kernel's agent invocation doesn't expose a way to resume a
/// partially-consumed IAsyncEnumerable after a failure inside it.
/// </summary>
public static class LlmRetryPolicy
{
    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(45),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(75),
        TimeSpan.FromSeconds(90),
    ];

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string callerLabel)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (HttpOperationException ex) when (
                ex.StatusCode == HttpStatusCode.TooManyRequests && attempt < BackoffSchedule.Length)
            {
                var delay = BackoffSchedule[attempt];
                System.Console.Error.WriteLine(
                    $"[{callerLabel}] Rate limit hit (429) — waiting {delay.TotalSeconds:F0}s before retry {attempt + 1}/{BackoffSchedule.Length}.");
                await Task.Delay(delay);
            }
        }
    }
}
