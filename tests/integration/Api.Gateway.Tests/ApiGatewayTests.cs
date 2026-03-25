using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SwarmFish.Core.Contracts.Models;
using SwarmFish.Core.Contracts.Interfaces;
using Xunit;
using System.Threading.Tasks;
using System;

namespace SwarmFish.Api.Gateway.Tests;

public class ApiGatewayTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiGatewayTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSimulations_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/simulations", new SimulationConfig(
            Guid.NewGuid(), 
            50, 
            10, 
            "What is the future of AI?",
            SimulationMode.Standard
        ));

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<dynamic>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ScalarUI_IsAvailable()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/scalar/v1");

        // Assert
        response.EnsureSuccessStatusCode();
    }
}
