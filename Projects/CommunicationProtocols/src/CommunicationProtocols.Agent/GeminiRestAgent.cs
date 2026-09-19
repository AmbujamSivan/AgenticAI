using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.SemanticKernel;

namespace CommunicationProtocols.Agent;

/// <summary>
/// Calls the Gemini REST API (generateContent) directly and runs its function-calling
/// loop by hand, instead of going through the alpha SK Google connector (whose tool
/// schema is rejected by the current Gemini API). It reuses the MCP tools already
/// imported into the kernel, so both our catalog MCP and the external Fetch MCP work.
///
/// The two things that make Gemini accept our tools: (1) sanitize the JSON schema to the
/// subset Gemini allows, and (2) OMIT the parameters field entirely for no-arg tools.
/// </summary>
public sealed class GeminiRestAgent(HttpClient http, string apiKey, string model)
{
    // Gemini's function-declaration schema is a small OpenAPI subset. Keep only these
    // keys and drop everything else (e.g. exclusiveMinimum/Maximum, additionalProperties,
    // $schema, title, pattern) — any unknown key makes the API reject the whole request.
    private static readonly HashSet<string> AllowedSchemaKeys =
        ["type", "format", "description", "nullable", "enum", "items", "properties", "required"];

    public async Task<string> RunAsync(Kernel kernel, string prompt, CancellationToken ct = default)
    {
        var functions = kernel.Plugins.SelectMany(p => p).ToDictionary(f => f.Name);
        var declarations = BuildFunctionDeclarations(functions.Values);

        var contents = new JsonArray
        {
            new JsonObject { ["role"] = "user", ["parts"] = new JsonArray { new JsonObject { ["text"] = prompt } } },
        };

        // Bounded loop so a misbehaving model can't spin forever.
        for (var step = 0; step < 8; step++)
        {
            var candidate = await CallGeminiAsync(contents, declarations, ct);
            var parts = candidate?["content"]?["parts"] as JsonArray ?? [];

            var calls = parts.Where(p => p?["functionCall"] is not null).ToList();
            if (calls.Count == 0)
                return string.Concat(parts.Select(p => p?["text"]?.GetValue<string>()).Where(t => t is not null));

            // Record the model's tool-call turn, then answer each call.
            contents.Add(CloneNode(candidate!["content"]!));

            var responseParts = new JsonArray();
            foreach (var call in calls)
            {
                var fc = call!["functionCall"]!;
                var name = fc["name"]!.GetValue<string>();
                Console.Error.WriteLine($"[gemini] -> {name}");

                var result = functions.TryGetValue(name, out var fn)
                    ? await InvokeAsync(kernel, fn, fc["args"] as JsonObject, ct)
                    : $"No such tool: {name}";

                responseParts.Add(new JsonObject
                {
                    ["functionResponse"] = new JsonObject
                    {
                        ["name"] = name,
                        ["response"] = new JsonObject { ["result"] = result },
                    },
                });
            }
            contents.Add(new JsonObject { ["role"] = "user", ["parts"] = responseParts });
        }

        return "(stopped: too many tool-call rounds)";
    }

    private async Task<JsonNode?> CallGeminiAsync(JsonArray contents, JsonArray declarations, CancellationToken ct)
    {
        var body = new JsonObject { ["contents"] = CloneNode(contents) };
        if (declarations.Count > 0)
            body["tools"] = new JsonArray { new JsonObject { ["functionDeclarations"] = CloneNode(declarations) } };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
        using var resp = await http.PostAsync(url,
            new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"), ct);

        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini {(int)resp.StatusCode}: {json}");

        return JsonNode.Parse(json)?["candidates"]?[0];
    }

    private async Task<string> InvokeAsync(Kernel kernel, KernelFunction fn, JsonObject? args, CancellationToken ct)
    {
        var kernelArgs = new KernelArguments();
        if (args is not null)
            foreach (var (key, value) in args)
                kernelArgs[key] = ToClr(value);

        try
        {
            var result = await fn.InvokeAsync(kernel, kernelArgs, ct);
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Tool error: {ex.Message}";
        }
    }

    private JsonArray BuildFunctionDeclarations(IEnumerable<KernelFunction> functions)
    {
        var declarations = new JsonArray();
        foreach (var fn in functions)
        {
            var declaration = new JsonObject
            {
                ["name"] = fn.Name,
                ["description"] = fn.Description,
            };

            var properties = new JsonObject();
            var required = new JsonArray();
            foreach (var param in fn.Metadata.Parameters)
            {
                properties[param.Name] = ParameterSchema(param);
                if (param.IsRequired)
                    required.Add(param.Name);
            }

            // Gemini rejects an empty properties object — only include parameters when there are some.
            if (properties.Count > 0)
            {
                var parameters = new JsonObject { ["type"] = "object", ["properties"] = properties };
                if (required.Count > 0)
                    parameters["required"] = required;
                declaration["parameters"] = parameters;
            }

            declarations.Add(declaration);
        }
        return declarations;
    }

    private JsonNode ParameterSchema(KernelParameterMetadata param)
    {
        // Prefer the tool's own JSON schema (from MCP), sanitized to Gemini's subset.
        if (param.Schema is not null
            && JsonNode.Parse(param.Schema.ToString()) is JsonObject schema)
        {
            Sanitize(schema);
            if (schema["type"] is null)
                schema["type"] = "string";
            return schema;
        }

        return new JsonObject { ["type"] = "string", ["description"] = param.Description ?? param.Name };
    }

    private void Sanitize(JsonObject node)
    {
        foreach (var key in node.Select(kv => kv.Key).Where(k => !AllowedSchemaKeys.Contains(k)).ToList())
            node.Remove(key);

        if (node["properties"] is JsonObject props)
            foreach (var child in props)
                if (child.Value is JsonObject childObj)
                    Sanitize(childObj);

        if (node["items"] is JsonObject items)
            Sanitize(items);
    }

    private static object? ToClr(JsonNode? node) => node is JsonValue value
        ? value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>(),
            JsonValueKind.True or JsonValueKind.False => value.GetValue<bool>(),
            JsonValueKind.Number => value.TryGetValue<long>(out var l) ? l : value.GetValue<double>(),
            _ => value.ToJsonString(),
        }
        : node?.ToJsonString();

    private static JsonNode CloneNode(JsonNode node) => JsonNode.Parse(node.ToJsonString())!;
}
