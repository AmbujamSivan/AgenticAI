using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CommunicationProtocols.Application.Streaming;

namespace CommunicationProtocols.Host.Ws;

/// <summary>
/// The raw WebSocket transport adapter. It accepts a socket at <c>/ws/stock</c>,
/// subscribes to the shared <see cref="StockNotifier"/>, and forwards each
/// <see cref="StockChange"/> as a JSON text frame. Contrast this hand-rolled
/// framing/serialization loop with the SignalR version coming in Phase 6.
/// </summary>
public static class StockWebSocket
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapStockWebSocket(this IEndpointRouteBuilder app)
    {
        app.Map("/ws/stock", HandleAsync);
        return app;
    }

    private static async Task HandleAsync(HttpContext context, StockNotifier notifier)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("This endpoint requires a WebSocket connection.");
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var subscription = notifier.Subscribe();

        // Link the request-abort token with a source we trip when the client sends a
        // Close frame, so both a dropped connection and a graceful client close end
        // the send loop. A WebSocket must also be *read* for its close handshake to
        // complete — that's what ReceiveUntilCloseAsync is for.
        using var closed = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var receivePump = ReceiveUntilCloseAsync(socket, closed);

        try
        {
            await foreach (var change in subscription.Reader.ReadAllAsync(closed.Token))
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(change, JsonOptions);
                await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, closed.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected or closed — expected.
        }

        // Complete the close handshake if the socket is still usable.
        if (socket.State == WebSocketState.Open)
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closing", CancellationToken.None);

        await receivePump;
    }

    /// <summary>
    /// This stream is server-push only, but the protocol still requires us to read:
    /// draining incoming frames lets us observe a client's Close and respond to it.
    /// </summary>
    private static async Task ReceiveUntilCloseAsync(WebSocket socket, CancellationTokenSource closed)
    {
        var buffer = new byte[1024];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, closed.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Acknowledging close", CancellationToken.None);
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            closed.Cancel();
        }
    }
}
