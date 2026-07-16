using System.Net;
using Microsoft.SemanticKernel;

namespace AeroMindIQ.Agents;

/// <summary>
/// Retries a Gemini agent call on HTTP 429. The free tier's per-minute quota (5
/// requests/minute on gemini-2.5-flash at time of writing) is easy to exceed once
/// multiple agents each make their own calls within one investigation cycle — this
/// makes a cycle actually complete instead of crashing mid-run, at the cost of the wait.
/// Each retry rebuilds the whole call (fresh kernel/agent/thread) rather than resuming
/// mid-stream, since Semantic Kernel's agent invocation doesn't expose a way to resume
/// a partially-consumed IAsyncEnumerable after a failure inside it.
/// </summary>
public static class GeminiRetryPolicy
{
    // Verification under the free tier (5 req/min on gemini-2.5-flash) showed the
    // Reviewer and Fetcher can both be mid-backoff at once, compounding how long the
    // shared per-minute quota stays exhausted — 4 attempts topping out at 60s wasn't
    // always enough for the window to clear. Extended to 6 attempts / 90s cap.
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
                    $"[{callerLabel}] Gemini rate limit hit (429) — waiting {delay.TotalSeconds:F0}s before retry {attempt + 1}/{BackoffSchedule.Length}.");
                await Task.Delay(delay);
            }
        }
    }
}
