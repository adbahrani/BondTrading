using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using BondsServer;
using Microsoft.AspNetCore.Http.HttpResults;

var connectedClients = new HashSet<WebSocket>();

/*
 * Init workers 
 */
var ingestedQueue = new BlockingCollection<Bond>();
var statsCalculatedQueue = new BlockingCollection<BondWithStatistics>();
var updatedQueue = new BlockingCollection<BondUpdate>();

var statCalculationWorker = new StatCalculationWorker(ingestedQueue, statsCalculatedQueue);

var cacheUpdateWorker = new CacheUpdateWorker(statsCalculatedQueue, updatedQueue);

// set to 500 to reduce browser load
// Move batchCount outside the lambda
int batchCount = 0;

var batchNotificationWorker = new BatchNotificationWorker(updatedQueue, 200, (message) =>
{
    var content = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
    
    // Add timeout protection
    var startTime = DateTimeOffset.UtcNow;
    
    lock (connectedClients)
    {
        if (connectedClients.Count == 0) return;
        
        var clientsToRemove = new List<WebSocket>();
        
        // Use a copy to avoid collection modification issues
        var currentClients = connectedClients.ToArray();
        
        foreach (WebSocket client in currentClients)
        {
            try
            {
                if (client.State != WebSocketState.Open)
                {
                    clientsToRemove.Add(client);
                    continue;
                }
                
                // Fire and forget - don't wait or check status
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await client.SendAsync(content, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch
                    {
                        // Ignore individual send failures
                    }
                });
            }
            catch
            {
                clientsToRemove.Add(client);
            }
        }

        // Remove dead connections
        foreach (var client in clientsToRemove)
        {
            connectedClients.Remove(client);
        }

        // Debug logging with timeout check
        batchCount++;
        var elapsed = DateTimeOffset.UtcNow.Subtract(startTime).TotalMilliseconds;
        
        if (batchCount % 200 == 0)
        {
            Console.WriteLine($"Broadcast batch #{batchCount} ({content.Count} bytes) to {connectedClients.Count} clients in {elapsed:F0}ms");
            if (clientsToRemove.Count > 0)
            {
                Console.WriteLine($"Removed {clientsToRemove.Count} dead connections");
            }
        }
    }
});

// Pre-seed cache with intial state
var initialBonds = DummyInventoryProvider.GenerateBonds(500_000);
var initialBondsWithStats = initialBonds.Select(b => StatCalculationWorker.CalculateStats(b)).ToList();
cacheUpdateWorker.Initialize(initialBondsWithStats);

// Start workers
var dummyInventoryProvider = new DummyInventoryProvider(initialBonds, ingestedQueue);

Task.Run(batchNotificationWorker.Run);
Task.Run(cacheUpdateWorker.Run);
Task.Run(statCalculationWorker.Run);
Task.Run(dummyInventoryProvider.Run);

/*
 * Init server
 */
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddSingleton(cacheUpdateWorker);

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Serve static index.html by default  
app.UseDefaultFiles();      
app.UseStaticFiles();    

app.UseCors();

app.UseWebSockets();

app.MapControllers();

app.MapGet("/status", async (context) =>
{
    context.Response.ContentType = "text/plain";

    var latestStatuses = cacheUpdateWorker.GetLatestStatuses();
    for (int i = 0; i < latestStatuses.Length; i++)
    {
        await context.Response.WriteAsync(latestStatuses.Span[i].Item1);
        await context.Response.WriteAsync("\n");
    }
    await context.Response.Body.FlushAsync();
});

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    Console.WriteLine($"WebSocket connected from {context.Connection.RemoteIpAddress}");

    lock (connectedClients)
    {
        connectedClients.Add(webSocket);
    }

    var buffer = new byte[1024];

    try
    {
        // Send initial heartbeat
        await webSocket.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes("CONNECTED\n")), 
            WebSocketMessageType.Text, 
            true, 
            CancellationToken.None);

        while (webSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("WebSocket close requested");
                    break;
                }
                
                // Handle ping/pong for keep-alive
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (message == "PING")
                    {
                        await webSocket.SendAsync(
                            new ArraySegment<byte>(Encoding.UTF8.GetBytes("PONG")), 
                            WebSocketMessageType.Text, 
                            true, 
                            CancellationToken.None);
                    }
                }
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                Console.WriteLine("WebSocket connection closed prematurely");
                break;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WebSocket error: {ex.Message}");
    }
    finally
    {
        lock (connectedClients)
        {
            connectedClients.Remove(webSocket);
        }
        
        if (webSocket.State == WebSocketState.Open)
        {
            try
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closing", CancellationToken.None);
            }
            catch { /* Ignore cleanup errors */ }
        }
        
        Console.WriteLine("WebSocket disconnected");
    }
});

app.MapGet("/", context =>
{
    context.Response.Redirect("/index.html");
    return Task.CompletedTask;
});

app.Run();