using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace CommunicationProtocols.Agent;

/// <summary>
/// Connects to an MCP server over stdio, lists its tools, and imports them into the
/// kernel as callable functions. This is the bridge that lets the LLM plan over our
/// product catalog (and, later, an external community MCP) — each MCP tool becomes a
/// KernelFunction the model can choose to call.
/// </summary>
public static class McpToolSource
{
    public static async Task<IAsyncDisposable> AddStdioMcpToolsAsync(
        Kernel kernel,
        string pluginName,
        string command,
        IEnumerable<string> arguments,
        CancellationToken ct = default)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = pluginName,
            Command = command,
            Arguments = arguments.ToList(),
        });

        var client = await McpClient.CreateAsync(transport, cancellationToken: ct);

        var tools = await client.ListToolsAsync(cancellationToken: ct);
        kernel.Plugins.AddFromFunctions(pluginName, tools.Select(t => t.AsKernelFunction()));

        return client;
    }
}
