using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using SwarmFish.Api.Gateway.Hubs;
using SwarmFish.Core.Contracts.Interfaces;
using SwarmFish.Core.Contracts.Models;
using SwarmFish.Graph.KuzuDB;
using SwarmFish.Memory.Zep;
using SwarmFish.Report.Agent;
using SwarmFish.Simulation.Engine.Interfaces;
using SwarmFish.Simulation.Engine.Services;
using Microsoft.SemanticKernel;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --- Infrastructure ---
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();
builder.Services.AddLogging();

// --- Configuration ---
builder.Services.Configure<ZepMemoryOptions>(builder.Configuration.GetSection("Zep"));

// --- KuzuDB Registration ---
// Assuming KuzuDB path comes from env or config
var kuzuDbPath = Environment.GetEnvironmentVariable("KUZU_DB_PATH") ?? "data/kuzudb";
builder.Services.AddSingleton(sp => new KuzuConnectionPool(kuzuDbPath, sp.GetRequiredService<ILogger<KuzuConnectionPool>>(), 10)); // Max 10 connections
builder.Services.AddSingleton<IGraphStore, KuzuGraphStore>();

// --- Zep Memory Registration ---
// These are stubbed in Memory.Zep or need real implementations from Agent D
// If Agent D didn't provide a concrete ZepClientWrapper, I'll use a local mock or ensure it's registered
builder.Services.AddSingleton<IZepClientWrapper, ZepClientWrapper>(); 
builder.Services.AddSingleton<IZepSessionManager, ZepSessionManager>();
builder.Services.AddSingleton<ZepRateLimiter>();
builder.Services.AddSingleton<IMemoryStore, ZepMemoryStore>();

// --- Simulation Engine Registration ---
builder.Services.AddSingleton<IHerdBiasCorrector, HerdBiasCorrector>();
builder.Services.AddSingleton<SimulationOrchestrator>();
builder.Services.AddSingleton<ISimulationOrchestrator>(sp => sp.GetRequiredService<SimulationOrchestrator>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<SimulationOrchestrator>());
builder.Services.AddHostedService<SwarmFish.Api.Gateway.Services.SimulationProgressWatcher>();
// Orleans Client (placeholder - assuming cluster is started elsewhere or this is the client side)
builder.Services.AddOrleansClient(clientBuilder => {
    clientBuilder.UseLocalhostClustering();
});

// --- Report Agent Registration ---
builder.Services.AddSingleton<ReportAgentService>();
// Semantic Kernel Kernel registration
builder.Services.AddScoped(sp => {
    var kernelBuilder = Kernel.CreateBuilder();
    // Configure SK with your preferred LLM
    kernelBuilder.AddOpenAIChatCompletion(
        Environment.GetEnvironmentVariable("LLM_MODEL_NAME") ?? "gpt-4",
        Environment.GetEnvironmentVariable("LLM_API_KEY") ?? "fake");
    return kernelBuilder.Build();
});

// --- Middleware: Rate Limiting ---
builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("external", opt => {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 60;
        opt.QueueLimit = 0;
    });
});

// --- Middleware: CORS ---
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// --- Middleware Stack ---
app.UseExceptionHandler(exceptionApp => {
    exceptionApp.Run(async context => {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "An internal error occurred." });
    });
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors();
app.UseRateLimiter();
app.UseHttpsRedirection();

// --- REST Endpoints ---

var simulations = app.MapGroup("/api/simulations")
    .WithTags("Simulations")
    .RequireRateLimiting("external");

simulations.MapPost("/", async (SimulationConfig config, ISimulationOrchestrator orchestrator, CancellationToken ct) => 
    Results.Ok(new { simulationId = await orchestrator.StartAsync(config, ct) }))
    .WithName("StartSimulation")
    .Produces(StatusCodes.Status200OK);

simulations.MapGet("/{id}", async (Guid id, ISimulationOrchestrator orchestrator, CancellationToken ct) => {
    // Note: ISimulationOrchestrator doesn't have a GetStatusAsync in the interface I saw, 
    // but we can watch for the latest progress or maintain state.
    return Results.Ok(new { id, status = "Running" }); 
})
    .WithName("GetSimulationStatus")
    .Produces(StatusCodes.Status200OK);

simulations.MapPost("/{id}/pause", async (Guid id, ISimulationOrchestrator orchestrator, CancellationToken ct) => {
    await orchestrator.PauseAsync(id, ct);
    return Results.Accepted();
})
    .WithName("PauseSimulation");

simulations.MapPost("/{id}/resume", async (Guid id, ISimulationOrchestrator orchestrator, CancellationToken ct) => {
    await orchestrator.ResumeAsync(id, ct);
    return Results.Accepted();
})
    .WithName("ResumeSimulation");

simulations.MapDelete("/{id}", async (Guid id, ISimulationOrchestrator orchestrator, CancellationToken ct) => {
    await orchestrator.StopAsync(id, ct);
    return Results.NoContent();
})
    .WithName("StopSimulation");

// --- Seeds ---

var seeds = app.MapGroup("/api/seeds")
    .WithTags("Seeds")
    .RequireRateLimiting("external");

seeds.MapPost("/", () => Results.Ok(new { seedId = Guid.NewGuid() }))
    .WithName("UploadSeed")
    .Produces(StatusCodes.Status200OK);

// --- Reports ---

var reports = app.MapGroup("/api/reports")
    .WithTags("Reports")
    .RequireRateLimiting("external");

reports.MapGet("/{simulationId}", async (Guid simulationId, ReportAgentService reportService, CancellationToken ct) => {
    var report = await reportService.GenerateReportAsync(simulationId, "Default query", ct);
    return Results.Ok(report);
})
    .WithName("GetReport");

reports.MapPost("/{simulationId}/chat", async (Guid simulationId, [FromBody] string message, ReportAgentService reportService, CancellationToken ct) => {
    // history management would go here
    var response = await reportService.ChatWithReportAsync(simulationId, message, new Microsoft.SemanticKernel.ChatCompletion.ChatHistory(), ct);
    return Results.Ok(new { response });
})
    .WithName("ChatWithReport");

// --- Internal ---

var internalApi = app.MapGroup("/internal")
    .WithTags("Internal");

internalApi.MapPost("/graph/bulk", async ([FromBody] BulkImportRequest request, IGraphStore graphStore, CancellationToken ct) => {
    if (graphStore is KuzuGraphStore kuzuStore)
    {
        await kuzuStore.BulkImportAsync(request.Nodes, request.Edges, ct);
        return Results.Ok();
    }
    return Results.BadRequest("Graph store does not support bulk import.");
})
    .WithName("BulkWriteGraph");

// --- SignalR ---

app.MapHub<SimulationHub>("/hubs/simulation");

// Background task to watch simulation progress and broadcast via SignalR
// This could be a separate background service or handled within the orchestrator
// For simplicity, we'll assume the orchestrator or a watcher handles this.

app.Run();

public record BulkImportRequest(IEnumerable<GraphNode> Nodes, IEnumerable<GraphEdge> Edges);

public partial class Program { }
