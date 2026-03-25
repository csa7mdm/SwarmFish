namespace SwarmFish.Agents.Orleans.Models;

/// <summary>
/// Represents the persona definition for a simulated agent.
/// </summary>
/// <param name="Id">The unique identifier for this persona.</param>
/// <param name="Name">The display name of the persona.</param>
/// <param name="Backstory">The background narrative that shapes the agent's behaviour.</param>
/// <param name="Traits">A list of personality traits that influence decision-making.</param>
[GenerateSerializer]
public record AgentPersona(
    [property: Id(0)] Guid Id,
    [property: Id(1)] string Name,
    [property: Id(2)] string Backstory,
    [property: Id(3)] IReadOnlyList<string> Traits
);
