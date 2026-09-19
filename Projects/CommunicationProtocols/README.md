# CommunicationProtocols

A personal **reference project**: one shared domain, exposed over many communication
protocols, so they can be compared apples-to-apples. Built to be extended — adding a
new protocol means adding one thin transport adapter, never a rewrite.

## Core idea

```
Domain  ──►  Application (IProductService)  ──►  many transports
                     ▲
        the single source of truth; every transport is a thin adapter over it
```

No business logic lives in any transport. REST, GraphQL, gRPC, WebSocket, SignalR and
the Tier-2 agentic/MCP layer are all just different ways to reach the *same* service.

## Two tiers

**Tier 1 — plain protocol references** (`main`)
| Phase | Transport | Status |
|------|-----------|--------|
| 1 | Domain + Application skeleton (in-memory `Product`) | ✅ done |
| 2 | REST (minimal APIs) + Swagger | ✅ done |
| 3 | GraphQL (HotChocolate) | ✅ done |
| 4 | gRPC + protobuf | ✅ done |
| 5 | WebSocket (live stock stream) | ✅ done |
| 6 | SignalR (same stream, compared to raw WS) | ✅ done |
| 7 | Landing page (static HTML + live WS feed) | ✅ done |

**Tier 2 — agentic AI layer** (`agentic-tier` branch, built on top of the same Application layer)
| Phase | Piece | Status |
|------|-------|--------|
| T2.1 | MCP server exposing the domain as tools (C# ModelContextProtocol SDK) | ✅ done |
| T2.2 | RAG: `semantic_search` over a Qdrant vector store (embedder swappable) | ✅ done |
| T2.3a | Semantic Kernel agent using our MCP tools (incl. RAG), pluggable LLM provider | ✅ done |
| T2.3b | Agent also consumes an external community MCP (Fetch/HTTP) | ✅ done |

### Agent (T2.3a)

`CommunicationProtocols.Agent` is a Semantic Kernel agent that launches our MCP server,
imports its tools, and lets the LLM plan tool calls over the catalog (including RAG
`semantic_search`). The provider is **pluggable** (like the RCA Engine): it defaults to
a **free local Ollama** model (`llama3.2`) so it runs with no cloud keys; set
`LLM__Provider=OpenAI` / `Google` (+ the matching `*_API_KEY`) to switch. Keys come from
the environment only — copy `.env.example` to `.env` (gitignored).

**T2.3b — multi-MCP:** pass `--fetch true` to also attach the external community
**Fetch MCP** (`uvx mcp-server-fetch`) as a second tool source, so the agent can plan
across our catalog tools *and* web fetch:

```bash
docker compose up -d                                   # Qdrant (RAG dependency)
dotnet run --project src/CommunicationProtocols.Agent -- --fetch true \
  --prompt "Find the cheapest in-stock product, then fetch https://example.com."
```

**Verified end-to-end with OpenAI (`gpt-4o-mini`) and Google (`gemini-flash-latest`):**
the agent reliably invokes RAG `semantic_search`, and with `--fetch true` plans across
**both** MCP servers in one turn (names a catalog product *and* quotes fetched web content).
The Google path calls **Gemini's REST API directly** (`GeminiRestAgent`) with a hand-rolled
tool-call loop — bypassing the alpha SK Google connector, whose tool schema the current
Gemini API rejects. It sanitizes each MCP tool's JSON schema to Gemini's supported subset
(allow-list of keys) and omits `parameters` for no-arg tools.

**Notes / limitations (verified):**
- Small local models (llama3.2 3B) handle a *single* simple tool call but often
  mis-format complex or multi-tool calls (emitting the call as text). Both MCP sources
  still import and the model *selects* the right tool — use a cloud model for dependable
  orchestration.
- **TLS on macOS:** cloud calls initially failed with `RevocationStatusUnknown` because
  .NET hard-fails when it can't confirm cert revocation, and CAs (Let's Encrypt) retired
  OCSP in 2025. `KernelFactory` sets `X509RevocationMode.NoCheck` on the cloud connectors'
  `HttpClient` — chain + hostname are still validated; only the (now-unavailable) online
  revocation lookup is skipped, matching curl/browser behavior.
- The **alpha SK Google connector** rejects our tool schema (400), so the Google provider
  uses a direct Gemini REST client instead (see `GeminiRestAgent`). OpenAI/Ollama use SK's
  built-in function-calling loop.

### RAG (T2.2)

`CommunicationProtocols.Rag` embeds each product into **Qdrant** and adds a
`semantic_search` MCP tool so an agent can query by intent ("something for a home
office") instead of exact keywords. The embedder is a dependency-free **local hashing
embedder** (matches on shared vocabulary — a placeholder for a real embedding model,
swappable via the `ITextEmbedder` interface). The vector store is behind
`IProductVectorStore`, so Pinecone could replace Qdrant with a DI change.

Start Qdrant first (the MCP server indexes into it on startup):

```bash
docker compose up -d        # Qdrant on :6333 (REST) and :6334 (gRPC)
```

### MCP server (T2.1)

A standalone **stdio** MCP server (`CommunicationProtocols.Mcp`) exposes the catalog as
tools — `list_products`, `get_product`, `create_product`, `adjust_stock` — each a thin
wrapper over the same `IProductService`. Any MCP host can launch it:

```jsonc
// e.g. Claude Desktop's claude_desktop_config.json
{
  "mcpServers": {
    "products": {
      "command": "dotnet",
      "args": ["run", "--project",
        "<abs-path>/CommunicationProtocols/src/CommunicationProtocols.Mcp"]
    }
  }
}
```

## Layout

```
src/
  CommunicationProtocols.Domain/        # entities (Product)
  CommunicationProtocols.Application/   # IProductService + in-memory impl
  CommunicationProtocols.Host/          # REST, GraphQL, gRPC, WebSocket, SignalR
  CommunicationProtocols.Mcp/           # MCP server + semantic_search tool (Tier 2)
  CommunicationProtocols.Rag/           # embedder + Qdrant vector store + search (Tier 2)
  CommunicationProtocols.Agent/         # Semantic Kernel agent, pluggable LLM (Tier 2)
```

## Requirements

- .NET 9 SDK (pinned via `global.json`)

## Build & run

```bash
dotnet build
dotnet run --project src/CommunicationProtocols.Host
```

The host listens on two ports (gRPC needs HTTP/2, which can't share a cleartext
port with HTTP/1):

| Port | Protocol | Serves |
|------|----------|--------|
| 5199 | HTTP/1 | REST (`/api/...`, Swagger at `/swagger`), GraphQL (`/graphql`), WebSocket (`/ws/stock`), SignalR (`/hubs/stock`) |
| 5200 | HTTP/2 (h2c) | gRPC (`product.ProductCatalog`) |

Try gRPC with reflection:

```bash
grpcurl -plaintext localhost:5200 list
grpcurl -plaintext -d '{}' localhost:5200 product.ProductCatalog/GetProducts
```
