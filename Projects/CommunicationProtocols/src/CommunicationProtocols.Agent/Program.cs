using CommunicationProtocols.Agent;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;

// Configuration: env vars (LLM__Provider, LLM__Model, *_API_KEY) + optional args.
var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var options = new LlmOptions
{
    Provider = Enum.TryParse<LlmProvider>(config["LLM:Provider"], ignoreCase: true, out var p)
        ? p
        : LlmProvider.Ollama,
    Model = config["LLM:Model"],
    OpenAiApiKey = config["OPENAI_API_KEY"],
    GoogleApiKey = config["GOOGLE_API_KEY"],
};

var prompt = config["prompt"]
    ?? "Which products would suit a home office, and what's the cheapest one in stock? Use the tools.";

// Whether to also attach the external community Fetch MCP (multi-MCP orchestration).
var useFetch = bool.TryParse(config["fetch"], out var f) && f;

Console.Error.WriteLine($"[agent] provider={options.Provider} model={options.ResolveModel()} fetch={useFetch}");

var kernel = KernelFactory.Create(options);

// Tool source 1 — OUR MCP server (product catalog + RAG semantic_search).
var mcpDll = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory,
    "../../../../CommunicationProtocols.Mcp/bin/Debug/net9.0/CommunicationProtocols.Mcp.dll"));

await using var productsMcp = await McpToolSource.AddStdioMcpToolsAsync(
    kernel, "products", "dotnet", [mcpDll]);
Console.Error.WriteLine("[agent] imported 'products' MCP tools.");

// Tool source 2 (optional) — an EXTERNAL community MCP: the Python Fetch server,
// launched via uvx. This proves the agent can plan across multiple MCP servers.
IAsyncDisposable? fetchMcp = null;
if (useFetch)
{
    fetchMcp = await McpToolSource.AddStdioMcpToolsAsync(
        kernel, "fetch", "uvx", ["mcp-server-fetch"]);
    Console.Error.WriteLine("[agent] imported 'fetch' MCP tools (external community server).");
}

Console.Error.WriteLine("[agent] asking the model...\n");

if (options.Provider == LlmProvider.Google)
{
    // Google path: call Gemini's REST API directly (the alpha SK connector's tool
    // schema is rejected by the current API). Reuses the same MCP tools in the kernel.
    var googleKey = options.GoogleApiKey
        ?? throw new InvalidOperationException("GOOGLE_API_KEY is not set.");
    var gemini = new GeminiRestAgent(KernelFactory.CloudHttpClient, googleKey, options.ResolveModel());
    Console.WriteLine(await gemini.RunAsync(kernel, prompt));
}
else
{
    // OpenAI / Ollama go through Semantic Kernel's auto function-calling loop.
    var settings = new PromptExecutionSettings
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
    };
    Console.WriteLine(await kernel.InvokePromptAsync(prompt, new KernelArguments(settings)));
}

if (fetchMcp is not null)
    await fetchMcp.DisposeAsync();
