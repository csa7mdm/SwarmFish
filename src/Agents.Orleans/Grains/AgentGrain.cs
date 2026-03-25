using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Orleans.Runtime;
using SwarmFish.Agents.Orleans.Models;
using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Contracts.Models;

namespace SwarmFish.Agents.Orleans.Grains;

/// <summary>
/// Orleans grain representing a single simulated agent.
/// Manages persona state, processes simulation ticks via Semantic Kernel,
/// and interacts with memory and graph stores.
/// </summary>
public class AgentGrain : Grain, IAgentGrain
{
    private readonly IPersistentState<AgentGrainState> _state;
    private readonly IMemoryStore _memoryStore;
    private readonly IGraphStore _graphStore;
    private readonly Kernel _kernel;
    private readonly ILogger<AgentGrain> _logger;
    private readonly string _promptTemplate;

    private AgentGrainState State => _state.State;

    /// <summary>
    /// Initialises a new instance of the <see cref="AgentGrain"/> class.
    /// </summary>
    /// <param name="state">The persistent state facet.</param>
    /// <param name="memoryStore">The agent memory store.</param>
    /// <param name="graphStore">The knowledge graph store.</param>
    /// <param name="kernel">The Semantic Kernel instance for LLM calls.</param>
    /// <param name="logger">Logger for structured logging.</param>
    public AgentGrain(
        [PersistentState("agent")] IPersistentState<AgentGrainState> state,
        IMemoryStore memoryStore,
        IGraphStore graphStore,
        Kernel kernel,
        ILogger<AgentGrain> logger)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _memoryStore = memoryStore ?? throw new ArgumentNullException(nameof(memoryStore));
        _graphStore = graphStore ?? throw new ArgumentNullException(nameof(graphStore));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _promptTemplate = LoadPromptTemplate();
    }

    /// <inheritdoc />
    public async Task InitialiseAsync(AgentPersona persona, Guid simulationId)
    {
        State.Persona = persona;
        State.SimulationId = simulationId;
        State.Status = AgentStatus.Active;
        State.TicksProcessed = 0;
        await _state.WriteStateAsync();

        // Seed initial memory with persona facts
        var seedEntry = new MemoryEntry(
            Content: $"I am {persona.Name}. {persona.Backstory} My traits: {string.Join(", ", persona.Traits)}",
            CreatedAt: DateTimeOffset.UtcNow,
            Type: MemoryEntryType.SeedFact);

        await _memoryStore.AppendMemoryAsync(persona.Id, seedEntry, CancellationToken.None);

        _logger.LogInformation(
            "Agent {AgentId} initialised with persona {PersonaName} for simulation {SimulationId}",
            persona.Id, persona.Name, simulationId);
    }

    /// <inheritdoc />
    public async Task<AgentEvent> ProcessTickAsync(SimulationTick tick)
    {
        if (State.Persona is null)
        {
            throw new InvalidOperationException("Agent has not been initialised.");
        }

        var agentId = State.Persona.Id;

        if (State.Status == AgentStatus.Suppressed)
        {
            _logger.LogDebug("Agent {AgentId} is suppressed, returning silent event", agentId);
            return new AgentEvent(agentId, "silent", "{}", DateTimeOffset.UtcNow);
        }

        // 1. Retrieve relevant memories (top-5 semantic search)
        var memories = await _memoryStore.SearchMemoryAsync(
            agentId,
            $"round {tick.Round} {tick.Context.PredictionQuery}",
            topK: 5,
            CancellationToken.None);

        // 2. Retrieve graph context (2-hop neighbourhood)
        var neighbours = await _graphStore.GetNeighboursAsync(
            agentId.ToString(),
            depth: 2,
            CancellationToken.None);

        // 3. Build prompt
        var prompt = BuildPrompt(State.Persona, memories, neighbours, tick);

        // 4. Call LLM via Semantic Kernel
        string llmResponse;
        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            llmResponse = result.GetValue<string>() ?? "{}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM call failed for agent {AgentId} at round {Round}, returning silent", agentId, tick.Round);
            llmResponse = """{"eventType": "silent", "payload": "LLM call failed", "targetAgentId": null}""";
        }

        // 5. Parse response into AgentEvent
        var agentEvent = ParseLlmResponse(agentId, llmResponse);

        // 6. Append event as new memory entry
        var memoryEntry = new MemoryEntry(
            Content: $"Round {tick.Round}: I {agentEvent.EventType} — {agentEvent.Payload}",
            CreatedAt: DateTimeOffset.UtcNow,
            Type: MemoryEntryType.Observation);

        await _memoryStore.AppendMemoryAsync(agentId, memoryEntry, CancellationToken.None);

        // 7. Update state
        State.TicksProcessed++;
        await _state.WriteStateAsync();

        _logger.LogDebug(
            "Agent {AgentId} processed tick {Round}, event type: {EventType}",
            agentId, tick.Round, agentEvent.EventType);

        return agentEvent;
    }

    /// <inheritdoc />
    public Task<AgentPersona?> GetPersonaAsync()
    {
        return Task.FromResult(State.Persona);
    }

    /// <inheritdoc />
    public async Task SuppressAsync()
    {
        State.Status = AgentStatus.Suppressed;
        await _state.WriteStateAsync();
        _logger.LogInformation("Agent {AgentId} suppressed", State.Persona?.Id);
    }

    /// <inheritdoc />
    public async Task ReactivateAsync()
    {
        State.Status = AgentStatus.Active;
        await _state.WriteStateAsync();
        _logger.LogInformation("Agent {AgentId} reactivated", State.Persona?.Id);
    }

    private string BuildPrompt(
        AgentPersona persona,
        IReadOnlyList<MemoryEntry> memories,
        IReadOnlyList<GraphNode> neighbours,
        SimulationTick tick)
    {
        var memoriesText = memories.Count > 0
            ? string.Join("\n", memories.Select(m => $"- [{m.Type}] {m.Content}"))
            : "No relevant memories.";

        var graphText = neighbours.Count > 0
            ? string.Join("\n", neighbours.Select(n => $"- {n.Label}: {n.Id} ({string.Join(", ", n.Properties.Select(p => $"{p.Key}={p.Value}"))})"))
            : "No social connections found.";

        var previousEventsText = tick.PreviousEvents.Count > 0
            ? string.Join("\n", tick.PreviousEvents.Take(20).Select(e => $"- Agent {e.AgentId}: [{e.EventType}] {e.Payload}"))
            : "No previous events.";

        return _promptTemplate
            .Replace("{persona.Name}", persona.Name)
            .Replace("{persona.Backstory}", persona.Backstory)
            .Replace("{persona.Traits}", string.Join(", ", persona.Traits))
            .Replace("{memories}", memoriesText)
            .Replace("{graphContext}", graphText)
            .Replace("{tick.Round}", tick.Round.ToString())
            .Replace("{tick.SimulatedTime}", tick.SimulatedTime.ToString("o"))
            .Replace("{previousEvents}", previousEventsText);
    }

    private static AgentEvent ParseLlmResponse(Guid agentId, string response)
    {
        try
        {
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var eventType = root.TryGetProperty("eventType", out var et)
                ? et.GetString() ?? "silent"
                : "silent";

            var payload = root.TryGetProperty("payload", out var pl)
                ? pl.GetString() ?? ""
                : "";

            return new AgentEvent(agentId, eventType, payload, DateTimeOffset.UtcNow);
        }
        catch (JsonException)
        {
            // If LLM response isn't valid JSON, wrap the raw text as a "spoke" event
            return new AgentEvent(agentId, "spoke", response, DateTimeOffset.UtcNow);
        }
    }

    private static string LoadPromptTemplate()
    {
        var assembly = typeof(AgentGrain).Assembly;
        var resourceName = "SwarmFish.Agents.Orleans.Resources.agent_tick_prompt.txt";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return "You are {persona.Name}. Decide what to do this round. Respond as JSON with eventType, payload, targetAgentId.";
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
