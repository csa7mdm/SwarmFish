using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SwarmFish.Agents.Orleans.Models;
using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Contracts.Models;

namespace SwarmFish.Agents.Orleans.Grains;

/// <summary>
/// Orleans grain representing a single simulated agent.
/// Manages persona state, processes simulation ticks via Semantic Kernel,
/// and interacts with memory and graph stores.
/// </summary>
public class AgentGrain : Grain<AgentGrainState>, IAgentGrain, IAgent
{
    private readonly IMemoryStore _memoryStore;
    private readonly IGraphStore _graphStore;
    private readonly Kernel _kernel;
    private readonly ILogger<AgentGrain> _logger;
    private readonly string _promptTemplate;

    /// <summary>
    /// Initialises a new instance of the <see cref="AgentGrain"/> class.
    /// </summary>
    /// <param name="memoryStore">The agent memory store.</param>
    /// <param name="graphStore">The knowledge graph store.</param>
    /// <param name="kernel">The Semantic Kernel instance for LLM calls.</param>
    /// <param name="logger">Logger for structured logging.</param>
    public AgentGrain(
        IMemoryStore memoryStore,
        IGraphStore graphStore,
        Kernel kernel,
        ILogger<AgentGrain> logger)
    {
        _memoryStore = memoryStore;
        _graphStore = graphStore;
        _kernel = kernel;
        _logger = logger;
        _promptTemplate = LoadPromptTemplate();
    }

    /// <inheritdoc />
    public async Task InitialiseAsync(AgentPersona persona, Guid simulationId)
    {
        State.Persona = persona;
        State.SimulationId = simulationId;
        State.Status = AgentStatus.Active;
        State.TicksProcessed = 0;
        await WriteStateAsync();

        // Seed initial memory with persona facts
        var seedEntry = new MemoryEntry(
            Content: $"I am {persona.Name}. {persona.Backstory} My traits: {string.Join(", ", persona.Traits)}",
            CreatedAt: DateTimeOffset.UtcNow,
            Type: MemoryEntryType.SeedFact);

        await _memoryStore.AppendMemoryAsync(persona.Id, seedEntry, CancellationToken.None);

        _logger.LogInformation(
            "Agent {AgentId} initialised with persona {PersonaName} for simulation {SimulationId}",
            this.GetPrimaryKey(), persona.Name, simulationId);
    }

    /// <inheritdoc />
    public Guid Id => this.GetPrimaryKey();

    /// <inheritdoc />
    public string Persona => System.Text.Json.JsonSerializer.Serialize(State.Persona);

    /// <inheritdoc />
    public AgentStatus Status => State.Status;

    /// <inheritdoc />
    public Task<AgentEvent> ProcessTickAsync(SimulationTick tick)
    {
        return ProcessTickInternalAsync(tick, CancellationToken.None);
    }

    Task<AgentEvent> IAgent.ProcessTickAsync(SimulationTick tick, CancellationToken ct)
    {
        return ProcessTickInternalAsync(tick, ct);
    }

    private async Task<AgentEvent> ProcessTickInternalAsync(SimulationTick tick, CancellationToken ct)
    {
        var agentId = this.GetPrimaryKey();

        if (State.Status == AgentStatus.Suppressed)
        {
            _logger.LogDebug("Agent {AgentId} is suppressed, returning silent event", agentId);
            return new AgentEvent(agentId, "silent", "{}", DateTimeOffset.UtcNow);
        }

        if (State.Persona is null)
        {
            throw new InvalidOperationException($"Agent {agentId} has not been initialised.");
        }

        // 1. Retrieve relevant memories (top-5 semantic search)
        var memories = await _memoryStore.SearchMemoryAsync(
            agentId,
            $"round {tick.Round} {tick.Context.PredictionQuery}",
            topK: 5,
            ct);

        // 2. Retrieve graph context (2-hop neighbourhood)
        var neighbours = await _graphStore.GetNeighboursAsync(
            agentId.ToString(),
            depth: 2,
            ct);

        // 3. Build prompt
        var prompt = BuildPrompt(State.Persona, memories, neighbours, tick);

        // 4. Call LLM via Semantic Kernel
        string llmResponse;
        try
        {
            var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct);
            llmResponse = result.GetValue<string>() ?? "{}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "LLM call failed for agent {AgentId} at round {Round}, returning silent", agentId, tick.Round);
            llmResponse = """{"eventType": "silent", "payload": "LLM call failed", "targetAgentId": null}""";
        }

        ct.ThrowIfCancellationRequested();

        // 5. Parse response into AgentEvent
        var agentEvent = ParseLlmResponse(agentId, llmResponse);

        // 6. Append event as new memory entry
        var memoryEntry = new MemoryEntry(
            Content: $"Round {tick.Round}: I {agentEvent.EventType} — {agentEvent.Payload}",
            CreatedAt: DateTimeOffset.UtcNow,
            Type: MemoryEntryType.Observation);

        await _memoryStore.AppendMemoryAsync(agentId, memoryEntry, ct);

        // 7. Update state
        State.TicksProcessed++;
        await WriteStateAsync();

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
        await WriteStateAsync();
        _logger.LogInformation("Agent {AgentId} suppressed", this.GetPrimaryKey());
    }

    /// <inheritdoc />
    public async Task ReactivateAsync()
    {
        State.Status = AgentStatus.Active;
        await WriteStateAsync();
        _logger.LogInformation("Agent {AgentId} reactivated", this.GetPrimaryKey());
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
