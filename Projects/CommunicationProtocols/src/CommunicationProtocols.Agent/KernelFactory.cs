using System.ClientModel;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using OpenAI;

namespace CommunicationProtocols.Agent;

/// <summary>
/// Builds a Semantic Kernel wired to the configured provider. OpenAI and Ollama go
/// through the OpenAI connector (Ollama via its OpenAI-compatible endpoint); Google via
/// the Gemini connector. Adding Anthropic later is a new case here plus its connector.
/// </summary>
public static class KernelFactory
{
    // Shared HTTP client for CLOUD providers that applies a fail-open revocation policy:
    // the certificate chain and hostname are still fully validated, but a connection is
    // NOT aborted when revocation status can't be determined. This matches curl/browser
    // behavior and is required on macOS since CAs (e.g. Let's Encrypt) retired OCSP in
    // 2025 — .NET's stricter default otherwise fails with RevocationStatusUnknown.
    public static readonly HttpClient CloudHttpClient = new(new SocketsHttpHandler
    {
        SslOptions = new SslClientAuthenticationOptions
        {
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        },
    });

    public static Kernel Create(LlmOptions options)
    {
        var builder = Kernel.CreateBuilder();
        var model = options.ResolveModel();

        switch (options.Provider)
        {
            case LlmProvider.OpenAI:
                var apiKey = options.OpenAiApiKey
                    ?? throw new InvalidOperationException(
                        "OPENAI_API_KEY is not set. Export it, or use the default Ollama provider.");
                builder.AddOpenAIChatCompletion(model, apiKey, httpClient: CloudHttpClient);
                break;

            case LlmProvider.Ollama:
                // Point the OpenAI client at Ollama's local OpenAI-compatible endpoint.
                // Local/loopback — no TLS, so the cloud revocation policy doesn't apply.
                var ollamaClient = new OpenAIClient(
                    new ApiKeyCredential("ollama"), // Ollama ignores the key
                    new OpenAIClientOptions { Endpoint = new Uri(options.OllamaEndpoint) });
                builder.AddOpenAIChatCompletion(model, ollamaClient);
                break;

            case LlmProvider.Google:
                var googleKey = options.GoogleApiKey
                    ?? throw new InvalidOperationException(
                        "GOOGLE_API_KEY is not set. Export it, or use the default Ollama provider.");
                builder.AddGoogleAIGeminiChatCompletion(model, googleKey, httpClient: CloudHttpClient);
                break;

            default:
                throw new NotSupportedException($"Unknown provider {options.Provider}.");
        }

        return builder.Build();
    }
}
