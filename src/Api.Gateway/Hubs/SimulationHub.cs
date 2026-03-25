using Microsoft.AspNetCore.SignalR;

namespace SwarmFish.Api.Gateway.Hubs;

/// <summary>
/// Hub for real-time simulation progress updates.
/// </summary>
public class SimulationHub : Hub
{
    /// <summary>
    /// Subscribes the client to updates for a specific simulation.
    /// </summary>
    /// <param name="simulationId">The ID of the simulation to follow.</param>
    public async Task Subscribe(string simulationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, simulationId);
    }
}
