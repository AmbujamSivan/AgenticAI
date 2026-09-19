using CommunicationProtocols.Application;
using CommunicationProtocols.Rag;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// A standalone MCP server (stdio transport) exposing the product catalog as tools.
// Any MCP host — Claude Desktop, an IDE, or the Semantic Kernel agent in a later
// phase — can launch this process and call the tools in ProductTools.
var builder = Host.CreateApplicationBuilder(args);

// stdio uses stdout for JSON-RPC, so ALL logging must go to stderr — otherwise
// log lines corrupt the protocol stream.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// The same shared Application layer every other transport uses.
builder.Services.AddSingleton<IProductService, InMemoryProductService>();

// RAG: local hashing embedder + Qdrant vector store + a startup indexer.
// Host/port/collection are overridable; defaults target a local Qdrant on :6334.
builder.Services.AddProductRag(
    qdrantHost: builder.Configuration["Qdrant:Host"] ?? "localhost",
    qdrantPort: int.TryParse(builder.Configuration["Qdrant:Port"], out var port) ? port : 6334);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
