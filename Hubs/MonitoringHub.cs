using Microsoft.AspNetCore.SignalR;

namespace OptimalyAI.Hubs;

public class MonitoringHub : Hub
{
    private readonly ILogger<MonitoringHub> _logger;

    public MonitoringHub(ILogger<MonitoringHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task Subscribe(string metric)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"metric-{metric}");
        _logger.LogInformation("Client {ConnectionId} subscribed to {Metric}", Context.ConnectionId, metric);
    }

    public async Task Unsubscribe(string metric)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"metric-{metric}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from {Metric}", Context.ConnectionId, metric);
    }
}