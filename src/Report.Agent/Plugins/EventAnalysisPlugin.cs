using System.ComponentModel;
using Microsoft.SemanticKernel;
using SwarmFish.Core.Contracts.Models;

namespace SwarmFish.Report.Agent.Plugins;

/// <summary>
/// Plugin for frequency analysis of simulation events.
/// </summary>
public class EventAnalysisPlugin
{
    private readonly IEnumerable<SimulationTick> _events;

    public EventAnalysisPlugin(IEnumerable<SimulationTick> events)
    {
        _events = events;
    }

    [KernelFunction]
    [Description("Get frequency count of a specific event type")]
    public string GetEventFrequency(
        [Description("The type of event to count")] string eventType)
    {
        var count = _events.Count(e => e.PreviousEvents.Any(ev => ev.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase)));
        return $"Event '{eventType}' occurred {count} times.";
    }

    [KernelFunction]
    [Description("Get the most influential agents by event count")]
    public string GetTopAgents(
        [Description("Number of top agents to return")] int count)
    {
        var topAgents = _events
            .SelectMany(e => e.PreviousEvents)
            .GroupBy(ev => ev.AgentId)
            .OrderByDescending(g => g.Count())
            .Take(count)
            .Select(g => $"{g.Key}: {g.Count()} events");

        return string.Join("\n", topAgents);
    }
}
