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
Task.Run(statCalculationWorker.Run);

var cacheUpdateWorker = new CacheUpdateWorker(statsCalculatedQueue, updatedQueue);
Task.Run(cacheUpdateWorker.Run);

var batchNotificationWorker = new BatchNotificationWorker(updatedQueue, 1000, (message) => // Changed to 2000
{
    var content = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));

    lock (connectedClients)
    {
        foreach (WebSocket client in connectedClients)
        {
            client.SendAsync(content, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
});
Task.Run(batchNotificationWorker.Run);

var dummyInventoryProvider = new DummyInventoryProvider(500_000, ingestedQueue);
Task.Run(dummyInventoryProvider.Run);

/*
 * Init server
 */
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers(); 
builder.Services.AddSingleton(cacheUpdateWorker); // Register for DI

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

// Enable CORS middleware
app.UseCors();

app.UseWebSockets();

// Add controller routing
app.MapControllers(); // Add this for API endpoints

app.MapGet("/status", async (context) =>
{
    context.Response.ContentType = "text/plain";

    var latestStatuses = cacheUpdateWorker.GetLatestStatuses();
    for (int i = 0; i < latestStatuses.Length; i++)
    {
        await context.Response.WriteAsync(latestStatuses.Span[i]);
        await context.Response.WriteAsync("\n");
    }
    await context.Response.Body.FlushAsync();
});

app.Map("/ws", async context =>
{
    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    Console.WriteLine("WebSocket connected");

    lock (connectedClients)
    {
        connectedClients.Add(webSocket);
    }

    var buffer = new byte[1024];

    try
    {
        while (true)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine("WebSocket closed");
                
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
                break;
            }
        }
    } finally
    {
        lock (connectedClients)
        {
            connectedClients.Remove(webSocket);
        }
    }
});

app.Run();