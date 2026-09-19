using CommunicationProtocols.Application;
using CommunicationProtocols.Application.Streaming;
using CommunicationProtocols.Host.GraphQl;
using CommunicationProtocols.Host.Grpc;
using CommunicationProtocols.Host.SignalR;
using CommunicationProtocols.Host.Ws;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// gRPC needs HTTP/2. Over cleartext (no TLS) HTTP/1 and HTTP/2 can't share a port,
// so we give each protocol its own endpoint:
//   :5199  HTTP/1  → REST + GraphQL + Swagger
//   :5200  HTTP/2  → gRPC (h2c)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5199, o => o.Protocols = HttpProtocols.Http1);
    options.ListenLocalhost(5200, o => o.Protocols = HttpProtocols.Http2);
});

// Shared Application layer — the single source of truth every transport calls into.
// Singleton so the in-memory seeded data persists for the life of the process.
builder.Services.AddSingleton<IProductService, InMemoryProductService>();

// REST transport via classic Web API controllers.
builder.Services.AddControllers();

// GraphQL transport via HotChocolate.
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>();

// gRPC transport (+ reflection so tools like grpcurl can discover the schema).
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

// Streaming: a shared notifier plus a background ticker that simulates stock
// movement. Both the WebSocket (Phase 5) and SignalR (Phase 6) transports read
// from this same StockNotifier.
builder.Services.AddSingleton<StockNotifier>();
builder.Services.AddHostedService<StockTicker>();

// SignalR transport: a hub fed from the same StockNotifier via a bridge.
builder.Services.AddSignalR();
builder.Services.AddHostedService<StockHubBroadcaster>();

// Swagger / OpenAPI so the REST surface is browsable.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "CommunicationProtocols — REST", Version = "v1" });
});

var app = builder.Build();

// Serve the static landing page (wwwroot/index.html) at /.
app.UseDefaultFiles();
app.UseStaticFiles();

// Enable WebSocket support (HTTP/1.1 upgrade) for the /ws/* endpoints.
app.UseWebSockets();

app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "CommunicationProtocols REST v1");
    o.RoutePrefix = "swagger";
});

app.MapControllers();

// GraphQL endpoint + Nitro IDE at /graphql.
app.MapGraphQL();

// gRPC service on the HTTP/2 endpoint; reflection enabled in Development.
app.MapGrpcService<ProductGrpcService>();
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();

// WebSocket transport: live stock stream at /ws/stock.
app.MapStockWebSocket();

// SignalR transport: same stream over a hub at /hubs/stock.
app.MapHub<StockHub>("/hubs/stock");

app.Run();
